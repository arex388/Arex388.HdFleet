using FluentValidation;
using System.Net;
using System.Text;
using static Arex388.HdFleet.ListProMilesCustomers;

namespace Arex388.HdFleet;

/// <summary>
/// ListProMilesCustomers request and response containers.
/// </summary>
public static class ListProMilesCustomers {
	/// <summary>
	/// ListProMilesCustomers request container.
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
			var sb = new StringBuilder("reports/promiles/customers?");

			sb.Append("limit=").Append(request.Limit);

			if (request.Cursor.HasValue()) {
				sb.Append("&cursor=").Append(WebUtility.UrlEncode(request.Cursor));
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// ListProMilesCustomers response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The matched customers.
		/// </summary>
		public IReadOnlyList<ProMilesCustomer> Customers { get; init; } = [];

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
