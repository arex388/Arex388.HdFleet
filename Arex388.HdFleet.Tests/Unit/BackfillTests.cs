using FluentAssertions;
using System.Net;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class BackfillTests {
	private static readonly BackfillJobId _jobId = new(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"));

	private static CreateBackfill.Request CreateRequest() => new() {
		From = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
		Reason = "initial_sync",
		To = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
		VehicleId = new ExternalVehicleId("V-1")
	};

	//  ============================================================================
	//  CreateBackfill
	//  ============================================================================

	[Fact]
	public async Task CreateBackfill_PostsExactBody() {
		var json = TestClients.ReadFixture("backfill.json");
		var client = TestClients.CreateWithJson(json, out var handler);

		var response = await client.CreateBackfillAsync(CreateRequest());

		response.Success.Should().BeTrue();
		handler.Methods.Should().ContainSingle().Which.Should().Be(HttpMethod.Post);
		handler.Requests[0].PathAndQuery.Should().Be("/api/external/v1/backfills");
		handler.Bodies[0].Should().Be(
			"{\"stream\":\"events\",\"from\":\"2026-06-01T00:00:00+00:00\",\"to\":\"2026-06-02T00:00:00+00:00\",\"sliceMinutes\":15,\"externalVehicleId\":\"V-1\",\"reason\":\"initial_sync\"}",
			"the POST body is hand-rolled with Utf8JsonWriter — no converter Write involved");
	}

	[Fact]
	public async Task CreateBackfill_Accepted202_MapsAsSuccess() {
		var json = TestClients.ReadFixture("backfill.json");
		var client = TestClients.Create(new CapturingHandler(json, HttpStatusCode.Accepted));

		var response = await client.CreateBackfillAsync(CreateRequest());

		response.Success.Should().BeTrue("202 means the job was accepted and carries the job body");
		response.Job!.Id.Should().Be(_jobId);
		response.Job.SliceCount.Should().Be(96);
		response.Job.Reused.Should().BeFalse();
	}

	[Fact]
	public async Task CreateBackfill_Reused200_CarriesReusedFlag() {
		var json = TestClients.ReadFixture("backfill.json").Replace("\"reused\": false", "\"reused\": true");
		var client = TestClients.CreateWithJson(json, out _);

		var response = await client.CreateBackfillAsync(CreateRequest());

		response.Success.Should().BeTrue();
		response.Job!.Reused.Should().BeTrue();
	}

	[Fact]
	public async Task CreateBackfill_Overlap409_ClassifiesConflict_WithoutRetry() {
		var handler = new CapturingHandler("{}", HttpStatusCode.Conflict);

		var client = TestClients.Create(handler);

		var response = await client.CreateBackfillAsync(CreateRequest());

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Conflict);
		handler.Requests.Should().ContainSingle("409 is non-retryable");
	}

	[Fact]
	public async Task CreateBackfill_TransientFailure_IsNeverRetried() {
		//	The POST must not retry even on retryable statuses — the retry
		//	handler is GET-only; the caller re-issues failed creates.
		var handler = new SequenceHandler(((HttpStatusCode)429, "{}"));

		var client = TestClients.Create(handler);

		var response = await client.CreateBackfillAsync(CreateRequest());

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.RateLimited);
		handler.Requests.Should().ContainSingle("POSTs are never retried");
	}

	[Fact]
	public async Task CreateBackfill_EightDayWindow_FailsValidation_WithoutHttpCall() {
		var client = TestClients.CreateWithJson("{}", out var handler);

		var response = await client.CreateBackfillAsync(new CreateBackfill.Request {
			From = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
			To = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero)
		});

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Invalid);
		response.Errors.Should().Contain("HD Fleet limits a backfill job to a window of 7 days or less.");
		handler.Requests.Should().BeEmpty("an 8-day window must never reach the wire");
	}

	//  ============================================================================
	//  GetBackfill
	//  ============================================================================

	[Fact]
	public async Task GetBackfill_BuildsExactEndpoint_AndDeserializes() {
		var json = TestClients.ReadFixture("backfill.json");
		var client = TestClients.CreateWithJson(json, out var handler);

		var response = await client.GetBackfillAsync(_jobId);

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/backfills/3fa85f64-5717-4562-b3fc-2c963f66afa6");
		response.Success.Should().BeTrue();
		response.Job!.Status.Should().Be("ready");
		response.Job.Stream.Should().Be("events");
		response.Job.VehicleId.Should().Be(new ExternalVehicleId("V-1"));
	}

	[Fact]
	public async Task GetBackfill_Expired410_ClassifiesGone() {
		var client = TestClients.Create(new CapturingHandler("{}", HttpStatusCode.Gone));

		var response = await client.GetBackfillAsync(_jobId);

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Gone);
	}

	//  ============================================================================
	//  ListBackfillSlices
	//  ============================================================================

	[Fact]
	public async Task ListBackfillSlices_HandlesIntegerCursor() {
		var json = "{ \"data\": [ { \"sequence\": 0, \"from\": \"2026-06-01T00:00:00Z\", \"to\": \"2026-06-01T00:15:00Z\", \"status\": \"ready\", \"endpoint\": \"events?from=a&to=b\" } ], \"nextCursor\": 3 }";
		var client = TestClients.CreateWithJson(json, out var handler);

		var response = await client.ListBackfillSlicesAsync(new ListBackfillSlices.Request {
			Id = _jobId
		});

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/backfills/3fa85f64-5717-4562-b3fc-2c963f66afa6/slices?limit=200");
		response.Success.Should().BeTrue();
		response.NextCursor.Should().Be("3", "the slices endpoint's integer cursor is tolerated and carried as its opaque string form");
		response.Slices.Should().ContainSingle().Which.Sequence.Should().Be(0);
	}

	//  ============================================================================
	//  Orchestrator
	//  ============================================================================

	[Fact]
	public async Task StreamBackfillEvents_WalksSlicesInSequence_InnerCursorsFirst() {
		//	Slices arrive out of order (seq 1 before seq 0); slice 0 has an
		//	inner cursor page. The walk must fully exhaust slice 0 before
		//	touching slice 1.
		var slicesJson = "{ \"data\": [ " +
			"{ \"sequence\": 1, \"from\": \"2026-06-01T00:15:00Z\", \"to\": \"2026-06-01T00:30:00Z\", \"status\": \"ready\", \"endpoint\": \"slice-b\" }, " +
			"{ \"sequence\": 0, \"from\": \"2026-06-01T00:00:00Z\", \"to\": \"2026-06-01T00:15:00Z\", \"status\": \"ready\", \"endpoint\": \"slice-a?x=1\" } " +
			"], \"nextCursor\": null }";
		var sliceAPage1 = "{ \"data\": [ { \"externalEventId\": \"E-1\", \"eventType\": \"a\", \"occurredAt\": \"2026-06-01T00:01:00Z\" } ], \"nextCursor\": \"in1\" }";
		var sliceAPage2 = "{ \"data\": [ { \"externalEventId\": \"E-2\", \"eventType\": \"a\", \"occurredAt\": \"2026-06-01T00:02:00Z\" } ], \"nextCursor\": null }";
		var sliceBPage1 = "{ \"data\": [ { \"externalEventId\": \"E-3\", \"eventType\": \"a\", \"occurredAt\": \"2026-06-01T00:16:00Z\" } ] }";

		var handler = new SequenceHandler(
			(HttpStatusCode.OK, slicesJson),
			(HttpStatusCode.OK, sliceAPage1),
			(HttpStatusCode.OK, sliceAPage2),
			(HttpStatusCode.OK, sliceBPage1));

		var client = TestClients.Create(handler);

		var events = new List<Event>();

		await foreach (var e in client.StreamBackfillEventsAsync(_jobId)) {
			events.Add(e);
		}

		events.Select(e => e.Id.Value).Should().Equal("E-1", "E-2", "E-3");
		handler.Requests.Should().HaveCount(4);
		handler.Requests[1].PathAndQuery.Should().Be("/api/external/v1/slice-a?x=1", "slice endpoints are consumed as given");
		handler.Requests[2].PathAndQuery.Should().Be("/api/external/v1/slice-a?x=1&cursor=in1", "the inner cursor appends to the slice's existing query string");
		handler.Requests[3].PathAndQuery.Should().Be("/api/external/v1/slice-b");
	}
}
