using FluentValidation;
using System.Globalization;
using System.Net;
using System.Text;
using static Arex388.HdFleet.ListEvents;

namespace Arex388.HdFleet;

/// <summary>
/// ListEvents request and response containers.
/// </summary>
public static class ListEvents {
	/// <summary>
	/// ListEvents request container.
	/// </summary>
	/// <remarks>
	/// The <see cref="From"/>/<see cref="To"/> window is required and must be 15
	/// minutes or less. Event media URLs may be existing HD Fleet blob URLs —
	/// treat them as opaque; do not parse, rewrite, or assume a lifetime.
	/// </remarks>
	public sealed class Request :
		RequestBase {
		/// <summary>
		/// The pagination cursor from the previous page, if any.
		/// </summary>
		public string? Cursor { get; init; }

		internal override string Endpoint => GetEndpoint(this);

		/// <summary>
		/// The window's start timestamp. Required.
		/// </summary>
		public DateTimeOffset From { get; init; }

		/// <summary>
		/// The number of results to return. <c>200</c> by default, also the maximum.
		/// </summary>
		public int Limit { get; init; } = 200;

		/// <summary>
		/// The window's end timestamp. Required, and no more than 15 minutes after
		/// <see cref="From"/>.
		/// </summary>
		public DateTimeOffset To { get; init; }

		//  ========================================================================
		//  Utilities
		//  ========================================================================

		private static string GetEndpoint(
			Request request) {
			var sb = new StringBuilder("events?");

			sb.Append("from=").Append(WebUtility.UrlEncode(request.From.ToString("o", CultureInfo.InvariantCulture)));
			sb.Append("&to=").Append(WebUtility.UrlEncode(request.To.ToString("o", CultureInfo.InvariantCulture)));
			sb.Append("&limit=").Append(request.Limit);

			if (request.Cursor.HasValue()) {
				sb.Append("&cursor=").Append(WebUtility.UrlEncode(request.Cursor));
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// ListEvents response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The matched events.
		/// </summary>
		public IReadOnlyList<Event> Events { get; init; } = [];

		/// <summary>
		/// The cursor for the next page, if any.
		/// </summary>
		public string? NextCursor { get; init; }
	}
}

//	================================================================================
//	Validators
//	================================================================================

//	An invalid window rejected server-side still burns a fresh-poll slot on a
//	guaranteed 400 — fail it before the request leaves the process.
file sealed class RequestValidator :
	AbstractValidator<Request> {
	public RequestValidator() {
		RuleFor(r => r.Limit).InclusiveBetween(1, 200);
		RuleFor(r => r.To).GreaterThan(r => r.From);
		RuleFor(r => r).Must(r => r.To - r.From <= TimeSpan.FromMinutes(15))
			.WithMessage("HD Fleet requires a time window of 15 minutes or less.");
	}
}
