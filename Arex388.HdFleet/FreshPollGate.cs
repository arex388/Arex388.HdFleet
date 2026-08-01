using Microsoft.Extensions.Caching.Memory;

namespace Arex388.HdFleet;

//	The fresh-poll gate: HD Fleet's cache TTL and rate limit are the same
//	number, so one IMemoryCache-backed mechanism satisfies both — cache the
//	successful response with TTL equal to the floor, and floor-driven 429s
//	become structurally impossible. The pipeline checks before sending (inside
//	the floor → the cached response, marked FromCache) and stores after a
//	successful fresh response. Cursor continuations never touch the gate.
internal interface IFreshPollGate {
	TResponse? GetCached<TResponse>(
		string key,
		string endpoint)
		where TResponse : class;

	void Store<TResponse>(
		string key,
		string endpoint,
		TResponse response,
		TimeSpan floor)
		where TResponse : class;
}

internal sealed class FreshPollGate(
	IMemoryCache cache) :
	IFreshPollGate {
	private readonly IMemoryCache _cache = cache;

	public TResponse? GetCached<TResponse>(
		string key,
		string endpoint)
		where TResponse : class => _cache.TryGetValue(GetCacheKey(key, endpoint), out var cached)
		? cached as TResponse
		: null;

	public void Store<TResponse>(
		string key,
		string endpoint,
		TResponse response,
		TimeSpan floor)
		where TResponse : class => _cache.Set(
		GetCacheKey(key, endpoint), response, GetTtl(floor));

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
}
