namespace Arex388.HdFleet;

/// <summary>
/// An <see cref="Event"/>'s media attachments.
/// </summary>
/// <remarks>
/// Media URLs may be existing HD Fleet blob URLs — treat them as opaque; do not
/// parse, rewrite, or assume a lifetime. No expiry semantics are documented, so
/// assume they can go stale.
/// </remarks>
public sealed class EventMedia {
	/// <summary>
	/// The forward-facing camera's video URL, if any.
	/// </summary>
	public string? ForwardVideoUrl { get; init; }

	/// <summary>
	/// The inward-facing camera's video URL, if any.
	/// </summary>
	public string? InwardVideoUrl { get; init; }

	/// <summary>
	/// The individual media items.
	/// </summary>
	public IReadOnlyList<EventMediaItem> Items { get; init; } = [];
}
