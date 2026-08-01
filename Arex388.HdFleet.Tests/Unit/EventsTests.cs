using FluentAssertions;
using System.Globalization;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class EventsTests {
	private static ListEvents.Request CreateRequest(
		string? cursor = null) => new() {
			Cursor = cursor,
			From = new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero),
			To = new DateTimeOffset(2026, 7, 31, 18, 10, 0, TimeSpan.Zero)
		};

	[Fact]
	public async Task Events_Deserialize_FullPartialAndNullMedia() {
		var client = TestClients.CreateWithFixtures();

		var response = await client.ListEventsAsync(CreateRequest());

		response.Success.Should().BeTrue();
		response.Events.Should().HaveCount(3);

		var full = response.Events[0];

		full.Id.Should().Be(new ExternalEventId("E-1"));
		full.VehicleId.Should().Be(new ExternalVehicleId("V-1"));
		full.Type.Should().Be("harsh_braking");
		full.OccurredAt.Should().Be(DateTimeOffset.Parse("2026-07-31T18:05:00Z", CultureInfo.InvariantCulture));
		full.SpeedInMilesPerHour.Should().Be(63.5M, "string-encoded numbers parse culture-invariantly");
		full.Media.Should().NotBeNull();
		full.Media.ForwardVideoUrl.Should().Be("https://blobs.hdfleet.net/media/fwd-1.mp4");
		full.Media.Items.Should().HaveCount(2);
		full.Media.Items[0].CameraNumber.Should().Be(1);
		full.Media.Items[0].MediaStatus.Should().Be("ready");
		full.Media.Items[1].CameraNumber.Should().BeNull("an all-nulls media item deserializes cleanly");
		full.Media.Items[1].Url.Should().BeNull();

		var noMedia = response.Events[1];

		noMedia.Id.Should().Be(new ExternalEventId("E-2"));
		noMedia.VehicleId.Should().BeNull();
		noMedia.Media.Should().BeNull("null media stays null so 'no media at all' is distinguishable");

		var partial = response.Events[2];

		partial.Id.Should().Be(new ExternalEventId("E-3"));
		partial.Media.Should().NotBeNull();
		partial.Media.ForwardVideoUrl.Should().BeNull();
		partial.Media.Items.Should().BeEmpty();
	}

	[Fact]
	public async Task ListEvents_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListEventsAsync(CreateRequest());

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be(
			"/api/external/v1/events?from=2026-07-31T18%3A00%3A00.0000000%2B00%3A00&to=2026-07-31T18%3A10%3A00.0000000%2B00%3A00&limit=200");
	}

	[Fact]
	public async Task SixteenMinuteWindow_FailsValidation_WithoutHttpCall() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var response = await client.ListEventsAsync(new ListEvents.Request {
			From = new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero),
			To = new DateTimeOffset(2026, 7, 31, 18, 16, 0, TimeSpan.Zero)
		});

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Invalid);
		response.Errors.Should().Contain("HD Fleet requires a time window of 15 minutes or less.");
		handler.Requests.Should().BeEmpty("a 16-minute window must never reach the wire");
	}

	[Fact]
	public async Task SameWindow_IsGated_CursorContinuationsAreNot() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var fresh = CreateRequest();

		await client.ListEventsAsync(fresh);

		var second = await client.ListEventsAsync(fresh);

		handler.Requests.Should().ContainSingle();
		second.FromCache.Should().BeTrue();

		var continuation = CreateRequest("abc");

		await client.ListEventsAsync(continuation);
		await client.ListEventsAsync(continuation);

		handler.Requests.Should().HaveCount(3, "cursor continuations bypass the gate");
	}
}
