using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class RetryHandlerTests {
	private const string EmptyPage = "{ \"data\": [] }";

	[Fact]
	public async Task RateLimited_RetriesToCap_ThenClassifies() {
		//	SequenceHandler stamps Retry-After: 0 so only jitter delays remain.
		var handler = new SequenceHandler(((HttpStatusCode)429, EmptyPage));

		var client = TestClients.Create(handler);

		var response = await client.ListVehiclesAsync();

		handler.Requests.Should().HaveCount(4, "3 retries after the initial send, then the last response flows through");
		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.RateLimited);
	}

	[Fact]
	public async Task TransientFailure_RecoversWithinRetries() {
		var handler = new SequenceHandler(
			((HttpStatusCode)429, EmptyPage),
			(HttpStatusCode.InternalServerError, EmptyPage),
			(HttpStatusCode.OK, EmptyPage));

		var client = TestClients.Create(handler);

		var response = await client.ListVehiclesAsync();

		handler.Requests.Should().HaveCount(3, "429 then 500 then 200 — the success ends the retry loop");
		response.Success.Should().BeTrue();
	}

	[Theory]
	[InlineData(HttpStatusCode.BadRequest)]
	[InlineData(HttpStatusCode.Unauthorized)]
	[InlineData(HttpStatusCode.Forbidden)]
	[InlineData(HttpStatusCode.NotFound)]
	[InlineData(HttpStatusCode.Conflict)]
	[InlineData(HttpStatusCode.Gone)]
	public async Task NonRetryableStatuses_PassThroughOnFirstResponse(
		HttpStatusCode statusCode) {
		var handler = new SequenceHandler((statusCode, "{}"));

		var client = TestClients.Create(handler);

		var response = await client.ListVehiclesAsync();

		handler.Requests.Should().ContainSingle($"{(int)statusCode} must never be retried");
		response.Success.Should().BeFalse();
	}

	[Fact]
	public async Task CancellationDuringBackoff_SurfacesAsCancelled() {
		var handler = new SlowRetryHandler();

		var client = TestClients.Create(handler);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var response = await client.ListVehiclesAsync(cts.Token);

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Cancelled);
	}

	[Fact]
	public async Task RetryAfter_BeyondDeadline_ReturnsRateLimitedWithoutRetry() {
		var handler = new ScriptedHandler(TimeSpan.FromSeconds(60), int.MaxValue);

		using var provider = TestClients.CreateProvider(handler, timeout: TimeSpan.FromSeconds(10));

		var client = provider.GetRequiredService<IHdFleetClient>();
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		var response = await client.ListVehiclesAsync();

		stopwatch.Stop();

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.RateLimited, "the 429 is returned instead of a wait the deadline cannot fit");
		response.Metadata.Should().NotBeNull();
		response.Metadata.Headers["Retry-After"].Should().Be("60");
		handler.Requests.Should().Be(1, "no retry went out");
		stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "the handler never slept");
	}

	[Fact]
	public async Task RetryAfter_WithinDeadline_IsStillRetried() {
		var handler = new ScriptedHandler(TimeSpan.FromSeconds(1), 1);

		using var provider = TestClients.CreateProvider(handler, timeout: TimeSpan.FromSeconds(10));

		var client = provider.GetRequiredService<IHdFleetClient>();

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeTrue();
		handler.Requests.Should().Be(2, "a 1 s wait fits a 10 s deadline");
	}

	[Fact]
	public async Task Backoff_BeyondDeadline_ReturnsUnavailable() {
		//	No Retry-After, so the 1 s first backoff step (plus jitter) applies and
		//	cannot end before the 500 ms deadline.
		var handler = new CapturingHandler("{}", HttpStatusCode.ServiceUnavailable);

		using var provider = TestClients.CreateProvider(handler, timeout: TimeSpan.FromMilliseconds(500));

		var client = provider.GetRequiredService<IHdFleetClient>();

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Unavailable, "the 503 is returned rather than timed out");
		handler.Requests.Should().ContainSingle("the backoff never started");
	}

	[Fact]
	public async Task RequestWithoutDeadlineStamp_RetriesAsBefore() {
		var handler = new ScriptedHandler(TimeSpan.FromSeconds(1), 1);

		using var invoker = CreateUnstampedInvoker(handler);
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:9/api/external/v1/vehicles");
		using var response = await invoker.SendAsync(request, CancellationToken.None);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		handler.Requests.Should().Be(2, "with no stamp there is no deadline to respect");
	}

	[Fact]
	public async Task UnstampedRequest_LongRetryAfter_IsCancelledDuringTheDelay() {
		var handler = new ScriptedHandler(TimeSpan.FromSeconds(30), int.MaxValue);

		using var invoker = CreateUnstampedInvoker(handler);
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:9/api/external/v1/vehicles");
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

		var act = () => invoker.SendAsync(request, cts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>("with no stamp the handler starts the 30 s delay and the token cancels it");

		handler.Requests.Should().Be(1, "the retry never went out");
	}

	[Fact]
	public async Task TimeBeforeThe429_CountsAgainstTheDeadline() {
		//	A 1 s Retry-After fits the whole 2 s timeout, but the 1.5 s the
		//	response took leaves only 0.5 s of it.
		var handler = new ScriptedHandler(TimeSpan.FromSeconds(1), int.MaxValue, TimeSpan.FromSeconds(1.5));

		using var provider = TestClients.CreateProvider(handler, timeout: TimeSpan.FromSeconds(2));

		var client = provider.GetRequiredService<IHdFleetClient>();

		var response = await client.ListVehiclesAsync();

		response.Status.Should().Be(ResponseStatus.RateLimited, "the wait is compared with the time left, not with the whole timeout");
		handler.Requests.Should().Be(1, "no retry went out");
	}

	//	The retry handler is internal and the client always stamps, so an
	//	unstamped request is driven through it directly.
	private static HttpMessageInvoker CreateUnstampedInvoker(
		HttpMessageHandler inner) {
		var handlerType = typeof(IHdFleetClient).Assembly.GetType("Arex388.HdFleet.HdFleetRetryHandler")
						  ?? throw new InvalidOperationException("HdFleetRetryHandler was not found.");
		var retryHandler = Activator.CreateInstance(handlerType, true) as DelegatingHandler
						   ?? throw new InvalidOperationException("HdFleetRetryHandler could not be created.");

		retryHandler.InnerHandler = inner;

		return new HttpMessageInvoker(retryHandler);
	}

	//	429 with the given Retry-After for the first failures requests, then 200,
	//	each answered after preResponseDelay.
	private sealed class ScriptedHandler(
		TimeSpan retryAfter,
		int failures,
		TimeSpan preResponseDelay = default) :
		HttpMessageHandler {
		public int Requests { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) {
			Requests++;

			if (preResponseDelay > TimeSpan.Zero) {
				await Task.Delay(preResponseDelay, cancellationToken);
			}

			if (Requests > failures) {
				return new HttpResponseMessage(HttpStatusCode.OK) {
					Content = new StringContent(EmptyPage, System.Text.Encoding.UTF8, "application/json")
				};
			}

			var response = new HttpResponseMessage((HttpStatusCode)429) {
				Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
			};

			response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);

			return response;
		}
	}

	//	Always 429 with a 30 s Retry-After — the caller's token has to cancel
	//	the backoff delay, not the send.
	private sealed class SlowRetryHandler :
		HttpMessageHandler {
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) {
			var response = new HttpResponseMessage((HttpStatusCode)429) {
				Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
			};

			response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

			return Task.FromResult(response);
		}
	}
}
