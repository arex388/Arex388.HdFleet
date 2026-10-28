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

				return response;
			} finally {
				//	Store and release in one step, so a new caller can never
				//	miss both the entry and the cached response. A failed
				//	response is not cached, so the next caller sends afresh.
				lock (_lock) {
					if (response?.Success == true) {
						//	Set echoes the value it stored; only the insertion
						//	is wanted.
						_ = _cache.Set(cacheKey, response, GetTtl(floor));
					}

					//	The entry is this leader's own, registered above.
					_ = _inFlight.Remove(cacheKey);
				}

				//	The pipeline never throws; if it did, the joiners are
				//	released as cancelled rather than left waiting. The source
				//	is completed here only, so the result is never false.
				_ = response is null
					? leader.TrySetCanceled(CancellationToken.None)
					: leader.TrySetResult(response);
			}
		}
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
