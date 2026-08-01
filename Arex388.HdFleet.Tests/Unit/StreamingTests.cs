using FluentAssertions;
using System.Net;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class StreamingTests {
	[Fact]
	public async Task StreamVehicles_WalksPagesUntilNullCursor() {
		var page1 = "{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ], \"nextCursor\": \"c1\" }";
		var page2 = "{ \"data\": [ { \"externalVehicleId\": \"V-2\" } ], \"nextCursor\": null }";
		var handler = new SequenceHandler(
			(HttpStatusCode.OK, page1),
			(HttpStatusCode.OK, page2));

		var client = TestClients.Create(handler);

		var vehicles = new List<Vehicle>();

		await foreach (var vehicle in client.StreamVehiclesAsync()) {
			vehicles.Add(vehicle);
		}

		vehicles.Should().HaveCount(2);
		handler.Requests.Should().HaveCount(2, "the continuation page must go out despite the fresh-poll floor — cursor requests bypass the gate");
		handler.Requests[1].PathAndQuery.Should().Contain("cursor=c1");
	}

	[Fact]
	public async Task StreamVehicles_EndsOnFailedPage() {
		var page1 = "{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ], \"nextCursor\": \"c1\" }";
		var handler = new SequenceHandler(
			(HttpStatusCode.OK, page1),
			(HttpStatusCode.BadRequest, "{}"));

		var client = TestClients.Create(handler);

		var vehicles = new List<Vehicle>();

		await foreach (var vehicle in client.StreamVehiclesAsync()) {
			vehicles.Add(vehicle);
		}

		vehicles.Should().ContainSingle("the stream ends silently when a page request fails");
	}

	[Fact]
	public async Task StreamVehicles_NeverNullCursor_ThrowsAtPageCap() {
		//	The server keeps returning a cursor forever — the walk must abort
		//	loudly instead of spinning unbounded.
		var page = "{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ], \"nextCursor\": \"again\" }";
		var handler = new SequenceHandler((HttpStatusCode.OK, page));

		var client = TestClients.Create(handler);

		var act = async () => {
			await foreach (var _ in client.StreamVehiclesAsync()) {
			}
		};

		await act.Should().ThrowAsync<InvalidOperationException>();
		handler.Requests.Should().HaveCount(100);
	}

	[Fact]
	public async Task GetLatestLocation_ReturnsMatchingVehicleRow() {
		var client = TestClients.CreateWithFixtures();

		var location = await client.GetLatestLocationAsync(new ExternalVehicleId("V-17"));

		location.Should().NotBeNull();
		location.VehicleId.Should().Be(new ExternalVehicleId("V-17"));
	}

	[Fact]
	public async Task GetLatestLocation_UnknownVehicle_ReturnsNull() {
		var client = TestClients.CreateWithFixtures();

		var location = await client.GetLatestLocationAsync(new ExternalVehicleId("V-9999"));

		location.Should().BeNull();
	}
}
