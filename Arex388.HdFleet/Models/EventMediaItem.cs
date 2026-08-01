namespace Arex388.HdFleet;

/// <summary>
/// An individual media item on an <see cref="Event"/>.
/// </summary>
/// <remarks>
/// Media URLs may be existing HD Fleet blob URLs — treat them as opaque; do not
/// parse, rewrite, or assume a lifetime. No expiry semantics are documented, so
/// assume they can go stale.
/// </remarks>
public sealed class EventMediaItem {
	/// <summary>
	/// The camera's number.
	/// </summary>
	public int? CameraNumber { get; init; }

	/// <summary>
	/// The media's end timestamp.
	/// </summary>
	public DateTimeOffset? EndAt { get; init; }

	/// <summary>
	/// The media's status.
	/// </summary>
	public string? MediaStatus { get; init; }

	/// <summary>
	/// The media's type.
	/// </summary>
	public string? MediaType { get; init; }

	/// <summary>
	/// The media's start timestamp.
	/// </summary>
	public DateTimeOffset? StartAt { get; init; }

	/// <summary>
	/// The media's URL.
	/// </summary>
	public string? Url { get; init; }
}
