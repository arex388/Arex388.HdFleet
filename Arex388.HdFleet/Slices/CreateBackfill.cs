using FluentValidation;
using System.Net.Http.Headers;
using System.Text.Json;
using static Arex388.HdFleet.CreateBackfill;

namespace Arex388.HdFleet;

/// <summary>
/// CreateBackfill request and response containers.
/// </summary>
public static class CreateBackfill {
	/// <summary>
	/// CreateBackfill request container. The API's only write — a POST.
	/// </summary>
	/// <remarks>
	/// One job covers up to 7 days. Near-real-time windows are rejected (the
	/// minimum age for <see cref="To"/> is undocumented). An identical ready job
	/// is reused (<c>Reused</c> is <c>true</c> on the returned job); a window
	/// overlapping an existing ready job is rejected with <c>409</c>
	/// (<c>Status = Conflict</c>) — reconcile against existing jobs. The POST is
	/// never retried by the client — re-issue a failed create yourself; job
	/// reuse makes accidental duplicates harmless.
	/// </remarks>
	public sealed class Request :
		RequestBase {
		internal override string Endpoint => "backfills";

		/// <summary>
		/// The window's start timestamp.
		/// </summary>
		public DateTimeOffset From { get; init; }

		internal override HttpMethod Method => HttpMethod.Post;

		/// <summary>
		/// The reason for the backfill, e.g. <c>"initial_sync"</c> — the only
		/// documented literal. Optional; the value set is undocumented.
		/// </summary>
		public string? Reason { get; init; }

		/// <summary>
		/// The slice length in minutes. <c>15</c> by default.
		/// </summary>
		public int SliceMinutes { get; init; } = 15;

		/// <summary>
		/// The stream to backfill. <c>"events"</c> — the only documented
		/// literal — by default.
		/// </summary>
		public string Stream { get; init; } = "events";

		/// <summary>
		/// The window's end timestamp. No more than 7 days after <see cref="From"/>.
		/// </summary>
		public DateTimeOffset To { get; init; }

		/// <summary>
		/// The vehicle to scope the backfill to, if any.
		/// </summary>
		public ExternalVehicleId? VehicleId { get; init; }

		//  ========================================================================
		//  Utilities
		//  ========================================================================

		//	The one write body in the API, hand-rolled with Utf8JsonWriter —
		//	the converters only read, by design.
		internal override HttpContent GetContent() {
			using var stream = new MemoryStream();

			using (var writer = new Utf8JsonWriter(stream)) {
				writer.WriteStartObject();
				writer.WriteString("stream"u8, Stream);
				writer.WriteString("from"u8, From);
				writer.WriteString("to"u8, To);
				writer.WriteNumber("sliceMinutes"u8, SliceMinutes);

				if (VehicleId is not null) {
					writer.WriteString("externalVehicleId"u8, VehicleId.Value.ToString());
				}

				if (Reason.HasValue()) {
					writer.WriteString("reason"u8, Reason);
				}

				writer.WriteEndObject();
			}

			var content = new ByteArrayContent(stream.ToArray());

			content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

			return content;
		}
	}

	/// <summary>
	/// CreateBackfill response container. A <c>202</c> means the job was
	/// accepted — poll <see cref="IHdFleetClient.GetBackfillAsync(BackfillJobId, CancellationToken)"/>
	/// until its status is <c>"ready"</c>; a <c>200</c> with <c>Reused</c> means
	/// an identical ready job already existed.
	/// </summary>
	public sealed class Response :
		ResponseBase<Response> {
		/// <summary>
		/// The created (or reused) backfill job.
		/// </summary>
		public BackfillJob? Job { get; init; }
	}
}

//	================================================================================
//	Validators
//	================================================================================

file sealed class RequestValidator :
	AbstractValidator<Request> {
	public RequestValidator() {
		RuleFor(r => r.SliceMinutes).GreaterThan(0);
		RuleFor(r => r.Stream).NotEmpty();
		RuleFor(r => r.To).GreaterThan(r => r.From);
		RuleFor(r => r).Must(r => r.To - r.From <= TimeSpan.FromDays(7))
			.WithMessage("HD Fleet limits a backfill job to a window of 7 days or less.");
	}
}
