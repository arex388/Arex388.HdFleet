using Microsoft.Extensions.Caching.Memory;

namespace Arex388.HdFleet;

//	The fresh-poll gate: HD Fleet's cache TTL and rate limit are the same
//	number, so one IMemoryCache-backed mechanism satisfies both — cache the
//	successful response with TTL equal to the floor, and floor-driven 429s
//	become structurally impossible. Every fresh (cursor-less) call goes through
//	the gate: inside the floor → the cached response, marked FromCache;
//	otherwise the first caller sends (the leader) and every caller that arrives
//	while that request is on the wire awaits it (a joiner) and receives the
//	same response, marked FromCache too. One gate per container, shared by
//	every client, so two clients on one key coalesce as one client's callers
//	do. Cursor continuations never touch the gate.
internal interface IFreshPollGate {
	ValueTask<TResponse> SendAsync<TResponse>(
		string key,
		string endpoint,
		TimeSpan floor,
		Func<Task<TResponse>> send,
		CancellationToken cancellationToken)
		where TResponse : ResponseBase<TResponse>, new();
}

internal sealed class FreshPollGate(
	IMemoryCache cache) :
	IFreshPollGate {
	private readonly IMemoryCache _cache = cache;
	private readonly Dictionary<string, Task> _inFlight = new(StringComparer.Ordinal);
	private readonly object _lock = new();

	//	A ValueTask: inside the floor the cache hit completes synchronously, and
	//	repeated calls inside the floor are the expected shape of a poll.
	public async ValueTask<TResponse> SendAsync<TResponse>(
		string key,
		string endpoint,
		TimeSpan floor,
		Func<Task<TResponse>> send,
		CancellationToken cancellationToken)
		where TResponse : ResponseBase<TResponse>, new() {
		var cacheKey = GetCacheKey(key, endpoint);

		while (true) {
			TaskCompletionSource<TResponse>? leader = null;
			Task<TResponse> pending;

			//	The cache check and the in-flight registration share one lock,
			//	so two callers can never both miss the cache and both send. An
			//	endpoint has one response type, so the in-flight task for a key
			//	is always a Task<TResponse>.
			lock (_lock) {
				if (_cache.TryGetValue(cacheKey, out var cached)
					&& cached is TResponse hit) {
					return hit.WithFromCache();
				}

				if (_inFlight.TryGetValue(cacheKey, out var inFlight)
					&& inFlight is Task<TResponse> typed) {
					pending = typed;
				} else {
					leader = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
					pending = leader.Task;

					_inFlight[cacheKey] = pending;
				}
			}

			if (leader is null) {
				var joined = await JoinAsync(pending, cancellationToken).ConfigureAwait(false);

				if (joined.Status != ResponseStatus.Cancelled) {
					return joined.WithFromCache();
				}

				if (cancellationToken.IsCancellationRequested) {
					return ResponseBase<TResponse>.Cancelled;
				}

				//	The leader's own token cancelled its request, not this
				//	caller's, so the outcome is not this caller's: go round
				//	again and send, or join whoever got there first.
				continue;
			}

			TResponse? response = null;

			try {
				response = await send().ConfigureAwait(false);
			} finally {
				//	The pipeline never throws; if it did, the joiners are
				//	released as cancelled rather than left waiting, and the
				//	exception keeps propagating past the value Release returns.
				response = Release(cacheKey, floor, response, leader);
			}

			return response;
		}
	}

	//	The leader's cleanup. Store and release in one step, so a new caller can
	//	never miss both the entry and the cached response; a failed response is
	//	not cached, so the next caller sends afresh. The removal and the
	//	completion run whatever the store does: a cache that rejects the entry
	//	(a consumer's size-limited MemoryCache, whose entries must carry a Size
	//	the gate does not invent) is the consumer's configuration, reported to
	//	the leader and its joiners as Failed rather than thrown past the
	//	never-throw contract, and never a stranded entry every later caller
	//	joins until its own deadline fires.
	private TResponse Release<TResponse>(
		string cacheKey,
		TimeSpan floor,
		TResponse? response,
		TaskCompletionSource<TResponse> leader)
		where TResponse : ResponseBase<TResponse>, new() {
		lock (_lock) {
			try {
				if (response?.Success == true) {
					//	Set echoes the value it stored; only the insertion
					//	is wanted.
					_ = _cache.Set(cacheKey, response, GetTtl(floor));
				}
			} catch (Exception e) {
				response = GetStoreFailed(response, e);
			} finally {
				//	The entry is this leader's own, registered by SendAsync.
				_ = _inFlight.Remove(cacheKey);
			}
		}

		//	The source is completed here only, so the result is never false.
		_ = response is null
			? leader.TrySetCanceled(CancellationToken.None)
			: leader.TrySetResult(response);

		//	A null response means the pipeline threw, and SendAsync's finally
		//	lets that exception propagate, so the Cancelled value is never
		//	returned to a caller.
		return response ?? ResponseBase<TResponse>.Cancelled;
	}

	//	========================================================================
	//	Utilities
	//	========================================================================

	//	The raw key is acceptable in an in-memory cache key — it never leaves
	//	the process. Anything that does leave uses KeyFingerprint instead. The
	//	endpoint is the complete relative URL of a fresh (cursor-less) call, so
	//	it already carries the route and the normalized query — for windowed
	//	endpoints the from/to window is in the key, which is what gates them
	//	per new query.
	private static string GetCacheKey(
		string key,
		string endpoint) => string.Concat("Arex388.HdFleet.Response[", key, "][", endpoint, "]");

	//	A ~5% safety margin over the documented floor so clock skew between
	//	this host and HD Fleet's never costs a 429.
	private static TimeSpan GetTtl(
		TimeSpan floor) => TimeSpan.FromTicks(floor.Ticks + (floor.Ticks / 20));

	//	The store failed after a successful exchange, so the exchange's own
	//	metadata and request id stay on the Failed response. The detail carries
	//	the cache's exception as it is: MemoryCache's message names its own
	//	configuration, not the entry's key, and the gate has no redaction of
	//	its own to apply.
	private static TResponse GetStoreFailed<TResponse>(
		TResponse? response,
		Exception exception)
		where TResponse : ResponseBase<TResponse>, new() {
		var failed = ResponseBase<TResponse>.FailedWith(ResponseStatus.Failed, "The fresh-poll cache rejected the response: " + exception.GetType().Name + ": " + exception.Message, response?.RequestId);

		return response?.Metadata is null
			? failed
			: failed.WithMetadata(response.Metadata);
	}

	//	A joiner's wait for the leader's response, under the joiner's own token
	//	only: cancelling it returns Cancelled to this caller and leaves the
	//	leader's request running for the others.
	private static async Task<TResponse> JoinAsync<TResponse>(
		Task<TResponse> pending,
		CancellationToken cancellationToken)
		where TResponse : ResponseBase<TResponse>, new() {
		if (!pending.IsCompleted
			&& cancellationToken.CanBeCanceled) {
			var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			//	A token that fires twice signals once; the second result is
			//	irrelevant.
			using (cancellationToken.Register(static state => _ = (state as TaskCompletionSource<bool>)?.TrySetResult(true), cancelled)) {
				var winner = await Task.WhenAny(pending, cancelled.Task).ConfigureAwait(false);

				if (winner != pending) {
					return ResponseBase<TResponse>.Cancelled;
				}
			}
		}

		return await pending.ConfigureAwait(false);
	}
}
