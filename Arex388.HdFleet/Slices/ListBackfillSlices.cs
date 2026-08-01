using FluentValidation;
using System.Net;
using System.Text;
using static Arex388.HdFleet.ListBackfillSlices;

namespace Arex388.HdFleet;

/// <summary>
/// ListBackfillSlices request and response containers.
/// </summary>
public static class ListBackfillSlices {
	/// <summary>
	/// ListBackfillSlices request container.
	/// </summary>
	/// <remarks>
	/// Unlike every other cursor endpoint, this endpoint's cursor is typed as an
	/// integer on the wire — it still rides here as its string form, opaque
	/// either way.
	/// </remarks>
	public sealed class Request :
		RequestBase {
		/// <summary>
		/// The pagination cursor from the previous page, if any.
		/// </summary>
		public string? Cursor { get; init; }

		internal override string Endpoint => GetEndpoint(this);

		/// <summary>
		/// The backfill job's id.
		/// </summary>
		public BackfillJobId Id { get; init; }

		/// <summary>
		/// The number of results to return. <c>200</c> by default, also the maximum.
		/// </summary>
		public int Limit { get; init; } = 200;

		//  ========================================================================
		//  Utilities
		//  ========================================================================

		private static string GetEndpoint(
			Request request) {
			var sb = new StringBuilder("backfills/");

			sb.Append(request.Id.ToString()).Append("/slices?");
			sb.Append("limit=").Append(request.Limit);

			if (request.Cursor.HasValue()) {
				sb.Append("&cursor=").Append(WebUtility.UrlEncode(request.Cursor));
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// ListBackfillSlices response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The cursor for the next page, if any.
		/// </summary>
		public string? NextCursor { get; init; }

		/// <summary>
		/// The matched slices, in sequence order as returned.
		/// </summary>
		public IReadOnlyList<BackfillSlice> Slices { get; init; } = [];
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
