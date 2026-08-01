namespace Arex388.HdFleet;

/// <summary>
/// Event object — a dashcam-oriented event, optionally with media attachments.
/// </summary>
public sealed class Event {
	/// <summary>
	/// The event's altitude.
	/// </summary>
	public decimal? Altitude { get; init; }

	/// <summary>
	/// The reporting tracking device's id.
	/// </summary>
	public string? DeviceId { get; init; }

	/// <summary>
	/// The associated driver's external id, if any.
	/// </summary>
	public ExternalDriverId? DriverId { get; init; }

	/// <summary>
	/// The associated driver's name, if any.
	/// </summary>
	public string? DriverName { get; init; }

	/// <summary>
	/// The vehicle's heading in degrees.
	/// </summary>
	public decimal? HeadingInDegrees { get; init; }

	/// <summary>
	/// The event's external id.
	/// </summary>
	public ExternalEventId Id { get; init; }

	/// <summary>
	/// The reporting tracking device's IMEI.
	/// </summary>
	public string? Imei { get; init; }

	/// <summary>
	/// The event's latitude.
	/// </summary>
	public decimal? Latitude { get; init; }

	/// <summary>
	/// The event's longitude.
	/// </summary>
	public decimal? Longitude { get; init; }

	/// <summary>
	/// The event's media attachments, if any.
	/// </summary>
	/// <remarks>
	/// Media URLs may be existing HD Fleet blob URLs — treat them as opaque; do
	/// not parse, rewrite, or assume a lifetime. No expiry semantics are
	/// documented, so assume they can go stale.
	/// </remarks>
	public EventMedia? Media { get; init; }

	/// <summary>
	/// The event's occurrence timestamp.
	/// </summary>
	public DateTimeOffset OccurredAt { get; init; }

	/// <summary>
	/// The vehicle's speed in miles per hour.
	/// </summary>
	public decimal? SpeedInMilesPerHour { get; init; }

	/// <summary>
	/// The event's type.
	/// </summary>
	public string Type { get; init; } = null!;

	/// <summary>
	/// The vehicle's external id, if any.
	/// </summary>
	public ExternalVehicleId? VehicleId { get; init; }

	/// <summary>
	/// The vehicle's VIN.
	/// </summary>
	public string? Vin { get; init; }
}
