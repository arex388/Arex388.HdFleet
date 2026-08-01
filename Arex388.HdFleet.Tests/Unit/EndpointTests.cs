using FluentAssertions;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class EndpointTests {
	[Fact]
	public async Task GetVehicle_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{}", out var handler);

		await client.GetVehicleAsync(new ExternalVehicleId("V-1"));

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/vehicles/V-1");
	}

	[Fact]
	public async Task ListLatestLocations_Default_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListLatestLocationsAsync();

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/locations/latest?limit=200");
	}

	[Fact]
	public async Task ListLatestLocations_Cursor_IsUrlEncoded() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListLatestLocationsAsync(new ListLatestLocations.Request {
			Cursor = "a b/c+d"
		});

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/locations/latest?limit=200&cursor=a+b%2Fc%2Bd");
	}

	[Fact]
	public async Task ListVehicles_Default_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListVehiclesAsync();

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/vehicles?limit=200");
	}

	[Fact]
	public async Task ListVehicles_CustomLimit_IsCarried() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListVehiclesAsync(new ListVehicles.Request {
			Limit = 50
		});

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/vehicles?limit=50");
	}
}
