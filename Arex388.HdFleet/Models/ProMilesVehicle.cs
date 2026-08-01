namespace Arex388.HdFleet;

/// <summary>
/// ProMiles vehicle object.
/// </summary>
public sealed class ProMilesVehicle {
	/// <summary>
	/// The vehicle's display name.
	/// </summary>
	public string? DisplayName { get; init; }

	/// <summary>
	/// The vehicle's external id.
	/// </summary>
	public ExternalVehicleId Id { get; init; }

	/// <summary>
	/// The vehicle's VIN.
	/// </summary>
	public string? Vin { get; init; }
}
