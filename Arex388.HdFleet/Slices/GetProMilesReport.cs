using FluentValidation;
using System.Globalization;
using System.Net;
using System.Text;
using static Arex388.HdFleet.GetProMilesReport;

namespace Arex388.HdFleet;

/// <summary>
/// GetProMilesReport request and response containers.
/// </summary>
public static class GetProMilesReport {
	/// <summary>
	/// GetProMilesReport request container.
	/// </summary>
	/// <remarks>
	/// Uniquely in this API, <see cref="FromDate"/>/<see cref="ToDate"/> are plain
	/// calendar dates on the wire (<c>yyyy-MM-dd</c>), not timestamps — the time
	/// component of the <see cref="DateTime"/> values is ignored.
	/// </remarks>
	public sealed class Request :
		RequestBase {
		/// <summary>
		/// The pagination cursor from the previous page, if any.
		/// </summary>
		public string? Cursor { get; init; }

		internal override string Endpoint => GetEndpoint(this);

		/// <summary>
		/// The report's start date. The time component is ignored.
		/// </summary>
		public DateTime FromDate { get; init; }

		/// <summary>
		/// The number of results to return. <c>200</c> by default, also the maximum.
		/// </summary>
		public int Limit { get; init; } = 200;

		/// <summary>
		/// The report's end date. The time component is ignored.
		/// </summary>
		public DateTime ToDate { get; init; }

		/// <summary>
		/// The vehicle's ProMiles name (the wire's <c>vName</c>). Required.
		/// </summary>
		public string VehicleName { get; init; } = null!;

		//  ========================================================================
		//  Utilities
		//  ========================================================================

		private static string GetEndpoint(
			Request request) {
			var sb = new StringBuilder("reports/promiles?");

			sb.Append("vName=").Append(WebUtility.UrlEncode(request.VehicleName));
			sb.Append("&fromDate=").Append(request.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
			sb.Append("&toDate=").Append(request.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
			sb.Append("&limit=").Append(request.Limit);

			if (request.Cursor.HasValue()) {
				sb.Append("&cursor=").Append(WebUtility.UrlEncode(request.Cursor));
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// GetProMilesReport response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The cursor for the next page, if any.
		/// </summary>
		public string? NextCursor { get; init; }

		/// <summary>
		/// The report's rows.
		/// </summary>
		public IReadOnlyList<ProMilesReportRow> Rows { get; init; } = [];
	}
}

//	================================================================================
//	Validators
//	================================================================================

file sealed class RequestValidator :
	AbstractValidator<Request> {
	public RequestValidator() {
		RuleFor(r => r.Limit).InclusiveBetween(1, 200);
		RuleFor(r => r.ToDate).GreaterThanOrEqualTo(r => r.FromDate);
		RuleFor(r => r.VehicleName).NotEmpty();
	}
}
