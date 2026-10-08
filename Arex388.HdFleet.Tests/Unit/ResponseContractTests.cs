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

	[Fact]
	public async Task SuccessfulResponses_CarryMetadata() {
		var date = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
		var client = TestClients.Create(new CapturingHandler(TestClients.ReadFixture("vehicles.json"), HttpStatusCode.OK, r => {
			r.Headers.Date = date;
			r.Headers.Age = TimeSpan.FromSeconds(5);
			r.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"");
			r.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue {
				MaxAge = TimeSpan.FromSeconds(60)
			};
		}));

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeTrue();
		response.Metadata.Should().NotBeNull();
		response.Metadata.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Metadata.Date.Should().Be(date);
		response.Metadata.Age.Should().Be(TimeSpan.FromSeconds(5));
		response.Metadata.ETag.Should().Be("\"v1\"");
		response.Metadata.CacheControl.Should().Be("max-age=60");
		response.Metadata.RecordCount.Should().Be(2, "the fixture page holds two vehicles");
		response.Metadata.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
		response.Metadata.FromCache.Should().BeFalse();
		response.Metadata.BodyExcerpt.Should().BeNull("the excerpt is captured on failures only");
		response.Metadata.Headers.Should().ContainKey("content-type", "content headers are captured too, case-insensitively");
	}

	[Fact]
	public async Task SingleObjectResponses_HaveNoRecordCount() {
		var client = TestClients.CreateWithJson(TestClients.ReadFixture("vehicle.json"), out _);

		var response = await client.GetVehicleAsync(new ExternalVehicleId("V-1"));

		response.Success.Should().BeTrue();
		response.Metadata!.RecordCount.Should().BeNull("the body is not a page envelope");
	}

	[Fact]
	public async Task FailedResponses_CarryMetadataWithBodyExcerpt() {
		var json = TestClients.ReadFixture("error-envelope.json");
		var client = TestClients.Create(new CapturingHandler(json, HttpStatusCode.BadRequest));

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeFalse();
		response.Metadata.Should().NotBeNull();
		response.Metadata.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		response.Metadata.BodyExcerpt.Should().Be(json);
		response.Metadata.RecordCount.Should().BeNull("an error body is not a page envelope");
	}

	[Fact]
	public async Task FailedResponses_BodyExcerpt_IsBoundedTo512Characters() {
		var client = TestClients.Create(new CapturingHandler(new string('x', 2000), HttpStatusCode.BadGateway));

		var response = await client.ListVehiclesAsync();

		response.Metadata!.BodyExcerpt.Should().HaveLength(512);
	}

	[Fact]
	public async Task DeserializationFailures_CarryMetadataWithBodyExcerpt() {
		var client = TestClients.CreateWithJson("{ broken", out _);

		var response = await client.ListVehiclesAsync();

		response.Status.Should().Be(ResponseStatus.Failed);
		response.Metadata.Should().NotBeNull();
		response.Metadata.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Metadata.BodyExcerpt.Should().Be("{ broken");
	}

	[Fact]
	public async Task NeverSentResponses_HaveNoMetadata() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out _);

		using var cts = new CancellationTokenSource();

		await cts.CancelAsync();

		var cancelled = await client.ListVehiclesAsync(cts.Token);
		var invalid = await client.ListLatestLocationsAsync(new ListLatestLocations.Request {
			Limit = 0
		});

		cancelled.Status.Should().Be(ResponseStatus.Cancelled);
		cancelled.Metadata.Should().BeNull("a pre-send cancellation never reached the wire");
		invalid.Status.Should().Be(ResponseStatus.Invalid);
		invalid.Metadata.Should().BeNull("a validation failure never reached the wire");
	}

	[Fact]
	public async Task MidFlightCancellation_CarriesMetadataWithoutStatusCode() {
		var client = TestClients.Create(new DelayingHandler());

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var response = await client.ListVehiclesAsync(cts.Token);

		response.Status.Should().Be(ResponseStatus.Cancelled);
		response.Metadata.Should().NotBeNull();
		response.Metadata.StatusCode.Should().BeNull("no response arrived");
		response.Metadata.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task RecordCount_IsTheWireArrayLength_NullElementsIncluded() {
		var client = TestClients.CreateWithJson("{ \"data\": [ null, { \"externalVehicleId\": \"V-1\" } ] }", out _);

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeTrue();
		response.Vehicles.Should().ContainSingle("the null element is dropped by the converter");
		response.Metadata!.RecordCount.Should().Be(2, "the record count is taken before mapping");
	}

	[Theory]
	[InlineData("{}")]
	[InlineData("{ \"data\": \"not-an-array\" }")]
	public async Task RecordCount_IsNull_WhenDataIsNotAnArray(
		string json) {
		var client = TestClients.CreateWithJson(json, out _);

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeTrue();
		response.Metadata!.RecordCount.Should().BeNull();
	}

	[Fact]
	public async Task TransportException_CarriesMetadataWithoutStatusCode() {
		var client = TestClients.Create(new ThrowingHandler());

		var response = await client.ListVehiclesAsync();

		response.Status.Should().Be(ResponseStatus.Failed);
		response.Errors[1].Should().StartWith("HttpRequestException: ");
		response.Metadata.Should().NotBeNull();
		response.Metadata.StatusCode.Should().BeNull("no response arrived");
		response.Metadata.Elapsed.Should().BeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task BodyFailureAfterHeaders_RetainsStatusAndHeaders() {
		var client = TestClients.Create(new FailingBodyHandler());

		var response = await client.ListVehiclesAsync();

		response.Status.Should().Be(ResponseStatus.Failed);
		response.Metadata.Should().NotBeNull();
		response.Metadata.StatusCode.Should().Be(HttpStatusCode.OK, "the status arrived before the body transfer failed");
		response.Metadata.Headers.Should().ContainKey("X-Request-ID");
	}

	[Fact]
	public async Task MidBodyCancellation_RetainsStatusCode() {
		var client = TestClients.Create(new DelayedBodyHandler());

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var response = await client.ListVehiclesAsync(cts.Token);

		response.Status.Should().Be(ResponseStatus.Cancelled);
		response.Metadata!.StatusCode.Should().Be(HttpStatusCode.OK, "headers arrived before the body stalled");
	}

	[Fact]
	public async Task NeverSentOutcome_EndpointGetterThrows_ReturnsWithNullEndpoint() {
		//	No validated slice has an endpoint getter that can throw, so the
		//	guard is exercised through the pre-send Cancelled outcome: a
		//	GetBackfillEventsPage request with a cursor and a null slice
		//	endpoint throws inside its getter.
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		using var cts = new CancellationTokenSource();

		await cts.CancelAsync();

		var request = new GetBackfillEventsPage.Request {
			Cursor = "c1",
			SliceEndpoint = null!
		};

		var act = async () => await client.GetBackfillEventsPageAsync(request, cts.Token);

		var response = (await act.Should().NotThrowAsync()).Subject;

		response.Status.Should().Be(ResponseStatus.Cancelled);
		GetEndpoint(response).Should().BeNull("a getter that throws leaves Endpoint null");
		handler.Requests.Should().BeEmpty();
	}

	[Fact]
	public async Task EndpointGetterThrows_ReturnsFailed_WithoutSending() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var request = new GetBackfillEventsPage.Request {
			Cursor = "c1",
			SliceEndpoint = null!
		};

		var act = async () => await client.GetBackfillEventsPageAsync(request);

		var response = (await act.Should().NotThrowAsync()).Subject;

		response.Status.Should().Be(ResponseStatus.Failed);
		response.Errors[1].Should().StartWith("NullReferenceException: ");
		response.Metadata.Should().BeNull("the request was never sent");
		GetEndpoint(response).Should().BeNull();
		handler.Requests.Should().BeEmpty();
	}

	[Fact]
	public async Task InvalidOutcome_IsStampedWithEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out _);

		var response = await client.ListLatestLocationsAsync(new ListLatestLocations.Request {
			Limit = 0
		});

		response.Status.Should().Be(ResponseStatus.Invalid);
		GetEndpoint(response).Should().Be("locations/latest?limit=0");
	}

	private static string? GetEndpoint<TResponse>(
		ResponseBase<TResponse> response)
		where TResponse : ResponseBase<TResponse>, new() => (string?)typeof(ResponseBase<TResponse>).GetProperty("Endpoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(response);

	private sealed class ThrowingHandler :
		HttpMessageHandler {
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) => throw new HttpRequestException("Connection refused.");
	}

	private sealed class FailingBodyHandler :
		HttpMessageHandler {
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) {
			var response = new HttpResponseMessage(HttpStatusCode.OK) {
				Content = new FailingContent()
			};

			response.Headers.Add("X-Request-ID", "req-body");

			return Task.FromResult(response);
		}

		private sealed class FailingContent :
			HttpContent {
			protected override Task SerializeToStreamAsync(
				Stream stream,
				System.Net.TransportContext? context) => throw new IOException("The connection was reset.");

			protected override bool TryComputeLength(
				out long length) {
				length = 0;

				return false;
			}
		}
	}
}
