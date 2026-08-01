namespace Arex388.HdFleet;

/// <summary>
/// Vehicle object.
/// </summary>
public sealed class Vehicle {
	/// <summary>
	/// The vehicle's tracking device id.
	/// </summary>
	public string? DeviceId { get; init; }

	/// <summary>
	/// The vehicle's tracking device type.
	/// </summary>
	public string? DeviceType { get; init; }

	/// <summary>
	/// The vehicle's display name.
	/// </summary>
	public string? DisplayName { get; init; }

	/// <summary>
	/// The vehicle's last known heading in degrees.
	/// </summary>
	public decimal? HeadingInDegrees { get; init; }

	/// <summary>
	/// The vehicle's external id.
	/// </summary>
	public ExternalVehicleId Id { get; init; }

	/// <summary>
	/// The vehicle's tracking device IMEI.
	/// </summary>
	public string? Imei { get; init; }

	/// <summary>
	/// The vehicle's last tracking activity timestamp.
	/// </summary>
	/// <remarks>
	/// <c>GET /vehicles</c> has a 15-minute fresh-poll floor, so this is at
	/// 15-minute granularity — a metadata source, not a position source. Use
	/// <see cref="ListLatestLocations"/> for positions.
	/// </remarks>
	public DateTimeOffset? LastActivityAt { get; init; }

	/// <summary>
	/// The vehicle's last known latitude.
	/// </summary>
	/// <remarks>
	/// <c>GET /vehicles</c> has a 15-minute fresh-poll floor, so this is at
	/// 15-minute granularity — a metadata source, not a position source. Use
	/// <see cref="ListLatestLocations"/> for positions.
	/// </remarks>
	public decimal? LastLatitude { get; init; }

	/// <summary>
	/// The vehicle's last known longitude.
	/// </summary>
	/// <remarks>
	/// <c>GET /vehicles</c> has a 15-minute fresh-poll floor, so this is at
	/// 15-minute granularity — a metadata source, not a position source. Use
	/// <see cref="ListLatestLocations"/> for positions.
	/// </remarks>
	public decimal? LastLongitude { get; init; }

	/// <summary>
	/// The vehicle's license plate number.
	/// </summary>
	public string? LicensePlateNumber { get; init; }

	/// <summary>
	/// The vehicle's last known speed in miles per hour.
	/// </summary>
	public decimal? SpeedInMilesPerHour { get; init; }

	/// <summary>
	/// The vehicle's status.
	/// </summary>
	public VehicleStatus Status { get; init; }

	/// <summary>
	/// The vehicle's last updated timestamp.
	/// </summary>
	public DateTimeOffset? UpdatedAt { get; init; }

	/// <summary>
	/// The vehicle's VIN.
	/// </summary>
	public string? Vin { get; init; }
}
