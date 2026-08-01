namespace Arex388.HdFleet;

/// <summary>
/// Driver object.
/// </summary>
public sealed class Driver {
	/// <summary>
	/// The driver's display name.
	/// </summary>
	public string? DisplayName { get; init; }

	/// <summary>
	/// The driver's email address.
	/// </summary>
	public string? Email { get; init; }

	/// <summary>
	/// The driver's external id.
	/// </summary>
	public ExternalDriverId Id { get; init; }

	/// <summary>
	/// The driver's license number.
	/// </summary>
	public string? LicenseNumber { get; init; }

	/// <summary>
	/// The driver's license state.
	/// </summary>
	public string? LicenseState { get; init; }

	/// <summary>
	/// The driver's phone number.
	/// </summary>
	public string? Phone { get; init; }

	/// <summary>
	/// The driver's last updated timestamp.
	/// </summary>
	public DateTimeOffset? UpdatedAt { get; init; }
}
