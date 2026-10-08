using FluentValidation.Results;

namespace Arex388.HdFleet;

/// <summary>
/// The response's base details.
/// </summary>
/// <typeparam name="TResponse">The response's type.</typeparam>
public abstract class ResponseBase<TResponse>
	where TResponse : ResponseBase<TResponse>, new() {
	//	The request's relative endpoint with the key redacted — what the
	//	Stream* helpers name in an HdFleetException. Set on every response the
	//	client returns, including Invalid and pre-send Cancelled.
	internal string? Endpoint { get; set; }

	/// <summary>
	/// The request's errors, if any.
	/// </summary>
	public IReadOnlyList<string> Errors { get; internal set; } = [];

	/// <summary>
	/// Whether the response was served from the fresh-poll cache instead of a live request.
	/// </summary>
	public bool FromCache { get; internal set; }

	/// <summary>
	/// What the client observed about the HTTP exchange behind this response. Set on every
	/// response produced after a request was sent — success, HTTP failure, timeout, transport
	/// failure or mid-flight cancellation; <c>null</c> only when the request was never sent
	/// (<see cref="ResponseStatus.Invalid"/>, or <see cref="ResponseStatus.Cancelled"/> before
	/// sending).
	/// </summary>
	public ResponseMetadata? Metadata { get; internal set; }

	/// <summary>
	/// The request id echoed by HD Fleet (<c>X-Request-ID</c>, or <c>error.requestId</c>), if any.
	/// </summary>
	public string? RequestId { get; internal set; }

	/// <summary>
	/// The request's outcome classification.
	/// </summary>
	public ResponseStatus Status { get; internal set; } = ResponseStatus.Succeeded;

	/// <summary>
	/// The request's status.
	/// </summary>
	public bool Success => Errors.Count == 0;

	internal TResponse WithMetadata(
		ResponseMetadata metadata) {
		Metadata = metadata;

		return (TResponse)this;
	}

	//	A gate cache hit must not mutate the stored instance — the fresh caller
	//	holds the same reference and its FromCache would retroactively flip.
	//	A shallow copy is safe: the payload and Errors are read-only.
	internal TResponse WithFromCache() {
		var clone = (TResponse)MemberwiseClone();

		clone.FromCache = true;
		clone.Metadata = Metadata?.WithFromCache();

		return clone;
	}

	//	============================================================================
	//	Responses
	//	============================================================================

	internal static TResponse Cancelled => new() {
		Errors = [
			"The request was cancelled."
		],
		Status = ResponseStatus.Cancelled
	};
	internal static TResponse Failed => new() {
		Errors = [
			"The request has failed."
		],
		Status = ResponseStatus.Failed
	};
	internal static TResponse FailedWith(
		ResponseStatus status,
		string detail,
		string? requestId) => new() {
			Errors = [
				"The request has failed.",
				detail
			],
			RequestId = requestId,
			Status = status
		};
	internal static TResponse Invalid(
		ValidationResult validationResult) => new() {
			Errors = validationResult.ToErrors(),
			Status = ResponseStatus.Invalid
		};
}
