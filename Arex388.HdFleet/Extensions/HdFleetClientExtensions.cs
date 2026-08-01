using System.Runtime.CompilerServices;

namespace Arex388.HdFleet;

/// <summary>
/// <c>IHdFleetClient</c> extensions.
/// </summary>
public static class HdFleetClientExtensions {
	//	A server bug returning a non-null cursor forever must not spin — cap
	//	total pages and bail loudly rather than iterating unbounded. At the
	//	200-item page maximum this allows 20,000 items per walk.
	private const int PageCap = 100;

	/// <param name="client">An instance of <c>HdFleetClient</c>.</param>
	extension(
		IHdFleetClient client) {
		/// <summary>
		/// Returns the latest location for a vehicle.
		/// </summary>
		/// <param name="id">The vehicle's external id.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>An instance of <c>Location</c>.</returns>
		/// <remarks>
		/// Returns <c>null</c> both when the vehicle has no latest location and when
		/// the underlying request failed — the two are indistinguishable here. Use
		/// <see cref="IHdFleetClient.ListLatestLocationsAsync(ListLatestLocations.Request, CancellationToken)"/>
		/// when error visibility matters.
		/// </remarks>
		public async Task<Location?> GetLatestLocationAsync(
			ExternalVehicleId id,
			CancellationToken cancellationToken = default) {
			await foreach (var location in client.StreamLatestLocationsAsync(cancellationToken).ConfigureAwait(false)) {
				if (location.VehicleId == id) {
					return location;
				}
			}

			return null;
		}

		/// <summary>
		/// Returns the latest locations recorded within the specified window.
		/// </summary>
		/// <param name="within">How far back a location's <c>RecordedAt</c> may be.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>A list of <c>Location</c>.</returns>
		/// <remarks>
		/// The freshness filter is applied client-side after the fact — it does not
		/// change what the API returns. Returns an empty list both when no locations
		/// are recent enough and when the underlying request failed — the two are
		/// indistinguishable here. Use
		/// <see cref="IHdFleetClient.ListLatestLocationsAsync(ListLatestLocations.Request, CancellationToken)"/>
		/// when error visibility matters.
		/// </remarks>
		public async Task<IReadOnlyList<Location>> ListRecentLocationsAsync(
			TimeSpan within,
			CancellationToken cancellationToken = default) {
			var at = DateTimeOffset.UtcNow - within;
			var locations = new List<Location>();

			await foreach (var location in client.StreamLatestLocationsAsync(cancellationToken).ConfigureAwait(false)) {
				if (location.RecordedAt >= at) {
					locations.Add(location);
				}
			}

			return locations;
		}

		/// <summary>
		/// Streams the latest locations for the account's vehicles, walking cursor
		/// pages until exhaustion.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>An async stream of <c>Location</c>.</returns>
		/// <remarks>
		/// The stream ends silently if a page request fails — a truncated stream is
		/// indistinguishable from an exhausted one here. Use
		/// <see cref="IHdFleetClient.ListLatestLocationsAsync(ListLatestLocations.Request, CancellationToken)"/>
		/// when error visibility matters. Cursor continuation pages bypass the
		/// fresh-poll gate. Walks at most 100 pages, then throws
		/// <see cref="InvalidOperationException"/> — a server returning a cursor
		/// forever must not spin.
		/// </remarks>
		public async IAsyncEnumerable<Location> StreamLatestLocationsAsync(
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default) {
			string? cursor = null;
			var pages = 0;

			do {
				var response = await client.ListLatestLocationsAsync(new ListLatestLocations.Request {
					Cursor = cursor
				}, cancellationToken).ConfigureAwait(false);

				if (!response.Success) {
					yield break;
				}

				foreach (var location in response.Locations) {
					yield return location;
				}

				cursor = response.NextCursor;
				pages++;

				if (pages >= PageCap && cursor.HasValue()) {
					throw new InvalidOperationException("HD Fleet returned a pagination cursor after " + PageCap + " pages — aborting the walk to avoid an unbounded loop.");
				}
			} while (cursor.HasValue());
		}

		/// <summary>
		/// Streams the account's vehicles, walking cursor pages until exhaustion.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>An async stream of <c>Vehicle</c>.</returns>
		/// <remarks>
		/// The stream ends silently if a page request fails — a truncated stream is
		/// indistinguishable from an exhausted one here. Use
		/// <see cref="IHdFleetClient.ListVehiclesAsync(ListVehicles.Request, CancellationToken)"/>
		/// when error visibility matters. Cursor continuation pages bypass the
		/// fresh-poll gate. Walks at most 100 pages, then throws
		/// <see cref="InvalidOperationException"/> — a server returning a cursor
		/// forever must not spin.
		/// </remarks>
		public async IAsyncEnumerable<Vehicle> StreamVehiclesAsync(
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default) {
			string? cursor = null;
			var pages = 0;

			do {
				var response = await client.ListVehiclesAsync(new ListVehicles.Request {
					Cursor = cursor
				}, cancellationToken).ConfigureAwait(false);

				if (!response.Success) {
					yield break;
				}

				foreach (var vehicle in response.Vehicles) {
					yield return vehicle;
				}

				cursor = response.NextCursor;
				pages++;

				if (pages >= PageCap && cursor.HasValue()) {
					throw new InvalidOperationException("HD Fleet returned a pagination cursor after " + PageCap + " pages — aborting the walk to avoid an unbounded loop.");
				}
			} while (cursor.HasValue());
		}
	}
}
