using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Reflection;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class TimeoutTests {
	[Fact]
	public async Task HandlerDelayingPastTimeout_ReturnsTimedOut() {
		using var provider = TestClients.CreateProvider(new DelayingHandler(), timeout: TimeSpan.FromMilliseconds(200));

		var client = provider.GetRequiredService<IHdFleetClient>();

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.TimedOut, "the library's deadline fired while the caller's token did not");
		response.Errors[0].Should().Be("The request has failed.");
		response.Metadata.Should().NotBeNull("a send was attempted");
		response.Metadata.StatusCode.Should().BeNull("no response arrived");
		response.Metadata.Headers.Should().BeEmpty();
		response.Metadata.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task CallerCancellation_BeforeTimeout_ReturnsCancelled() {
		using var provider = TestClients.CreateProvider(new DelayingHandler(), timeout: TimeSpan.FromSeconds(20));

		var client = provider.GetRequiredService<IHdFleetClient>();

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var response = await client.ListVehiclesAsync(cts.Token);

		response.Status.Should().Be(ResponseStatus.Cancelled, "the caller's token wins over the library's deadline");
		response.Metadata.Should().NotBeNull("a mid-flight cancellation happens after the send was attempted");
	}

	[Fact]
	public async Task StreamTimedOut_ThrowsHdFleetException_NotOperationCanceled() {
		using var provider = TestClients.CreateProvider(new DelayingHandler(), timeout: TimeSpan.FromMilliseconds(200));

		var client = provider.GetRequiredService<IHdFleetClient>();

		var act = async () => {
			await foreach (var _ in client.StreamLatestLocationsAsync()) {
			}
		};

		var exception = (await act.Should().ThrowExactlyAsync<HdFleetException>()).Which;

		exception.Status.Should().Be(ResponseStatus.TimedOut);
		exception.Message.Should().StartWith("locations/latest?limit=200 returned TimedOut after ");
	}

	[Fact]
	public async Task TimeoutAbove100Seconds_IsTheConfiguredDeadline() {
		var timeout = TimeSpan.FromMinutes(5);

		using var provider = TestClients.CreateProvider(new CapturingHandler("{ \"data\": [] }"), timeout: timeout);

		var client = provider.GetRequiredService<IHdFleetClient>();
		var clientType = client.GetType();

		//	The deadline the client applies through CancelAfter, and the
		//	HttpClient it sends through: nothing may cap it at the BCL's
		//	100-second default.
		var options = (HdFleetClientOptions)clientType.GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;
		var effectiveTimeout = (TimeSpan)typeof(HdFleetClientOptions).GetProperty("EffectiveTimeout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(options)!;
		var httpClient = (HttpClient)clientType.GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;

		effectiveTimeout.Should().Be(timeout);
		httpClient.Timeout.Should().Be(Timeout.InfiniteTimeSpan, "HttpClient.Timeout would otherwise fire at 100 seconds");

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeTrue();
	}

	[Fact]
	public void NullTimeout_DefaultsTo100Seconds() {
		var options = new HdFleetClientOptions {
			Key = TestClients.ApiKey
		};

		var effectiveTimeout = (TimeSpan)typeof(HdFleetClientOptions).GetProperty("EffectiveTimeout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(options)!;

		effectiveTimeout.Should().Be(TimeSpan.FromSeconds(100));
	}

	[Fact]
	public async Task SuccessWithinTimeout_IsNotAffected() {
		using var provider = TestClients.CreateProvider(new CapturingHandler("{ \"data\": [] }", HttpStatusCode.OK), timeout: TimeSpan.FromSeconds(5));

		var client = provider.GetRequiredService<IHdFleetClient>();

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeTrue();
		response.Status.Should().Be(ResponseStatus.Succeeded);
	}

	[Fact]
	public async Task RetryAfterPastDeadline_ReturnsRateLimited_WithoutTheDelay() {
		//	A 429 with Retry-After: 2 would make the retry handler wait two
		//	seconds, which the 200 ms deadline cannot fit; the handler returns
		//	the 429 at once rather than sleeping into a TimedOut.
		var handler = new RetryAfterHandler(TimeSpan.FromSeconds(2));

		using var provider = TestClients.CreateProvider(handler, timeout: TimeSpan.FromMilliseconds(200));

		var client = provider.GetRequiredService<IHdFleetClient>();
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		var response = await client.ListVehiclesAsync();

		stopwatch.Stop();

		response.Status.Should().Be(ResponseStatus.RateLimited);
		stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1.5), "the handler does not sleep a Retry-After the deadline cannot fit");
		handler.Requests.Should().Be(1, "the retry never went out");
	}

	[Fact]
	public void FactoryClient_CarriesItsConfiguredTimeout() {
		using var provider = TestClients.CreateFactoryProvider(new CapturingHandler("{ \"data\": [] }"));

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();
		var timeout = TimeSpan.FromSeconds(42);

		var client = factory.CreateClient(new HdFleetClientOptions {
			Key = TestClients.ApiKey,
			Timeout = timeout
		});

		var options = (HdFleetClientOptions)client.GetType().GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;
		var effectiveTimeout = (TimeSpan)typeof(HdFleetClientOptions).GetProperty("EffectiveTimeout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(options)!;
		var httpClient = (HttpClient)client.GetType().GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;

		effectiveTimeout.Should().Be(timeout);
		httpClient.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
	}

	private sealed class RetryAfterHandler(
		TimeSpan retryAfter) :
		HttpMessageHandler {
		public int Requests { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) {
			Requests++;

			var response = new HttpResponseMessage((HttpStatusCode)429) {
				Content = new StringContent("{}")
			};

			response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);

			return Task.FromResult(response);
		}
	}
}
