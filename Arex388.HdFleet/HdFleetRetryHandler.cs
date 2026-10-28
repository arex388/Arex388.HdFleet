using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

namespace Arex388.HdFleet;

//	Transparent retry for transient failures on the named client. The
//	fresh-poll gate prevents self-inflicted floor violations, but 429 can
//	still arrive from quota exhaustion or HD Fleet's global source-IP abuse
//	protection, and 500/503 from their infrastructure (503 is documented as
//	"centralized quota store unavailable" — likely transient). The handler
//	holds no key — per-request auth flows through it untouched, so handler
//	pooling stays safe across accounts.
internal sealed class HdFleetRetryHandler :
	DelegatingHandler {
	//	The HttpRequestMessage.Properties key under which the client stamps the
	//	request's deadline, a long Stopwatch timestamp.
	internal const string DeadlineKey = "Arex388.HdFleet.Deadline";

	private static readonly int[] _backoffSeconds = [1, 4, 12];

	//	Random.Shared is .NET 6+ — a [ThreadStatic] instance avoids both the
	//	lock and the correlated-seed problem on netstandard2.0.
	[ThreadStatic]
	private static Random? _random;

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken) {
		for (var attempt = 0; ; attempt++) {
			var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

			//	Retry GETs only: HttpRequestMessage cannot always be resent
			//	once it has content, and a retried backfill POST can
			//	legitimately 409 on overlap — the caller re-issues those.
			if (request.Method != HttpMethod.Get
				|| !IsRetryable(response.StatusCode)
				|| attempt >= _backoffSeconds.Length) {
				return response;
			}

			//	Honor Retry-After when present; otherwise the backoff table.
			//	Jitter is added either way — the API docs ask for it.
			var delay = response.Headers.RetryAfter?.Delta
				?? TimeSpan.FromSeconds(_backoffSeconds[attempt]);

			delay += GetJitter();

			//	A wait that cannot finish before the request's deadline is not
			//	taken: the response is returned as it is, so the caller sees the
			//	API's own 429/500/503 instead of a TimedOut after a long sleep.
			if (!FitsDeadline(request, delay)) {
				return response;
			}

			response.Dispose();

			await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
		}
	}

	//  ============================================================================
	//  Utilities
	//  ============================================================================

	//	The client stamps each request with its deadline as a Stopwatch
	//	timestamp (a monotonic reading, since the pooled handler has no clock
	//	to inject). A request with no stamp, or one of another type, has no
	//	deadline to respect.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool FitsDeadline(
		HttpRequestMessage request,
		TimeSpan delay) {
		if (!request.Properties.TryGetValue(DeadlineKey, out var stamp)
			|| stamp is not long deadline) {
			return true;
		}

		var delayTicks = (long)(delay.TotalSeconds * Stopwatch.Frequency);

		return Stopwatch.GetTimestamp() + delayTicks < deadline;
	}

	private static TimeSpan GetJitter() {
		_random ??= new Random();

		return TimeSpan.FromMilliseconds(_random.Next(0, 1000));
	}

	//	429 is not a named HttpStatusCode member on netstandard2.0 — compare
	//	numerically. Never retry 400/401/403/404/409/410.
	private static bool IsRetryable(
		HttpStatusCode statusCode) => (int)statusCode is 429 or 500 or 503;
}
