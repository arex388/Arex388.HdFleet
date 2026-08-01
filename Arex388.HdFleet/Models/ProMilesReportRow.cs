namespace Arex388.HdFleet;

/// <summary>
/// ProMiles report row object.
/// </summary>
public sealed class ProMilesReportRow {
	/// <summary>
	/// The distance traveled in miles.
	/// </summary>
	public decimal? DistanceMiles { get; init; }

	/// <summary>
	/// The first location's timestamp.
	/// </summary>
	public DateTimeOffset? FirstLocationAt { get; init; }

	/// <summary>
	/// The last location's timestamp.
	/// </summary>
	public DateTimeOffset? LastLocationAt { get; init; }

	/// <summary>
	/// The number of location points in the row.
	/// </summary>
	public int PointCount { get; init; }

	/// <summary>
	/// The report's date.
	/// </summary>
	public DateTimeOffset? ReportDate { get; init; }

	/// <summary>
	/// The vehicle's name.
	/// </summary>
	public string? VehicleName { get; init; }
}
