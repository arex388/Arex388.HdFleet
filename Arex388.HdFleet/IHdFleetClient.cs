namespace Arex388.HdFleet;

/// <summary>
/// HD Fleet API client.
/// </summary>
public interface IHdFleetClient {
	/// <summary>
	/// Creates a backfill job — the sanctioned path for historical catch-up.
	/// </summary>
	/// <remarks>
	/// A <c>202</c> means the job was accepted — poll
	/// <see cref="GetBackfillAsync(BackfillJobId, CancellationToken)"/> until its
	/// status is <c>"ready"</c>; a <c>200</c> with <c>Reused</c> means an
	/// identical ready job already existed; a <c>409</c> (<c>Status = Conflict</c>)
	/// means the window overlaps an existing ready job. This POST is never
	/// retried by the client — re-issue a failed create yourself; job reuse
	/// makes accidental duplicates harmless.
	/// </remarks>
	/// <param name="request">The <see cref="CreateBackfill.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="CreateBackfill.Response"/> response.</returns>
	Task<CreateBackfill.Response> CreateBackfillAsync(
		CreateBackfill.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a backfill job. Poll until its status is <c>"ready"</c>; a
	/// <c>410</c> (<c>Status = Gone</c>) means the job expired — recreate it.
	/// </summary>
	/// <param name="id">The backfill job's id.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="GetBackfill.Response"/> response.</returns>
	Task<GetBackfill.Response> GetBackfillAsync(
		BackfillJobId id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a backfill job. Poll until its status is <c>"ready"</c>; a
	/// <c>410</c> (<c>Status = Gone</c>) means the job expired — recreate it.
	/// </summary>
	/// <param name="request">The <see cref="GetBackfill.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="GetBackfill.Response"/> response.</returns>
	Task<GetBackfill.Response> GetBackfillAsync(
		GetBackfill.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns one page of an events backfill slice by its opaque endpoint URL.
	/// </summary>
	/// <remarks>
	/// Follow <c>NextCursor</c> inside the slice until null before moving to the
	/// next sequence — or use
	/// <see cref="HdFleetClientExtensions.StreamBackfillEventsAsync(IHdFleetClient, BackfillJobId, CancellationToken, Action{ResponseMetadata})"/>
	/// which implements the documented consumption protocol.
	/// </remarks>
	/// <param name="request">The <see cref="GetBackfillEventsPage.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="GetBackfillEventsPage.Response"/> response.</returns>
	Task<GetBackfillEventsPage.Response> GetBackfillEventsPageAsync(
		GetBackfillEventsPage.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a ProMiles mileage report for a vehicle over a date range.
	/// </summary>
	/// <remarks>
	/// Uniquely in this API, the report dates are plain calendar dates on the
	/// wire (<c>yyyy-MM-dd</c>) — the time component of the request's
	/// <c>DateTime</c> values is ignored.
	/// </remarks>
	/// <param name="request">The <see cref="GetProMilesReport.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="GetProMilesReport.Response"/> response.</returns>
	Task<GetProMilesReport.Response> GetProMilesReportAsync(
		GetProMilesReport.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a vehicle.
	/// </summary>
	/// <remarks>
	/// Repeated automated lookups are subject to client quota and global abuse
	/// protection — prefer <see cref="ListVehiclesAsync(CancellationToken)"/> for
	/// sync jobs; never call this in a loop.
	/// </remarks>
	/// <param name="id">The vehicle's external id.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="GetVehicle.Response"/> response.</returns>
	Task<GetVehicle.Response> GetVehicleAsync(
		ExternalVehicleId id,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a vehicle.
	/// </summary>
	/// <remarks>
	/// Repeated automated lookups are subject to client quota and global abuse
	/// protection — prefer <see cref="ListVehiclesAsync(CancellationToken)"/> for
	/// sync jobs; never call this in a loop.
	/// </remarks>
	/// <param name="request">The <see cref="GetVehicle.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="GetVehicle.Response"/> response.</returns>
	Task<GetVehicle.Response> GetVehicleAsync(
		GetVehicle.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a backfill job's slices — durable, ordered, bounded endpoint URLs.
	/// </summary>
	/// <remarks>
	/// Consume slices in ascending sequence, exhausting each slice's inner
	/// cursor before moving to the next. Unlike every other cursor endpoint,
	/// this endpoint's cursor is an integer on the wire — it still rides here as
	/// its opaque string form.
	/// </remarks>
	/// <param name="request">The <see cref="ListBackfillSlices.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListBackfillSlices.Response"/> response.</returns>
	Task<ListBackfillSlices.Response> ListBackfillSlicesAsync(
		ListBackfillSlices.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the account's drivers.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListDrivers.Response"/> response.</returns>
	Task<ListDrivers.Response> ListDriversAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the account's drivers.
	/// </summary>
	/// <param name="request">The <see cref="ListDrivers.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListDrivers.Response"/> response.</returns>
	Task<ListDrivers.Response> ListDriversAsync(
		ListDrivers.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the events for the account's vehicles within a bounded time window.
	/// </summary>
	/// <remarks>
	/// The window must be 15 minutes or less, and fresh queries are floored at 60
	/// seconds per new window. Event media URLs may be existing HD Fleet blob
	/// URLs — treat them as opaque; do not parse, rewrite, or assume a lifetime.
	/// </remarks>
	/// <param name="request">The <see cref="ListEvents.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListEvents.Response"/> response.</returns>
	Task<ListEvents.Response> ListEventsAsync(
		ListEvents.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns HD Fleet's own upstream partner integrations and their health.
	/// </summary>
	/// <remarks>
	/// A diagnostic surface, not part of the data path — not meant for tight
	/// polling.
	/// </remarks>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListIntegrations.Response"/> response.</returns>
	Task<ListIntegrations.Response> ListIntegrationsAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns HD Fleet's own upstream partner integrations and their health.
	/// </summary>
	/// <remarks>
	/// A diagnostic surface, not part of the data path — not meant for tight
	/// polling.
	/// </remarks>
	/// <param name="request">The <see cref="ListIntegrations.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListIntegrations.Response"/> response.</returns>
	Task<ListIntegrations.Response> ListIntegrationsAsync(
		ListIntegrations.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the latest locations for the account's vehicles.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListLatestLocations.Response"/> response.</returns>
	Task<ListLatestLocations.Response> ListLatestLocationsAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the latest locations for the account's vehicles.
	/// </summary>
	/// <param name="request">The <see cref="ListLatestLocations.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListLatestLocations.Response"/> response.</returns>
	Task<ListLatestLocations.Response> ListLatestLocationsAsync(
		ListLatestLocations.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the location history for the account's vehicles within a bounded
	/// time window.
	/// </summary>
	/// <remarks>
	/// The window must be 15 minutes or less, and fresh queries are floored at 60
	/// seconds per new window. Do not brute-force history through this endpoint —
	/// a day of history is ~96 sequential windowed queries at 60 seconds apart;
	/// use the backfills workflow instead.
	/// </remarks>
	/// <param name="from">The window's start timestamp.</param>
	/// <param name="to">The window's end timestamp. No more than 15 minutes after <paramref name="from"/>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListLocationHistory.Response"/> response.</returns>
	Task<ListLocationHistory.Response> ListLocationHistoryAsync(
		DateTimeOffset from,
		DateTimeOffset to,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the location history for the account's vehicles within a bounded
	/// time window.
	/// </summary>
	/// <remarks>
	/// The window must be 15 minutes or less, and fresh queries are floored at 60
	/// seconds per new window. Do not brute-force history through this endpoint —
	/// a day of history is ~96 sequential windowed queries at 60 seconds apart;
	/// use the backfills workflow instead.
	/// </remarks>
	/// <param name="request">The <see cref="ListLocationHistory.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListLocationHistory.Response"/> response.</returns>
	Task<ListLocationHistory.Response> ListLocationHistoryAsync(
		ListLocationHistory.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the ProMiles customers.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListProMilesCustomers.Response"/> response.</returns>
	Task<ListProMilesCustomers.Response> ListProMilesCustomersAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the ProMiles customers.
	/// </summary>
	/// <param name="request">The <see cref="ListProMilesCustomers.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListProMilesCustomers.Response"/> response.</returns>
	Task<ListProMilesCustomers.Response> ListProMilesCustomersAsync(
		ListProMilesCustomers.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a ProMiles customer's vehicles.
	/// </summary>
	/// <param name="customerId">The ProMiles customer's id.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListProMilesVehicles.Response"/> response.</returns>
	Task<ListProMilesVehicles.Response> ListProMilesVehiclesAsync(
		int customerId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns a ProMiles customer's vehicles.
	/// </summary>
	/// <param name="request">The <see cref="ListProMilesVehicles.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListProMilesVehicles.Response"/> response.</returns>
	Task<ListProMilesVehicles.Response> ListProMilesVehiclesAsync(
		ListProMilesVehicles.Request request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the account's vehicles.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListVehicles.Response"/> response.</returns>
	Task<ListVehicles.Response> ListVehiclesAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns the account's vehicles.
	/// </summary>
	/// <param name="request">The <see cref="ListVehicles.Request"/> request.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="ListVehicles.Response"/> response.</returns>
	Task<ListVehicles.Response> ListVehiclesAsync(
		ListVehicles.Request request,
		CancellationToken cancellationToken = default);
}
