namespace Arex388.HdFleet;

/// <summary>
/// The request's outcome classification. <see cref="ResponseBase{TResponse}.Success"/> remains
/// the primary check; branch on this when the failure class matters.
/// </summary>
public enum ResponseStatus :
	byte {
	/// <summary>
	/// The request succeeded.
	/// </summary>
	Succeeded,

	/// <summary>
	/// The request was cancelled before completion.
	/// </summary>
	Cancelled,

	/// <summary>
	/// The request failed client-side validation and was never sent.
	/// </summary>
	Invalid,

	/// <summary>
	/// HD Fleet rejected the request as invalid (HTTP 400).
	/// </summary>
	InvalidRequest,

	/// <summary>
	/// The API key is missing or invalid (HTTP 401).
	/// </summary>
	Unauthorized,

	/// <summary>
	/// The API key lacks the required scope (HTTP 403).
	/// </summary>
	Forbidden,

	/// <summary>
	/// The requested resource was not found (HTTP 404).
	/// </summary>
	NotFound,

	/// <summary>
	/// The request conflicts with existing state (HTTP 409).
	/// </summary>
	Conflict,

	/// <summary>
	/// The requested resource has expired or is no longer available (HTTP 410).
	/// </summary>
	Gone,

	/// <summary>
	/// The request was rate limited (HTTP 429).
	/// </summary>
	RateLimited,

	/// <summary>
	/// HD Fleet had an internal error (HTTP 500).
	/// </summary>
	ServerError,

	/// <summary>
	/// HD Fleet is temporarily unavailable (HTTP 503).
	/// </summary>
	Unavailable,

	/// <summary>
	/// The request failed for a reason not otherwise classified.
	/// </summary>
	Failed
}
