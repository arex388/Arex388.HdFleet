namespace Arex388.HdFleet;

//	The non-2xx error envelope: { "error": { "code", "message", "requestId",
//	"details" } }. All four are strings; code is an opaque vendor string and
//	deliberately not modeled as an enum.
internal sealed class ErrorEnvelope {
	public string? Code { get; init; }
	public string? Details { get; init; }
	public string? Message { get; init; }
	public string? RequestId { get; init; }
}
