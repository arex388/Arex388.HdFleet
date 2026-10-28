using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class FreshPollGateTests {
	[Fact]
	public async Task RapidFreshCalls_ProduceOneOutboundRequest() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var first = await client.ListLatestLocationsAsync();
		var second = await client.ListLatestLocationsAsync();
		var third = await client.ListLatestLocationsAsync();

		handler.Requests.Should().ContainSingle("calls within the 60 s floor must be served from the gate cache");
		first.FromCache.Should().BeFalse();
		second.FromCache.Should().BeTrue();
		third.FromCache.Should().BeTrue();
	}

	[Fact]
	public async Task ConcurrentFreshCalls_ProduceOneOutboundRequest() {
		var handler = new BlockingHandler("{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ] }");
		var client = TestClients.Create(handler);

		var first = client.ListVehiclesAsync();

		await handler.Started;

		var second = client.ListVehiclesAsync();

		handler.Release();

		var leader = await first;
		var joiner = await second;

		handler.Requests.Should().Be(1, "the second caller joins the request already on the wire");
		leader.FromCache.Should().BeFalse();
		joiner.FromCache.Should().BeTrue();
		joiner.Success.Should().BeTrue();
		joiner.Vehicles.Should().BeSameAs(leader.Vehicles, "the joiner receives the leader's response");
		joiner.Metadata.Should().NotBeNull();
		joiner.Metadata.FromCache.Should().BeTrue();
		joiner.Metadata.RecordCount.Should().Be(1);
	}

	[Fact]
	public async Task SameKeyClients_WithDifferentTimeouts_ShareInFlightRequests() {
		var handler = new BlockingHandler("{ \"data\": [] }");

		using var provider = TestClients.CreateFactoryProvider(handler);

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();
		var clientA = factory.CreateClient(new HdFleetClientOptions {
			Key = TestClients.ApiKey,
			Timeout = TimeSpan.FromSeconds(30)
		});
		var clientB = factory.CreateClient(new HdFleetClientOptions {
			Key = TestClients.ApiKey,
			Timeout = TimeSpan.FromSeconds(60)
		});

		clientB.Should().NotBeSameAs(clientA, "the factory keys clients by key and timeout");

		var first = clientA.ListVehiclesAsync();

		await handler.Started;

		var second = clientB.ListVehiclesAsync();

		handler.Release();

		var leader = await first;
		var joiner = await second;

		handler.Requests.Should().Be(1, "the gate is shared by every client in the container");
		leader.FromCache.Should().BeFalse();
		joiner.FromCache.Should().BeTrue();
	}

	[Fact]
	public async Task Joiner_WaitsUnderItsOwnDeadline_NotTheLeaders() {
		var handler = new BlockingHandler("{ \"data\": [] }");

		using var provider = TestClients.CreateFactoryProvider(handler);

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();
		var patient = factory.CreateClient(new HdFleetClientOptions {
			Key = TestClients.ApiKey,
			Timeout = TimeSpan.FromSeconds(30)
		});
		var hurried = factory.CreateClient(new HdFleetClientOptions {
			Key = TestClients.ApiKey,
			Timeout = TimeSpan.FromMilliseconds(200)
		});

		var first = patient.ListVehiclesAsync();

		await handler.Started;

		var joiner = await hurried.ListVehiclesAsync();

		joiner.Status.Should().Be(ResponseStatus.TimedOut, "the joiner's own 200 ms deadline fired while the leader's request was still on the wire");
		joiner.Metadata.Should().BeNull("no request was sent on the joiner's behalf");
		first.IsCompleted.Should().BeFalse("the leader's request is unaffected");

		handler.Release();

		var leader = await first;

		leader.Success.Should().BeTrue();
		handler.Requests.Should().Be(1);
	}

	[Fact]
	public async Task CancelledLeader_PromotesTheJoiner_UnderItsOriginalDeadline() {
		var handler = new BlockingHandler("{ \"data\": [] }");

		using var provider = TestClients.CreateFactoryProvider(handler);

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();
		var patient = factory.CreateClient(new HdFleetClientOptions {
			Key = TestClients.ApiKey,
			Timeout = TimeSpan.FromSeconds(30)
		});
		var hurried = factory.CreateClient(new HdFleetClientOptions {
			Key = TestClients.ApiKey,
			Timeout = TimeSpan.FromMilliseconds(600)
		});

		using var cts = new CancellationTokenSource();

		var first = patient.ListVehiclesAsync(cts.Token);

		await handler.Started;

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var second = hurried.ListVehiclesAsync();

		//	Cancel the leader well inside the joiner's deadline: the joiner is
		//	promoted and sends itself, and its 600 ms deadline keeps counting
		//	from when it first called rather than restarting.
		await Task.Delay(TimeSpan.FromMilliseconds(400));

		cts.Cancel();

		var leader = await first;
		var promoted = await second;

		stopwatch.Stop();

		leader.Status.Should().Be(ResponseStatus.Cancelled, "the leader's own token cancelled its request");
		promoted.Status.Should().Be(ResponseStatus.TimedOut, "the promoted joiner sent and then hit its own deadline");
		handler.Requests.Should().Be(2, "the leader's request and the promoted joiner's");
		stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(950), "a restarted deadline would end at about 1000 ms");
	}

	[Fact]
	public async Task CancelledJoiner_ReturnsCancelled_LeaderUnaffected() {
		var handler = new BlockingHandler("{ \"data\": [] }");
		var client = TestClients.Create(handler);

		var first = client.ListVehiclesAsync();

		await handler.Started;

		using var cts = new CancellationTokenSource();

		var second = client.ListVehiclesAsync(cts.Token);

		cts.Cancel();

		var joiner = await second;

		joiner.Status.Should().Be(ResponseStatus.Cancelled, "the joiner's token cancels only its own wait");
		joiner.Metadata.Should().BeNull("no request was sent on the joiner's behalf");
		first.IsCompleted.Should().BeFalse("the leader's request is still on the wire");

		handler.Release();

		var leader = await first;

		leader.Success.Should().BeTrue();
		leader.Status.Should().Be(ResponseStatus.Succeeded);
		handler.Requests.Should().Be(1);
	}

	[Fact]
	public async Task FailedLeader_JoinerReceivesTheFailure_WithoutSending() {
		var handler = new BlockingHandler("{}", HttpStatusCode.Unauthorized);
		var client = TestClients.Create(handler);

		var first = client.ListVehiclesAsync();

		await handler.Started;

		var second = client.ListVehiclesAsync();

		handler.Release();

		var leader = await first;
		var joiner = await second;

		handler.Requests.Should().Be(1, "the joiner never sends on its own behalf");
		leader.Status.Should().Be(ResponseStatus.Unauthorized);
		joiner.Status.Should().Be(ResponseStatus.Unauthorized, "one outage is reported once per caller");
		joiner.Success.Should().BeFalse();

		var third = await client.ListVehiclesAsync();

		third.Status.Should().Be(ResponseStatus.Unauthorized);
		handler.Requests.Should().Be(2, "a failed response is never cached, so the next caller sends afresh");
	}

	[Fact]
	public async Task CacheHit_CarriesOriginalMetadata_MarkedFromCache() {
		var client = TestClients.CreateWithJson("{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ] }", out _);

		var first = await client.ListVehiclesAsync();
		var second = await client.ListVehiclesAsync();

		second.FromCache.Should().BeTrue();
		second.Metadata.Should().NotBeNull();
		second.Metadata.FromCache.Should().BeTrue();
		second.Metadata.StatusCode.Should().Be(first.Metadata!.StatusCode);
		second.Metadata.Elapsed.Should().Be(first.Metadata.Elapsed, "every other field is the original exchange's");
		second.Metadata.RecordCount.Should().Be(1);
		first.Metadata.FromCache.Should().BeFalse("the stored instance the fresh caller holds must not change");
	}

	[Fact]
	public async Task CursorContinuations_AreNotGated() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var request = new ListLatestLocations.Request {
			Cursor = "abc"
		};

		await client.ListLatestLocationsAsync(request);
		await client.ListLatestLocationsAsync(request);

		handler.Requests.Should().HaveCount(2, "cursor continuations bypass the fresh-poll gate");
	}

	[Fact]
	public async Task DistinctQueries_GateIndependently() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListLatestLocationsAsync(new ListLatestLocations.Request {
			Limit = 100
		});
		await client.ListLatestLocationsAsync(new ListLatestLocations.Request {
			Limit = 200
		});

		handler.Requests.Should().HaveCount(2, "the normalized query is part of the gate key, so distinct queries gate independently");
	}

	[Fact]
	public async Task DistinctRoutes_GateIndependently() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListLatestLocationsAsync();
		await client.ListVehiclesAsync();

		handler.Requests.Should().HaveCount(2, "the route is part of the gate key");
	}

	[Fact]
	public async Task DifferentAccounts_ShareNoGateState() {
		var handler = new CapturingHandler("{ \"data\": [] }");

		using var provider = TestClients.CreateFactoryProvider(handler);

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();
		var clientA = factory.CreateClient(new HdFleetClientOptions {
			Key = "account-a"
		});
		var clientB = factory.CreateClient(new HdFleetClientOptions {
			Key = "account-b"
		});

		await clientA.ListLatestLocationsAsync();
		await clientB.ListLatestLocationsAsync();

		handler.Requests.Should().HaveCount(2, "two accounts sharing one IMemoryCache must not share gate entries");
	}

	[Fact]
	public void NamedHttpClient_HasNoDefaultAuthorizationHeader() {
		using var provider = TestClients.CreateProvider(new CapturingHandler("{ \"data\": [] }"));

		//	Force client construction, then inspect a freshly-created named
		//	HttpClient — registration must never plant a default Authorization
		//	header; auth is set per request.
		_ = provider.GetRequiredService<IHdFleetClient>();

		var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IHdFleetClient));

		httpClient.DefaultRequestHeaders.Authorization.Should().BeNull("a pooled client with a default Authorization header is a cross-account data leak");
	}

	[Fact]
	public async Task Requests_CarryPerRequestBearerHeader() {
		var handler = new HeaderCapturingHandler();

		var client = TestClients.Create(handler);

		await client.ListVehiclesAsync();

		handler.AuthorizationHeaders.Should().ContainSingle().Which.Should().Be("Bearer " + TestClients.ApiKey);
	}

	//	Signals when the first request arrives, then holds every request until
	//	released — a request on the wire that other callers can join.
	private sealed class BlockingHandler(
		string json,
		HttpStatusCode statusCode = HttpStatusCode.OK) :
		HttpMessageHandler {
		private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _requests;

		public int Requests => _requests;
		public Task Started => _started.Task;

		//	Releasing twice is harmless, so the result is not needed.
		public void Release() => _ = _release.TrySetResult(true);

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) {
			//	The count is read through Requests; only the first arrival
			//	signals Started, later ones are no-ops.
			_ = Interlocked.Increment(ref _requests);
			_ = _started.TrySetResult(true);

			await _release.Task.WaitAsync(cancellationToken);

			return new HttpResponseMessage(statusCode) {
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			};
		}
	}

	private sealed class HeaderCapturingHandler :
		HttpMessageHandler {
		public IList<string?> AuthorizationHeaders { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) {
			AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());

			return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
				Content = new StringContent("{ \"data\": [] }", System.Text.Encoding.UTF8, "application/json")
			});
		}
	}
}
