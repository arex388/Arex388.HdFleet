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
		/// Streams the events of a ready backfill job, implementing the documented
		/// consumption protocol: slices are walked strictly in ascending sequence,
		/// and each slice's inner cursor is exhausted before the next sequence
		/// begins. Processing is sequential by design.
		/// </summary>
		/// <param name="jobId">The backfill job's id.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>An async stream of <c>Event</c>.</returns>
		/// <remarks>
		/// The stream ends silently if any page request fails — including a
		/// <c>410</c> from an expired job mid-walk; a truncated stream is
		/// indistinguishable from an exhausted one here. Use
		/// <see cref="IHdFleetClient.ListBackfillSlicesAsync(ListBackfillSlices.Request, CancellationToken)"/>
		/// and
		/// <see cref="IHdFleetClient.GetBackfillEventsPageAsync(GetBackfillEventsPage.Request, CancellationToken)"/>
		/// when error visibility matters. Walks at most 100 slice-list pages and
		/// 100 pages per slice, then throws <see cref="InvalidOperationException"/>.
		/// </remarks>
		public async IAsyncEnumerable<Event> StreamBackfillEventsAsync(
			BackfillJobId jobId,
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default) {
			//	Collect every slice first — the walk must be strictly ordered by
			//	sequence, and slice-list pages are not guaranteed to be.
			var slices = new List<BackfillSlice>();

			string? cursor = null;
			var pages = 0;

			do {
				var response = await client.ListBackfillSlicesAsync(new ListBackfillSlices.Request {
					Cursor = cursor,
					Id = jobId
				}, cancellationToken).ConfigureAwait(false);

				if (!response.Success) {
					yield break;
				}

				slices.AddRange(response.Slices);

				cursor = response.NextCursor;
				pages++;

				if (pages >= PageCap && cursor.HasValue()) {
					throw new InvalidOperationException("HD Fleet returned a pagination cursor after " + PageCap + " pages — aborting the walk to avoid an unbounded loop.");
				}
			} while (cursor.HasValue());

			foreach (var slice in slices.OrderBy(s => s.Sequence)) {
				string? inner = null;
				var innerPages = 0;

				do {
					var response = await client.GetBackfillEventsPageAsync(new GetBackfillEventsPage.Request {
						Cursor = inner,
						SliceEndpoint = slice.Endpoint
					}, cancellationToken).ConfigureAwait(false);

					if (!response.Success) {
						yield break;
					}

					foreach (var @event in response.Events) {
						yield return @event;
					}

					inner = response.NextCursor;
					innerPages++;

					if (innerPages >= PageCap && inner.HasValue()) {
						throw new InvalidOperationException("HD Fleet returned a pagination cursor after " + PageCap + " pages — aborting the walk to avoid an unbounded loop.");
					}
				} while (inner.HasValue());
			}
		}

		/// <summary>
		/// Streams the account's drivers, walking cursor pages until exhaustion.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>An async stream of <c>Driver</c>.</returns>
		/// <remarks>
		/// The stream ends silently if a page request fails — a truncated stream is
		/// indistinguishable from an exhausted one here. Use
		/// <see cref="IHdFleetClient.ListDriversAsync(ListDrivers.Request, CancellationToken)"/>
		/// when error visibility matters. Cursor continuation pages bypass the
		/// fresh-poll gate. Walks at most 100 pages, then throws
		/// <see cref="InvalidOperationException"/> — a server returning a cursor
		/// forever must not spin.
		/// </remarks>
		public async IAsyncEnumerable<Driver> StreamDriversAsync(
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default) {
			string? cursor = null;
			var pages = 0;

			do {
				var response = await client.ListDriversAsync(new ListDrivers.Request {
					Cursor = cursor
				}, cancellationToken).ConfigureAwait(false);

				if (!response.Success) {
					yield break;
				}

				foreach (var driver in response.Drivers) {
					yield return driver;
				}

				cursor = response.NextCursor;
				pages++;

				if (pages >= PageCap && cursor.HasValue()) {
					throw new InvalidOperationException("HD Fleet returned a pagination cursor after " + PageCap + " pages — aborting the walk to avoid an unbounded loop.");
				}
			} while (cursor.HasValue());
		}

		/// <summary>
		/// Streams the location history for the account's vehicles within one
		/// bounded time window, walking cursor pages until exhaustion.
		/// </summary>
		/// <param name="from">The window's start timestamp.</param>
		/// <param name="to">The window's end timestamp. No more than 15 minutes after <paramref name="from"/>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>An async stream of <c>Location</c>.</returns>
		/// <remarks>
		/// Walks cursors within the given window only — it never advances the
		/// window; a day of history belongs to the backfills workflow, not this
		/// endpoint. The stream ends silently if a page request fails — a
		/// truncated stream is indistinguishable from an exhausted one here. Use
		/// <see cref="IHdFleetClient.ListLocationHistoryAsync(ListLocationHistory.Request, CancellationToken)"/>
		/// when error visibility matters. Cursor continuation pages bypass the
		/// fresh-poll gate. Walks at most 100 pages, then throws
		/// <see cref="InvalidOperationException"/> — a server returning a cursor
		/// forever must not spin.
		/// </remarks>
		public async IAsyncEnumerable<Location> StreamLocationHistoryAsync(
			DateTimeOffset from,
			DateTimeOffset to,
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default) {
			string? cursor = null;
			var pages = 0;

			do {
				var response = await client.ListLocationHistoryAsync(new ListLocationHistory.Request {
					Cursor = cursor,
					From = from,
					To = to
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
