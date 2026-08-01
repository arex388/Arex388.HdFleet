using FluentValidation;
using System.Net;
using System.Text;
using static Arex388.HdFleet.ListIntegrations;

namespace Arex388.HdFleet;

/// <summary>
/// ListIntegrations request and response containers.
/// </summary>
public static class ListIntegrations {
	/// <summary>
	/// ListIntegrations request container.
	/// </summary>
	public sealed class Request :
		RequestBase {
		internal static readonly Request Default = new();

		/// <summary>
		/// The pagination cursor from the previous page, if any.
		/// </summary>
		public string? Cursor { get; init; }

		internal override string Endpoint => GetEndpoint(this);

		/// <summary>
		/// The number of results to return. <c>200</c> by default, also the maximum.
		/// </summary>
		public int Limit { get; init; } = 200;

		//  ========================================================================
		//  Utilities
		//  ========================================================================

		private static string GetEndpoint(
			Request request) {
			var sb = new StringBuilder("integrations?");

			sb.Append("limit=").Append(request.Limit);

			if (request.Cursor.HasValue()) {
				sb.Append("&cursor=").Append(WebUtility.UrlEncode(request.Cursor));
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// ListIntegrations response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The matched integrations.
		/// </summary>
		public IReadOnlyList<Integration> Integrations { get; init; } = [];

		/// <summary>
		/// The cursor for the next page, if any.
		/// </summary>
		public string? NextCursor { get; init; }
	}
}

//	================================================================================
//	Validators
//	================================================================================

file sealed class RequestValidator :
	AbstractValidator<Request> {
	public RequestValidator() {
		RuleFor(r => r.Limit).InclusiveBetween(1, 200);
	}
}
