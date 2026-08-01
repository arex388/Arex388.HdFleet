using FluentAssertions;
using System.Net;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class ResponseContractTests {
	[Fact]
	public async Task ErrorEnvelope_IsParsedIntoDetailAndRequestId() {
		var json = TestClients.ReadFixture("error-envelope.json");
		var client = TestClients.Create(new CapturingHandler(json, HttpStatusCode.BadRequest));

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.InvalidRequest);
		response.RequestId.Should().Be("req-12345", "error.requestId backfills when no X-Request-ID header is present");
		response.Errors[0].Should().Be("The request has failed.");
		response.Errors[1].Should().Be("quota_exceeded: The client quota has been exceeded. (Retry after the quota window resets.)");
	}

	[Theory]
	[InlineData(HttpStatusCode.BadRequest, ResponseStatus.InvalidRequest)]
	[InlineData(HttpStatusCode.Unauthorized, ResponseStatus.Unauthorized)]
	[InlineData(HttpStatusCode.Forbidden, ResponseStatus.Forbidden)]
	[InlineData(HttpStatusCode.NotFound, ResponseStatus.NotFound)]
	[InlineData(HttpStatusCode.Conflict, ResponseStatus.Conflict)]
	[InlineData(HttpStatusCode.Gone, ResponseStatus.Gone)]
	public async Task FailedResponses_ClassifyIntoResponseStatus(
		HttpStatusCode statusCode,
		ResponseStatus expectedStatus) {
		var client = TestClients.Create(new CapturingHandler("{}", statusCode));

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeFalse();
		response.Status.Should().Be(expectedStatus);
	}

	[Fact]
	public async Task FailedResponses_AreDistinctInstances() {
		var client = TestClients.Create(new CapturingHandler("oops", HttpStatusCode.BadRequest));

		var a = await client.ListVehiclesAsync();
		var b = await client.ListVehiclesAsync();

		a.Success.Should().BeFalse();
		b.Success.Should().BeFalse();
		a.Should().NotBeSameAs(b);
		a.Errors.Should().NotBeSameAs(b.Errors);
	}

	[Fact]
	public async Task FailedResponses_CarryStatusDetail_WhenBodyIsNotJson() {
		var client = TestClients.Create(new CapturingHandler("<html>oops</html>", HttpStatusCode.BadGateway));

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Failed);
		response.Errors[0].Should().Be("The request has failed.", "the first entry must stay stable for existing consumer string-matches");
		response.Errors[1].Should().Be("HTTP 502 Bad Gateway");
	}

	[Fact]
	public async Task FailedResponses_CarryExceptionDetail() {
		var client = TestClients.CreateWithJson("{ broken", out _);

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeFalse();
		response.Errors[0].Should().Be("The request has failed.");
		response.Errors[1].Should().StartWith("JsonException: ");
	}

	[Fact]
	public async Task SuccessfulResponses_CarryRequestId() {
		var client = TestClients.Create(new CapturingHandler("{ \"data\": [] }", HttpStatusCode.OK, r => r.Headers.Add("X-Request-ID", "req-99")));

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeTrue();
		response.RequestId.Should().Be("req-99");
	}

	[Fact]
	public async Task PreCancelledToken_ReturnsCancelled() {
		var client = TestClients.CreateWithFixtures();

		using var cts = new CancellationTokenSource();

		await cts.CancelAsync();

		var response = await client.ListVehiclesAsync(cts.Token);

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Cancelled);
		response.Errors.Should().ContainSingle().Which.Should().Be("The request was cancelled.");
	}

	[Fact]
	public async Task MidFlightCancellation_ReturnsCancelled() {
		var client = TestClients.Create(new DelayingHandler());

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var response = await client.ListVehiclesAsync(cts.Token);

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Cancelled);
	}

	[Fact]
	public async Task MidBodyCancellation_ReturnsCancelled() {
		var client = TestClients.Create(new DelayedBodyHandler());

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var response = await client.ListVehiclesAsync(cts.Token);

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Cancelled);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(201)]
	public async Task InvalidLimit_ReturnsInvalid_WithoutHttpCall(
		int limit) {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var response = await client.ListLatestLocationsAsync(new ListLatestLocations.Request {
			Limit = limit
		});

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Invalid);
		response.Errors.Should().NotBeEmpty();
		handler.Requests.Should().BeEmpty("validation failures must not reach the API");
	}
}
