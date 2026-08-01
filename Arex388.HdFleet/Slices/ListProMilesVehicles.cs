using FluentValidation;
using System.Net;
using System.Text;
using static Arex388.HdFleet.ListProMilesVehicles;

namespace Arex388.HdFleet;

/// <summary>
/// ListProMilesVehicles request and response containers.
/// </summary>
public static class ListProMilesVehicles {
	/// <summary>
	/// ListProMilesVehicles request container.
	/// </summary>
	public sealed class Request :
		RequestBase {
		/// <summary>
		/// The pagination cursor from the previous page, if any.
		/// </summary>
		public string? Cursor { get; init; }

		/// <summary>
		/// The ProMiles customer's id. Required.
		/// </summary>
		public int CustomerId { get; init; }

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
			var sb = new StringBuilder("reports/promiles/vehicles?");

			sb.Append("customerId=").Append(request.CustomerId);
			sb.Append("&limit=").Append(request.Limit);

			if (request.Cursor.HasValue()) {
				sb.Append("&cursor=").Append(WebUtility.UrlEncode(request.Cursor));
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// ListProMilesVehicles response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The cursor for the next page, if any.
		/// </summary>
		public string? NextCursor { get; init; }

		/// <summary>
		/// The matched vehicles.
		/// </summary>
		public IReadOnlyList<ProMilesVehicle> Vehicles { get; init; } = [];
	}
}

//	================================================================================
//	Validators
//	================================================================================

file sealed class RequestValidator :
	AbstractValidator<Request> {
	public RequestValidator() {
		RuleFor(r => r.CustomerId).GreaterThan(0);
		RuleFor(r => r.Limit).InclusiveBetween(1, 200);
	}
}
