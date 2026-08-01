namespace Arex388.HdFleet;

/// <summary>
/// Integration object — one of HD Fleet's own upstream partner connections and
/// its health. A diagnostic surface, not part of the data path.
/// </summary>
public sealed class Integration {
	/// <summary>
	/// The integration's display name.
	/// </summary>
	public string? DisplayName { get; init; }

	/// <summary>
	/// The integration's id. Identifies HD Fleet's integration record, not a
	/// fleet entity.
	/// </summary>
	public string Id { get; init; } = null!;

	/// <summary>
	/// The integration's last error timestamp.
	/// </summary>
	public DateTimeOffset? LastErrorAt { get; init; }

	/// <summary>
	/// The integration's last success timestamp.
	/// </summary>
	public DateTimeOffset? LastSuccessAt { get; init; }

	/// <summary>
	/// The integration's mode. Value set undocumented.
	/// </summary>
	public string Mode { get; init; } = null!;

	/// <summary>
	/// The integration's partner. Value set undocumented.
	/// </summary>
	public string Partner { get; init; } = null!;

	/// <summary>
	/// The integration's status. Value set undocumented.
	/// </summary>
	public string Status { get; init; } = null!;

	/// <summary>
	/// The integration's last updated timestamp.
	/// </summary>
	public DateTimeOffset? UpdatedAt { get; init; }
}
