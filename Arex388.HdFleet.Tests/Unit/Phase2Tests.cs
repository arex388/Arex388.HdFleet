using FluentAssertions;
using System.Globalization;
using System.Net;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class Phase2Tests {
	//  ============================================================================
	//  Drivers
	//  ============================================================================

	[Fact]
	public async Task Drivers_Deserialize_WithNullsAndUnknownProperties() {
		var client = TestClients.CreateWithFixtures();

		var response = await client.ListDriversAsync();

		response.Success.Should().BeTrue();
		response.Drivers.Should().HaveCount(2);

		var first = response.Drivers[0];

		first.Id.Should().Be(new ExternalDriverId("D-1"));
		first.DisplayName.Should().Be("Jamie Driver");
		first.Email.Should().Be("jamie@example.com");
		first.Phone.Should().Be("+1-602-555-0100");
		first.LicenseState.Should().Be("AZ");
		first.UpdatedAt.Should().Be(DateTimeOffset.Parse("2026-07-31T12:00:00Z", CultureInfo.InvariantCulture));

		var second = response.Drivers[1];

		second.Id.Should().Be(new ExternalDriverId("D-2"));
		second.DisplayName.Should().BeNull();
		second.Email.Should().BeNull();
		second.UpdatedAt.Should().BeNull();
	}

	[Fact]
	public async Task ListDrivers_Default_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListDriversAsync();

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/drivers?limit=200");
	}

	[Fact]
	public async Task StreamDrivers_WalksPages() {
		var page1 = "{ \"data\": [ { \"externalDriverId\": \"D-1\" } ], \"nextCursor\": \"c1\" }";
		var page2 = "{ \"data\": [ { \"externalDriverId\": \"D-2\" } ] }";
		var handler = new SequenceHandler(
			(HttpStatusCode.OK, page1),
			(HttpStatusCode.OK, page2));

		var client = TestClients.Create(handler);

		var drivers = new List<Driver>();

		await foreach (var driver in client.StreamDriversAsync()) {
			drivers.Add(driver);
		}

		drivers.Should().HaveCount(2);
	}

	//  ============================================================================
	//  Location history
	//  ============================================================================

	[Fact]
	public async Task LocationHistory_Deserializes() {
		var client = TestClients.CreateWithFixtures();

		var response = await client.ListLocationHistoryAsync(
			DateTimeOffset.Parse("2026-07-31T18:00:00Z", CultureInfo.InvariantCulture),
			DateTimeOffset.Parse("2026-07-31T18:10:00Z", CultureInfo.InvariantCulture));

		response.Success.Should().BeTrue();

		var location = response.Locations.Should().ContainSingle().Subject;

		location.VehicleId.Should().Be(new ExternalVehicleId("V-1"));
		location.Odometer.Should().Be(100001.5M, "string-encoded numbers parse culture-invariantly");
		location.DriverId.Should().Be(new ExternalDriverId("D-1"));
	}

	[Fact]
	public async Task ListLocationHistory_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListLocationHistoryAsync(new ListLocationHistory.Request {
			From = new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero),
			To = new DateTimeOffset(2026, 7, 31, 18, 10, 0, TimeSpan.Zero)
		});

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be(
			"/api/external/v1/locations/history?from=2026-07-31T18%3A00%3A00.0000000%2B00%3A00&to=2026-07-31T18%3A10%3A00.0000000%2B00%3A00&limit=200");
	}

	[Fact]
	public async Task SixteenMinuteWindow_FailsValidation_WithoutHttpCall() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var response = await client.ListLocationHistoryAsync(new ListLocationHistory.Request {
			From = new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero),
			To = new DateTimeOffset(2026, 7, 31, 18, 16, 0, TimeSpan.Zero)
		});

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Invalid);
		response.Errors.Should().Contain("HD Fleet requires a time window of 15 minutes or less.");
		handler.Requests.Should().BeEmpty("a 16-minute window must never reach the wire");
	}

	[Fact]
	public async Task ToNotAfterFrom_FailsValidation() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var response = await client.ListLocationHistoryAsync(new ListLocationHistory.Request {
			From = new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero),
			To = new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero)
		});

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Invalid);
		handler.Requests.Should().BeEmpty();
	}

	[Fact]
	public async Task SameWindow_IsGated_DistinctWindowsAreNot() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var from = new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero);
		var to = new DateTimeOffset(2026, 7, 31, 18, 10, 0, TimeSpan.Zero);

		var first = await client.ListLocationHistoryAsync(from, to);
		var second = await client.ListLocationHistoryAsync(from, to);

		handler.Requests.Should().ContainSingle("the same window inside the floor is served from the gate cache");
		second.FromCache.Should().BeTrue();
		first.FromCache.Should().BeFalse();

		await client.ListLocationHistoryAsync(to, to.AddMinutes(10));

		handler.Requests.Should().HaveCount(2, "a distinct window is a new query and gates independently");
	}

	[Fact]
	public async Task HistoryCursorContinuations_AreNotGated() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var request = new ListLocationHistory.Request {
			Cursor = "abc",
			From = new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero),
			To = new DateTimeOffset(2026, 7, 31, 18, 10, 0, TimeSpan.Zero)
		};

		await client.ListLocationHistoryAsync(request);
		await client.ListLocationHistoryAsync(request);

		handler.Requests.Should().HaveCount(2, "cursor continuation within a window is exempt from the floor");
	}
}
