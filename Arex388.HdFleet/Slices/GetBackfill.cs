namespace Arex388.HdFleet;

/// <summary>
/// GetBackfill request and response containers.
/// </summary>
public static class GetBackfill {
	/// <summary>
	/// GetBackfill request container. Poll until the job's status is
	/// <c>"ready"</c>; a <c>410</c> (<c>Status = Gone</c>) means the job expired
	/// or is unavailable — recreate it.
	/// </summary>
	public sealed class Request :
		RequestBase {
		//  Explicit concatenation: on netstandard2.0 interpolation with a struct
		//  hole lowers to string.Format and boxes the id.
		internal override string Endpoint => "backfills/" + Id.ToString();

		/// <summary>
		/// The backfill job's id.
		/// </summary>
		public BackfillJobId Id { get; init; }
	}

	/// <summary>
	/// GetBackfill response container.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The matched backfill job.
		/// </summary>
		public BackfillJob? Job { get; init; }
	}
}
