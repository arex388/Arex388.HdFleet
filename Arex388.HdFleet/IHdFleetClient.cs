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
