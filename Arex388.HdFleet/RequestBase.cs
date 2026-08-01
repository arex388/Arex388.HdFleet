namespace Arex388.HdFleet;

/// <summary>
/// The request's base details.
/// </summary>
public abstract class RequestBase {
	internal abstract string Endpoint { get; }

	//	The API has exactly one write (POST /backfills) — a minimal virtual
	//	default keeps every GET slice untouched rather than growing a general
	//	request-body framework. The "converters only read" rule stands: the one
	//	body is written with a hand-rolled Utf8JsonWriter in its slice.
	internal virtual HttpContent? GetContent() => null;

	internal virtual HttpMethod Method => HttpMethod.Get;
}
