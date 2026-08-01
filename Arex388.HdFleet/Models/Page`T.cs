namespace Arex388.HdFleet;

//	The cursor page envelope every HD Fleet collection endpoint wraps its items
//	in. Internal by design — the map step lifts Data/NextCursor into each
//	slice's public Response; consumers never see the envelope.
internal sealed class Page<T> {
	public IReadOnlyList<T> Data { get; init; } = [];
	public string? NextCursor { get; init; }
}
