using FluentAssertions;
using System.Globalization;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class ProMilesTests {
	[Fact]
	public async Task Customers_Deserialize() {
		var client = TestClients.CreateWithFixtures();

		var response = await client.ListProMilesCustomersAsync();

		response.Success.Should().BeTrue();
		response.Customers.Should().HaveCount(2);
		response.Customers[0].CustomerId.Should().Be(100);
		response.Customers[0].Name.Should().Be("Acme Trucking");
		response.Customers[0].CompanyId.Should().Be(7);
		response.Customers[1].Name.Should().BeNull();
		response.Customers[1].CompanyId.Should().BeNull();
	}

	[Fact]
	public async Task Vehicles_Deserialize() {
		var client = TestClients.CreateWithFixtures();

		var response = await client.ListProMilesVehiclesAsync(100);

		response.Success.Should().BeTrue();
		response.Vehicles.Should().HaveCount(2);
		response.Vehicles[0].Id.Should().Be(new ExternalVehicleId("V-1"));
		response.Vehicles[1].DisplayName.Should().BeNull();
	}

	[Fact]
	public async Task Report_Deserializes() {
		var client = TestClients.CreateWithFixtures();

		var response = await client.GetProMilesReportAsync(new GetProMilesReport.Request {
			FromDate = new DateTime(2026, 6, 1),
			ToDate = new DateTime(2026, 6, 2),
			VehicleName = "Truck 1"
		});

		response.Success.Should().BeTrue();
		response.Rows.Should().HaveCount(2);
		response.Rows[0].DistanceMiles.Should().Be(312.7M, "string-encoded numbers parse culture-invariantly");
		response.Rows[0].PointCount.Should().Be(480);
		response.Rows[0].FirstLocationAt.Should().Be(DateTimeOffset.Parse("2026-06-01T06:00:00Z", CultureInfo.InvariantCulture));
		response.Rows[1].VehicleName.Should().BeNull();
	}

	[Fact]
	public async Task ListCustomers_Default_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListProMilesCustomersAsync();

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/reports/promiles/customers?limit=200");
	}

	[Fact]
	public async Task ListVehicles_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListProMilesVehiclesAsync(100);

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/reports/promiles/vehicles?customerId=100&limit=200");
	}

	[Fact]
	public async Task GetReport_BuildsExactEndpoint_WithPlainDates() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.GetProMilesReportAsync(new GetProMilesReport.Request {
			FromDate = new DateTime(2026, 6, 1, 13, 45, 0),
			ToDate = new DateTime(2026, 6, 2),
			VehicleName = "Truck 1/A"
		});

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be(
			"/api/external/v1/reports/promiles?vName=Truck+1%2FA&fromDate=2026-06-01&toDate=2026-06-02&limit=200",
			"dates serialize as plain yyyy-MM-dd (time component ignored) and vName is URL-encoded");
	}

	[Fact]
	public async Task GetReport_EmptyVehicleName_FailsValidation_WithoutHttpCall() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		var response = await client.GetProMilesReportAsync(new GetProMilesReport.Request {
			FromDate = new DateTime(2026, 6, 1),
			ToDate = new DateTime(2026, 6, 2),
			VehicleName = ""
		});

		response.Success.Should().BeFalse();
		response.Status.Should().Be(ResponseStatus.Invalid);
		handler.Requests.Should().BeEmpty();
	}
}
