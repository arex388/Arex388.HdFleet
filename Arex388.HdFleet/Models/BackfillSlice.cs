namespace Arex388.HdFleet;

/// <summary>
/// Backfill slice object — a durable, ordered, bounded endpoint URL.
/// </summary>
public sealed class BackfillSlice {
	/// <summary>
	/// The slice's endpoint URL. Treat as opaque — consume it as given.
	/// </summary>
	public string Endpoint { get; init; } = null!;

	/// <summary>
	/// The slice's window start timestamp.
	/// </summary>
	public DateTimeOffset From { get; init; }

	/// <summary>
	/// The slice's order in the job. Consume slices in ascending sequence,
	/// exhausting each slice's inner cursor before moving to the next.
	/// </summary>
	public int Sequence { get; init; }

	/// <summary>
	/// The slice's status. Value set undocumented beyond <c>"ready"</c>.
	/// </summary>
	public string? Status { get; init; }

	/// <summary>
	/// The slice's window end timestamp.
	/// </summary>
	public DateTimeOffset To { get; init; }
}
