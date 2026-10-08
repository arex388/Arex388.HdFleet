using System.Globalization;
using System.Text;

namespace Arex388.HdFleet;

/// <summary>
/// Thrown by the <c>Stream*</c> helpers in <see cref="HdFleetClientExtensions"/> when a page
/// request fails. Items from earlier pages were already yielded; nothing is retried.
/// </summary>
/// <remarks>
/// Caller cancellation is not reported through this type — the helpers throw
/// <see cref="OperationCanceledException"/> instead. The raw API key never appears in
/// <see cref="Exception.Message"/>, <see cref="Errors"/> or <see cref="Metadata"/>.
/// </remarks>
public sealed class HdFleetException :
	Exception {
	//	hh:mm:ss.f, with a d. prefix only once the exchange ran a day or more.
	private const string DaysElapsedFormat = @"d\.hh\:mm\:ss\.f";
	private const string ElapsedFormat = @"hh\:mm\:ss\.f";

	internal HdFleetException(
		string endpoint,
		ResponseStatus status,
		IReadOnlyList<string> errors,
		string? requestId,
		ResponseMetadata? metadata) :
		base(GetMessage(endpoint, status, errors, metadata)) {
		Endpoint = endpoint;
		Errors = errors;
		Metadata = metadata;
		RequestId = requestId;
		Status = status;
	}

	/// <summary>
	/// The failed request's relative path, e.g. <c>locations/latest?limit=200</c>.
	/// </summary>
	public string Endpoint { get; }

	/// <summary>
	/// The failed page's errors.
	/// </summary>
	public IReadOnlyList<string> Errors { get; }

	/// <summary>
	/// The failed page's response metadata; <c>null</c> when the page was never sent (for
	/// example, it failed client-side validation).
	/// </summary>
	public ResponseMetadata? Metadata { get; }

	/// <summary>
	/// The request id echoed by HD Fleet (<c>X-Request-ID</c>, or <c>error.requestId</c>), if any.
	/// </summary>
	public string? RequestId { get; }

	/// <summary>
	/// The failed page's outcome classification.
	/// </summary>
	public ResponseStatus Status { get; }

	//	============================================================================
	//	Utilities
	//	============================================================================

	//	"{Endpoint} returned {Status} (HTTP {code}) after {elapsed}: {errors}" —
	//	the status code and elapsed time only when a send was attempted. Every
	//	part is either library text or already-redacted wire text.
	private static string GetMessage(
		string endpoint,
		ResponseStatus status,
		IReadOnlyList<string> errors,
		ResponseMetadata? metadata) {
		var builder = new StringBuilder(endpoint.Length + 128);

		builder.Append(endpoint)
			   .Append(" returned ")
			   .Append(status.ToStringFast());

		if (metadata?.StatusCode is { } statusCode) {
			builder.Append(" (HTTP ")
				   .Append(((int)statusCode).ToString(CultureInfo.InvariantCulture))
				   .Append(')');
		}

		if (metadata is not null) {
			builder.Append(" after ")
				   .Append(metadata.Elapsed.ToString(metadata.Elapsed.Days > 0 ? DaysElapsedFormat : ElapsedFormat, CultureInfo.InvariantCulture));
		}

		if (errors.Count > 0) {
			builder.Append(": ")
				   .Append(string.Join(" ", errors));
		}

		return builder.ToString();
	}
}
