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
	public async Task StreamVehicles_FailedPage_ThrowsHdFleetException() {
		var page1 = "{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ], \"nextCursor\": \"c1\" }";
		var handler = new SequenceHandler(
			(HttpStatusCode.OK, page1),
			(HttpStatusCode.BadRequest, "{}"));

		var client = TestClients.Create(handler);

		var vehicles = new List<Vehicle>();

		var act = async () => {
			await foreach (var vehicle in client.StreamVehiclesAsync()) {
				vehicles.Add(vehicle);
			}
		};

		var exception = (await act.Should().ThrowAsync<HdFleetException>("a failed page throws rather than ending the stream silently")).Which;

		exception.Status.Should().Be(ResponseStatus.InvalidRequest);
		vehicles.Should().ContainSingle("items from earlier pages were already yielded");
	}

	[Fact]
	public async Task StreamLatestLocations_ServerErrorPage_ThrowsHdFleetException() {
		var page1 = "{ \"data\": [ { \"externalVehicleId\": \"V-1\", \"latitude\": 1, \"longitude\": 2, \"recordedAt\": \"2026-07-31T17:45:00Z\" } ], \"nextCursor\": \"c1\" }";
		var handler = new SequenceHandler(
			(HttpStatusCode.OK, page1),
			(HttpStatusCode.InternalServerError, "{}"));

		var client = TestClients.Create(handler);

		var count = 0;

		var act = async () => {
			await foreach (var _ in client.StreamLatestLocationsAsync()) {
				count++;
			}
		};

		var exception = (await act.Should().ThrowAsync<HdFleetException>()).Which;

		exception.Status.Should().Be(ResponseStatus.ServerError);
		exception.Endpoint.Should().Be("locations/latest?limit=200&cursor=c1");
		exception.Metadata.Should().NotBeNull();
		exception.Metadata.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		exception.Errors.Should().NotBeEmpty();
		exception.Message.Should().StartWith("locations/latest?limit=200&cursor=c1 returned ServerError (HTTP 500) after ");
		handler.Requests.Should().HaveCount(5, "the first page, then the failed page and its 3 transparent retries — the stream itself retries nothing");
		count.Should().Be(1);
	}

	[Fact]
	public async Task StreamVehicles_CallerCancellation_ThrowsOperationCanceledException() {
		var client = TestClients.Create(new DelayingHandler());

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var act = async () => {
			await foreach (var _ in client.StreamVehiclesAsync(cts.Token)) {
			}
		};

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task StreamVehicles_OnResponse_InvokedOncePerPage_AndReportsGatedRepeat() {
		var page1 = "{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ], \"nextCursor\": \"c1\" }";
		var page2 = "{ \"data\": [ { \"externalVehicleId\": \"V-2\" }, { \"externalVehicleId\": \"V-3\" } ], \"nextCursor\": null }";
		var handler = new SequenceHandler(
			(HttpStatusCode.OK, page1),
			(HttpStatusCode.OK, page2));

		var client = TestClients.Create(handler);

		var first = new List<ResponseMetadata>();

		await foreach (var _ in client.StreamVehiclesAsync(onResponse: first.Add)) {
		}

		first.Should().HaveCount(2, "one callback per page");
		first[0].FromCache.Should().BeFalse();
		first[0].RecordCount.Should().Be(1);
		first[0].StatusCode.Should().Be(HttpStatusCode.OK);
		first[1].FromCache.Should().BeFalse();
		first[1].RecordCount.Should().Be(2);

		var repeat = new List<ResponseMetadata>();

		await foreach (var _ in client.StreamVehiclesAsync(onResponse: repeat.Add)) {
		}

		repeat.Should().HaveCount(2);
		repeat[0].FromCache.Should().BeTrue("the cursor-less first page is answered by the fresh-poll gate");
		repeat[0].RecordCount.Should().Be(1, "a gated page reports the original exchange");
		repeat[1].FromCache.Should().BeFalse("cursor continuations bypass the gate");
		handler.Requests.Should().HaveCount(3);
	}

	[Fact]
	public async Task StreamLocationHistory_InvalidWindow_ThrowsWithoutCallback() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);
		var from = DateTimeOffset.UtcNow.AddHours(-2);
		var calls = 0;

		var act = async () => {
			await foreach (var _ in client.StreamLocationHistoryAsync(from, from.AddHours(1), onResponse: _ => calls++)) {
			}
		};

		var exception = (await act.Should().ThrowAsync<HdFleetException>()).Which;

		exception.Status.Should().Be(ResponseStatus.Invalid);
		exception.Metadata.Should().BeNull("the page was never sent");
		calls.Should().Be(0, "a page that was never sent skips the callback");
		handler.Requests.Should().BeEmpty();
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

	[Fact]
	public async Task GetLatestLocation_FailedPage_ThrowsHdFleetException() {
		var client = TestClients.Create(new CapturingHandler("{}", HttpStatusCode.Unauthorized));

		var act = async () => await client.GetLatestLocationAsync(new ExternalVehicleId("V-17"));

		(await act.Should().ThrowAsync<HdFleetException>()).Which.Status.Should().Be(ResponseStatus.Unauthorized);
	}

	[Fact]
	public async Task ListRecentLocations_FailedPage_ThrowsHdFleetException() {
		var client = TestClients.Create(new CapturingHandler("{}", HttpStatusCode.Forbidden));

		var act = async () => await client.ListRecentLocationsAsync(TimeSpan.FromMinutes(5));

		(await act.Should().ThrowAsync<HdFleetException>()).Which.Status.Should().Be(ResponseStatus.Forbidden);
	}
}
