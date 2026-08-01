using System.Text;

namespace Arex388.HdFleet;

/// <summary>
/// GetBackfillEventsPage request and response containers — fetches one page of
/// an events backfill slice by its opaque endpoint URL.
/// </summary>
public static class GetBackfillEventsPage {
	/// <summary>
	/// GetBackfillEventsPage request container.
	/// </summary>
	/// <remarks>
	/// Slice endpoints come from
	/// <see cref="IHdFleetClient.ListBackfillSlicesAsync(ListBackfillSlices.Request, CancellationToken)"/>
	/// and are opaque — consume them as given; follow <c>NextCursor</c> inside
	/// the slice until null before moving to the next sequence.
	/// </remarks>
	public sealed class Request :
		RequestBase {
		/// <summary>
		/// The pagination cursor from the previous page of this slice, if any.
		/// </summary>
		public string? Cursor { get; init; }

		internal override string Endpoint => GetEndpoint(this);

		/// <summary>
		/// The slice's endpoint URL, exactly as returned by ListBackfillSlices.
		/// </summary>
		public required string SliceEndpoint { get; init; }

		//  ========================================================================
		//  Utilities
		//  ========================================================================

		//	The slice endpoint is opaque — it may or may not already carry a
		//	query string, so the cursor separator is chosen defensively.
		private static string GetEndpoint(
			Request request) {
			if (!request.Cursor.HasValue()) {
				return request.SliceEndpoint;
			}

			var sb = new StringBuilder(request.SliceEndpoint);

			sb.Append(request.SliceEndpoint.IndexOf('?') >= 0 ? '&' : '?');
			sb.Append("cursor=").Append(System.Net.WebUtility.UrlEncode(request.Cursor));

			return sb.ToString();
		}
	}

	/// <summary>
	/// GetBackfillEventsPage response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The page's events.
		/// </summary>
		public IReadOnlyList<Event> Events { get; init; } = [];

		/// <summary>
		/// The cursor for the next page within this slice, if any.
		/// </summary>
		public string? NextCursor { get; init; }
	}
}
