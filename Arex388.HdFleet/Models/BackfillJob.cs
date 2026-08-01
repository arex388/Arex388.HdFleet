namespace Arex388.HdFleet;

/// <summary>
/// Backfill job object.
/// </summary>
public sealed class BackfillJob {
	/// <summary>
	/// The job's created timestamp.
	/// </summary>
	public DateTimeOffset CreatedAt { get; init; }

	/// <summary>
	/// The job's expiration timestamp. An expired job's resources return
	/// <c>410</c> — recreate the job.
	/// </summary>
	public DateTimeOffset ExpiresAt { get; init; }

	/// <summary>
	/// The job's window start timestamp.
	/// </summary>
	public DateTimeOffset From { get; init; }

	/// <summary>
	/// The job's id.
	/// </summary>
	public BackfillJobId Id { get; init; }

	/// <summary>
	/// The job's reason, as requested. Value set undocumented.
	/// </summary>
	public string? Reason { get; init; }

	/// <summary>
	/// Whether an identical ready job was reused instead of a new one being created.
	/// </summary>
	public bool Reused { get; init; }

	/// <summary>
	/// The job's slice count.
	/// </summary>
	public int SliceCount { get; init; }

	/// <summary>
	/// The job's slice length in minutes.
	/// </summary>
	public int SliceMinutes { get; init; }

	/// <summary>
	/// The URL of the job's slices resource, if any. Treat as opaque.
	/// </summary>
	public string? SlicesUrl { get; init; }

	/// <summary>
	/// The job's status. Only <c>"ready"</c> is documented; other values exist
	/// but are undocumented — poll until <c>"ready"</c>.
	/// </summary>
	public string? Status { get; init; }

	/// <summary>
	/// The job's stream. Only <c>"events"</c> is documented.
	/// </summary>
	public string? Stream { get; init; }

	/// <summary>
	/// The job's window end timestamp.
	/// </summary>
	public DateTimeOffset To { get; init; }

	/// <summary>
	/// The vehicle the job is scoped to, if any.
	/// </summary>
	public ExternalVehicleId? VehicleId { get; init; }
}
