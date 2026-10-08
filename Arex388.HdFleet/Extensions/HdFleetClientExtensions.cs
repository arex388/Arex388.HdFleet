using System.Diagnostics.CodeAnalysis;
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
		/// <returns>An instance of <c>Location</c>, or <c>null</c> when the vehicle has no latest location.</returns>
		/// <exception cref="HdFleetException">A page request failed.</exception>
		/// <exception cref="OperationCanceledException">The request was cancelled.</exception>
		/// <remarks>
		/// Walks <see cref="StreamLatestLocationsAsync(IHdFleetClient, CancellationToken, Action{ResponseMetadata})"/>,
		/// so a failed page request throws <see cref="HdFleetException"/> and cancellation throws
		/// <see cref="OperationCanceledException"/>.
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
		/// <exception cref="HdFleetException">A page request failed.</exception>
		/// <exception cref="OperationCanceledException">The request was cancelled.</exception>
		/// <remarks>
		/// The freshness filter is applied client-side after the fact — it does not
		/// change what the API returns. Walks
		/// <see cref="StreamLatestLocationsAsync(IHdFleetClient, CancellationToken, Action{ResponseMetadata})"/>,
		/// so a failed page request throws <see cref="HdFleetException"/> and cancellation throws
		/// <see cref="OperationCanceledException"/>.
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
		/// <param name="onResponse">Invoked once per page that was sent, slice-list pages included, with that page's <see cref="ResponseMetadata"/>.</param>
		/// <returns>An async stream of <c>Event</c>.</returns>
		/// <exception cref="HdFleetException">A page request failed — including a <c>410</c> from an expired job mid-walk.</exception>
		/// <exception cref="OperationCanceledException">The request was cancelled.</exception>
		/// <exception cref="InvalidOperationException">The walk exceeded 100 slice-list pages or 100 pages in one slice.</exception>
		/// <remarks>
		/// A failed page request throws <see cref="HdFleetException"/> carrying that page's
		/// status, errors, request id and metadata; events from earlier pages were already
		/// yielded, and nothing is retried beyond the client's transparent retries. Caller
		/// cancellation throws <see cref="OperationCanceledException"/>.
		/// <paramref name="onResponse"/> runs before a page's events are yielded or its exception
		/// is thrown; a page that was never sent (failed validation, or cancelled before
		/// sending) skips it. Walks at most 100 slice-list pages and 100 pages per slice, then
		/// throws <see cref="InvalidOperationException"/>.
		/// </remarks>
		[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The trailing optional callback keeps every existing positional call compiling.")]
		public async IAsyncEnumerable<Event> StreamBackfillEventsAsync(
			BackfillJobId jobId,
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default,
			Action<ResponseMetadata>? onResponse = null) {
			//	Collect every slice first — the walk must be strictly ordered by
			//	sequence, and slice-list pages are not guaranteed to be.
			var slices = new List<BackfillSlice>();

			string? cursor = null;
			var pages = 0;

			do {
				var request = new ListBackfillSlices.Request {
					Cursor = cursor,
					Id = jobId
				};
				var response = await client.ListBackfillSlicesAsync(request, cancellationToken).ConfigureAwait(false);

				EnsurePage(response, onResponse, cancellationToken);

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
					var request = new GetBackfillEventsPage.Request {
						Cursor = inner,
						SliceEndpoint = slice.Endpoint
					};
					var response = await client.GetBackfillEventsPageAsync(request, cancellationToken).ConfigureAwait(false);

					EnsurePage(response, onResponse, cancellationToken);

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
		/// <param name="onResponse">Invoked once per page that was sent, with that page's <see cref="ResponseMetadata"/>.</param>
		/// <returns>An async stream of <c>Driver</c>.</returns>
		/// <exception cref="HdFleetException">A page request failed.</exception>
		/// <exception cref="OperationCanceledException">The request was cancelled.</exception>
		/// <exception cref="InvalidOperationException">The walk exceeded 100 pages.</exception>
		/// <remarks>
		/// A failed page request throws <see cref="HdFleetException"/> carrying that page's
		/// status, errors, request id and metadata; drivers from earlier pages were already
		/// yielded, and nothing is retried beyond the client's transparent retries. Caller
		/// cancellation throws <see cref="OperationCanceledException"/>.
		/// <paramref name="onResponse"/> runs before a page's items are yielded or its exception
		/// is thrown, and reports <see cref="ResponseMetadata.FromCache"/> when the first page
		/// came from the fresh-poll gate; a page that was never sent (failed validation, or
		/// cancelled before sending) skips it. Cursor continuation pages bypass the fresh-poll
		/// gate. Walks at most 100 pages, then throws <see cref="InvalidOperationException"/> — a
		/// server returning a cursor forever must not spin.
		/// </remarks>
		[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The trailing optional callback keeps every existing positional call compiling.")]
		public async IAsyncEnumerable<Driver> StreamDriversAsync(
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default,
			Action<ResponseMetadata>? onResponse = null) {
			string? cursor = null;
			var pages = 0;

			do {
				var request = new ListDrivers.Request {
					Cursor = cursor
				};
				var response = await client.ListDriversAsync(request, cancellationToken).ConfigureAwait(false);

				EnsurePage(response, onResponse, cancellationToken);

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
		/// <param name="onResponse">Invoked once per page that was sent, with that page's <see cref="ResponseMetadata"/>.</param>
		/// <returns>An async stream of <c>Location</c>.</returns>
		/// <exception cref="HdFleetException">A page request failed, including a window that failed client-side validation.</exception>
		/// <exception cref="OperationCanceledException">The request was cancelled.</exception>
		/// <exception cref="InvalidOperationException">The walk exceeded 100 pages.</exception>
		/// <remarks>
		/// Walks cursors within the given window only — it never advances the
		/// window; a day of history belongs to the backfills workflow, not this
		/// endpoint. A failed page request throws <see cref="HdFleetException"/> carrying that
		/// page's status, errors, request id and metadata; locations from earlier pages were
		/// already yielded, and nothing is retried beyond the client's transparent retries.
		/// Caller cancellation throws <see cref="OperationCanceledException"/>.
		/// <paramref name="onResponse"/> runs before a page's items are yielded or its exception
		/// is thrown, and reports <see cref="ResponseMetadata.FromCache"/> when the first page
		/// came from the fresh-poll gate; a page that was never sent (failed validation, or
		/// cancelled before sending) skips it. Cursor continuation pages bypass the fresh-poll
		/// gate. Walks at most 100 pages, then throws <see cref="InvalidOperationException"/> — a
		/// server returning a cursor forever must not spin.
		/// </remarks>
		[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The trailing optional callback keeps every existing positional call compiling.")]
		public async IAsyncEnumerable<Location> StreamLocationHistoryAsync(
			DateTimeOffset from,
			DateTimeOffset to,
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default,
			Action<ResponseMetadata>? onResponse = null) {
			string? cursor = null;
			var pages = 0;

			do {
				var request = new ListLocationHistory.Request {
					Cursor = cursor,
					From = from,
					To = to
				};
				var response = await client.ListLocationHistoryAsync(request, cancellationToken).ConfigureAwait(false);

				EnsurePage(response, onResponse, cancellationToken);

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
		/// <param name="onResponse">Invoked once per page that was sent, with that page's <see cref="ResponseMetadata"/>.</param>
		/// <returns>An async stream of <c>Location</c>.</returns>
		/// <exception cref="HdFleetException">A page request failed.</exception>
		/// <exception cref="OperationCanceledException">The request was cancelled.</exception>
		/// <exception cref="InvalidOperationException">The walk exceeded 100 pages.</exception>
		/// <remarks>
		/// A failed page request throws <see cref="HdFleetException"/> carrying that page's
		/// status, errors, request id and metadata; locations from earlier pages were already
		/// yielded, and nothing is retried beyond the client's transparent retries. Caller
		/// cancellation throws <see cref="OperationCanceledException"/>.
		/// <paramref name="onResponse"/> runs before a page's items are yielded or its exception
		/// is thrown, and reports <see cref="ResponseMetadata.FromCache"/> when the first page
		/// came from the fresh-poll gate; a page that was never sent (failed validation, or
		/// cancelled before sending) skips it. Cursor continuation pages bypass the fresh-poll
		/// gate. Walks at most 100 pages, then throws <see cref="InvalidOperationException"/> — a
		/// server returning a cursor forever must not spin.
		/// </remarks>
		[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The trailing optional callback keeps every existing positional call compiling.")]
		public async IAsyncEnumerable<Location> StreamLatestLocationsAsync(
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default,
			Action<ResponseMetadata>? onResponse = null) {
			string? cursor = null;
			var pages = 0;

			do {
				var request = new ListLatestLocations.Request {
					Cursor = cursor
				};
				var response = await client.ListLatestLocationsAsync(request, cancellationToken).ConfigureAwait(false);

				EnsurePage(response, onResponse, cancellationToken);

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
		/// <param name="onResponse">Invoked once per page that was sent, with that page's <see cref="ResponseMetadata"/>.</param>
		/// <returns>An async stream of <c>Vehicle</c>.</returns>
		/// <exception cref="HdFleetException">A page request failed.</exception>
		/// <exception cref="OperationCanceledException">The request was cancelled.</exception>
		/// <exception cref="InvalidOperationException">The walk exceeded 100 pages.</exception>
		/// <remarks>
		/// A failed page request throws <see cref="HdFleetException"/> carrying that page's
		/// status, errors, request id and metadata; vehicles from earlier pages were already
		/// yielded, and nothing is retried beyond the client's transparent retries. Caller
		/// cancellation throws <see cref="OperationCanceledException"/>.
		/// <paramref name="onResponse"/> runs before a page's items are yielded or its exception
		/// is thrown, and reports <see cref="ResponseMetadata.FromCache"/> when the first page
		/// came from the fresh-poll gate; a page that was never sent (failed validation, or
		/// cancelled before sending) skips it. Cursor continuation pages bypass the fresh-poll
		/// gate. Walks at most 100 pages, then throws <see cref="InvalidOperationException"/> — a
		/// server returning a cursor forever must not spin.
		/// </remarks>
		[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The trailing optional callback keeps every existing positional call compiling.")]
		public async IAsyncEnumerable<Vehicle> StreamVehiclesAsync(
			[EnumeratorCancellation]
			CancellationToken cancellationToken = default,
			Action<ResponseMetadata>? onResponse = null) {
			string? cursor = null;
			var pages = 0;

			do {
				var request = new ListVehicles.Request {
					Cursor = cursor
				};
				var response = await client.ListVehiclesAsync(request, cancellationToken).ConfigureAwait(false);

				EnsurePage(response, onResponse, cancellationToken);

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

	//	============================================================================
	//	Utilities
	//	============================================================================

	//	Report the page, then fail loudly: an IAsyncEnumerable has no return
	//	envelope, so an exception is the only honest signal for a failed page.
	//	Cancellation keeps its idiomatic exception type so callers'
	//	`when (ct.IsCancellationRequested)` filters still match. A page that
	//	was never sent has no metadata and skips the callback.
	private static void EnsurePage<TResponse>(
		ResponseBase<TResponse> response,
		Action<ResponseMetadata>? onResponse,
		CancellationToken cancellationToken)
		where TResponse : ResponseBase<TResponse>, new() {
		if (response.Metadata is not null) {
			onResponse?.Invoke(response.Metadata);
		}

		if (response.Success) {
			return;
		}

		if (response.Status == ResponseStatus.Cancelled) {
			throw new OperationCanceledException(response.Errors[0], cancellationToken);
		}

		throw new HdFleetException(response.Endpoint ?? string.Empty, response.Status, response.Errors, response.RequestId, response.Metadata);
	}
}
