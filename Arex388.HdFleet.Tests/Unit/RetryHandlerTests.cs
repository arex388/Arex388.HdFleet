using FluentAssertions;
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
