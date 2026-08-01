namespace Arex388.HdFleet;

/// <summary>
/// HD Fleet API client.
/// </summary>
public interface IHdFleetClient {
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
}
