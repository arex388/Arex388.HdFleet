namespace Arex388.HdFleet;

/// <summary>
/// HD Fleet API client.
/// </summary>
public interface IHdFleetClient {
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
