namespace Arex388.HdFleet;

//	The non-generic view of a page the pipeline reads RecordCount from — the
//	send path only knows TModel, not the element type.
internal abstract class Page {
	//	The wire length of the data array, null elements included; null when
	//	data is absent or not an array. Data drops null elements, so the
	//	converter counts this rather than deriving it from Data.
	internal int? RecordCount { get; init; }
}

//	The cursor page envelope every HD Fleet collection endpoint wraps its items
//	in. Internal by design — the map step lifts Data/NextCursor into each
//	slice's public Response; consumers never see the envelope.
internal sealed class Page<T> :
	Page {
	public IReadOnlyList<T> Data { get; init; } = [];
	public string? NextCursor { get; init; }
}
