using FluentAssertions;
using System.Globalization;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class ConverterTests {
	[Fact]
	public async Task Vehicle_Deserializes_UnderGermanCulture() {
		//	Pins InvariantCulture parsing of string-encoded numbers — de-DE
		//	reads "33.448376" as 33448376 if the converter ever regresses to
		//	culture-sensitive parsing.
		var originalCulture = CultureInfo.CurrentCulture;

		CultureInfo.CurrentCulture = new CultureInfo("de-DE");

		try {
			var client = TestClients.CreateWithFixtures();
			var response = await client.GetVehicleAsync(new ExternalVehicleId("V-1"));

			response.Success.Should().BeTrue();
			response.Vehicle!.Id.Should().Be(new ExternalVehicleId("V-1"));
			response.Vehicle.LastLatitude.Should().Be(33.448376M, "string-encoded numbers must parse culture-invariantly");
			response.Vehicle.LastLongitude.Should().Be(-112.074036M);
			response.Vehicle.SpeedInMilesPerHour.Should().Be(42.5M);
			response.Vehicle.LicensePlateNumber.Should().Be("ABC-1234");
			response.Vehicle.LastActivityAt.Should().Be(DateTimeOffset.Parse("2026-07-31T17:44:30Z", CultureInfo.InvariantCulture));
		} finally {
			CultureInfo.CurrentCulture = originalCulture;
		}
	}

	[Fact]
	public async Task Vehicle_AllNullableFieldsNull_DeserializesCleanly() {
		var json = TestClients.ReadFixture("vehicle-nulls.json");
		var client = TestClients.CreateWithJson(json, out _);

		var response = await client.GetVehicleAsync(new ExternalVehicleId("V-NULLS"));

		response.Success.Should().BeTrue();
		response.Vehicle!.Id.Should().Be(new ExternalVehicleId("V-NULLS"));
		response.Vehicle.DisplayName.Should().BeNull();
		response.Vehicle.Vin.Should().BeNull();
		response.Vehicle.LastLatitude.Should().BeNull();
		response.Vehicle.LastActivityAt.Should().BeNull();
		response.Vehicle.Status.Should().Be(VehicleStatus.None, "a null status falls back to None");
	}

	[Fact]
	public async Task Vehicle_UnrecognizedStatus_FallsBackToNone() {
		//	The fixture's status is "active" — no VehicleStatus member is seeded
		//	yet, so every wire value must fall back to None instead of throwing.
		var client = TestClients.CreateWithFixtures();

		var response = await client.GetVehicleAsync(new ExternalVehicleId("V-1"));

		response.Success.Should().BeTrue();
		response.Vehicle!.Status.Should().Be(VehicleStatus.None);
	}

	[Fact]
	public async Task Location_AllNullableFieldsNull_DeserializesCleanly() {
		var json = "{ \"data\": [" + TestClients.ReadFixture("location-nulls.json") + "], \"nextCursor\": null }";
		var client = TestClients.CreateWithJson(json, out _);

		var response = await client.ListLatestLocationsAsync();

		response.Success.Should().BeTrue();

		var location = response.Locations.Should().ContainSingle().Subject;

		location.VehicleId.Should().Be(new ExternalVehicleId("V-NULLS"));
		location.Latitude.Should().Be(33.448376M);
		location.Longitude.Should().Be(-112.074036M);
		location.Odometer.Should().BeNull();
		location.DriverId.Should().BeNull();
		location.SpeedInMilesPerHour.Should().BeNull();
		location.Status.Should().BeNull();
	}

	[Fact]
	public async Task LatestLocationsPage_Deserializes_All200Records() {
		var client = TestClients.CreateWithFixtures();

		var response = await client.ListLatestLocationsAsync();

		response.Success.Should().BeTrue();
		response.Locations.Should().HaveCount(200, "the fixture carries a full 200-record page with unknown properties the converters must skip");
		response.NextCursor.Should().BeNull();

		var first = response.Locations[0];

		first.VehicleId.Should().Be(new ExternalVehicleId("V-1"));
		first.RecordedAt.Should().Be(DateTimeOffset.Parse("2026-07-31T18:01:00Z", CultureInfo.InvariantCulture));
		first.DeviceId.Should().Be("DEV-1");
	}

	[Fact]
	public async Task Page_NullNextCursor_MeansExhaustion() {
		var client = TestClients.CreateWithJson("{ \"data\": [], \"nextCursor\": null }", out _);

		var response = await client.ListLatestLocationsAsync();

		response.Success.Should().BeTrue();
		response.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task Page_AbsentNextCursor_MeansExhaustion() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out _);

		var response = await client.ListLatestLocationsAsync();

		response.Success.Should().BeTrue();
		response.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task Page_PresentNextCursor_IsLifted() {
		var client = TestClients.CreateWithJson("{ \"data\": [], \"nextCursor\": \"abc123\" }", out _);

		var response = await client.ListLatestLocationsAsync();

		response.Success.Should().BeTrue();
		response.NextCursor.Should().Be("abc123");
	}
}
