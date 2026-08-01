namespace Arex388.HdFleet;

/// <summary>
/// Location object.
/// </summary>
public sealed class Location {
	/// <summary>
	/// The location's altitude.
	/// </summary>
	public decimal? Altitude { get; init; }

	/// <summary>
	/// The reporting tracking device's id.
	/// </summary>
	public string? DeviceId { get; init; }

	/// <summary>
	/// The driver's external id, if any.
	/// </summary>
	public ExternalDriverId? DriverId { get; init; }

	/// <summary>
	/// The driver's name, if any.
	/// </summary>
	public string? DriverName { get; init; }

	/// <summary>
	/// The vehicle's heading in degrees.
	/// </summary>
	public decimal? HeadingInDegrees { get; init; }

	/// <summary>
	/// The reporting tracking device's IMEI.
	/// </summary>
	public string? Imei { get; init; }

	/// <summary>
	/// The location's latitude.
	/// </summary>
	public decimal Latitude { get; init; }

	/// <summary>
	/// The location's longitude.
	/// </summary>
	public decimal Longitude { get; init; }

	/// <summary>
	/// The vehicle's odometer reading.
	/// </summary>
	public decimal? Odometer { get; init; }

	/// <summary>
	/// The unit of the vehicle's odometer reading.
	/// </summary>
	public string? OdometerUnit { get; init; }

	/// <summary>
	/// The location's recorded timestamp. This is the GPS fix time, not the ingest time.
	/// </summary>
	public DateTimeOffset RecordedAt { get; init; }

	/// <summary>
	/// The vehicle's speed in miles per hour.
	/// </summary>
	public decimal? SpeedInMilesPerHour { get; init; }

	/// <summary>
	/// The vehicle's status.
	/// </summary>
	public string? Status { get; init; }

	/// <summary>
	/// The vehicle's external id.
	/// </summary>
	public ExternalVehicleId VehicleId { get; init; }

	/// <summary>
	/// The vehicle's VIN.
	/// </summary>
	public string? Vin { get; init; }
}
