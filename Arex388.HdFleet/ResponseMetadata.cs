using System.Net;

namespace Arex388.HdFleet;

/// <summary>
/// What the client observed about one HTTP exchange: status code, cache headers, timing and
/// the page's record count. Recorded on every <see cref="ResponseBase{TResponse}"/> the client
/// produces after a request is sent.
/// </summary>
/// <remarks>
/// Every value read off the wire has the raw API key replaced with <c>[redacted]</c> before it
/// is stored. <see cref="Headers"/> holds response headers only; the request's
/// <c>Authorization</c> header is never copied.
/// </remarks>
public sealed class ResponseMetadata {
	private static readonly IReadOnlyDictionary<string, string> _emptyHeaders = new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);

	internal ResponseMetadata() {
	}

	/// <summary>
	/// The response's <c>Age</c> header, if any.
	/// </summary>
	public TimeSpan? Age { get; internal init; }

	/// <summary>
	/// The first 512 characters of the response body when the request failed with a non-2xx
	/// status or the body could not be deserialized; <c>null</c> on success.
	/// </summary>
	public string? BodyExcerpt { get; internal init; }

	/// <summary>
	/// The response's <c>Cache-Control</c> header, if any.
	/// </summary>
	public string? CacheControl { get; internal init; }

	/// <summary>
	/// The response's <c>Date</c> header, if any.
	/// </summary>
	public DateTimeOffset? Date { get; internal init; }

	/// <summary>
	/// The wall-clock time from just before the request was sent to after the body was read,
	/// or to when the failure was caught. Includes any transparent retries.
	/// </summary>
	public TimeSpan Elapsed { get; internal init; }

	/// <summary>
	/// The response's <c>ETag</c> header, if any.
	/// </summary>
	public string? ETag { get; internal init; }

	/// <summary>
	/// Whether this response was answered by the fresh-poll gate instead of a live request.
	/// Every other value then describes the original exchange.
	/// </summary>
	public bool FromCache { get; internal init; }

	/// <summary>
	/// Every response and content header, with multiple values comma-joined. Keys are
	/// case-insensitive. Empty when no response arrived.
	/// </summary>
	public IReadOnlyDictionary<string, string> Headers { get; internal init; } = _emptyHeaders;

	/// <summary>
	/// The number of records in the page's <c>data</c> array before mapping; <c>null</c> when
	/// the body is not a page (single-object responses, error bodies, no body).
	/// </summary>
	public int? RecordCount { get; internal init; }

	/// <summary>
	/// The response's HTTP status code; <c>null</c> when no response arrived (timeout,
	/// cancellation, transport failure).
	/// </summary>
	public HttpStatusCode? StatusCode { get; internal init; }

	//	A gate cache hit reports the original exchange with FromCache flipped —
	//	a copy, so the stored instance the fresh caller holds never changes.
	internal ResponseMetadata WithFromCache() => new() {
		Age = Age,
		BodyExcerpt = BodyExcerpt,
		CacheControl = CacheControl,
		Date = Date,
		Elapsed = Elapsed,
		ETag = ETag,
		FromCache = true,
		Headers = Headers,
		RecordCount = RecordCount,
		StatusCode = StatusCode
	};
}
