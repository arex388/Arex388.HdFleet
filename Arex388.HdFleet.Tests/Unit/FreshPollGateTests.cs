using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

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
