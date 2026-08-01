namespace Arex388.HdFleet;

/// <summary>
/// HD Fleet API client factory for interacting with multiple accounts.
/// </summary>
public interface IHdFleetClientFactory {
	/// <summary>
	/// Create a client for an account. Clients are cached per API key — repeated
	/// calls with the same key return the same instance.
	/// </summary>
	/// <param name="options">The client's configuration options.</param>
	/// <returns>The <see cref="IHdFleetClient"/> client.</returns>
	IHdFleetClient CreateClient(
		HdFleetClientOptions options);
}
