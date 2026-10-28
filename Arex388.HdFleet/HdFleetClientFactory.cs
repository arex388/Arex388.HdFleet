using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace Arex388.HdFleet;

internal sealed class HdFleetClientFactory(
	IServiceProvider services,
	IMemoryCache cache) :
	IHdFleetClientFactory {
	private static readonly MemoryCacheEntryOptions _memoryCacheEntryOptions = new() {
		AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
		Size = 1,
		SlidingExpiration = TimeSpan.FromHours(1),
	};

	private readonly IServiceProvider _services = services;
	private readonly IMemoryCache _cache = cache;
	private readonly object _missLock = new();

	/// <inheritdoc />
	public IHdFleetClient CreateClient(
		HdFleetClientOptions options) {
		HdFleetClientOptions.ThrowIfInvalid(options);

		//  One client per account and effective timeout: a later caller with a
		//  different Timeout gets its own client rather than silently inheriting
		//  the first caller's. The fingerprint keeps the raw key out of the
		//  cache key.
		var key = string.Concat(
			"Arex388.HdFleet.Client[",
			options.Fingerprint,
			"][",
			options.EffectiveTimeout.Ticks.ToString(CultureInfo.InvariantCulture),
			"]");

		//  Fast path: cache hits skip the lock and entry-options work entirely.
		if (_cache.TryGetValue<Lazy<IHdFleetClient>>(key, out var cached)) {
			return cached!.Value;
		}

		//  GetOrCreate's value factory is not synchronized, so two racing
		//  first-touches could each publish their own Lazy and materialize
		//  different clients. Double-checked locking serializes entry creation
		//  (hit once per key per cache lifetime); the Lazy keeps client
		//  construction outside the lock.
		Lazy<IHdFleetClient> lazy;

		lock (_missLock) {
			if (!_cache.TryGetValue(key, out lazy!)) {
				lazy = new Lazy<IHdFleetClient>(() => {
					var httpClientFactory = _services.GetRequiredService<IHttpClientFactory>();
					var httpClient = httpClientFactory.CreateClient(nameof(IHdFleetClient));

					return new HdFleetClient(_services, httpClient, options);
				}, LazyThreadSafetyMode.ExecutionAndPublication);

				_cache.Set(key, lazy, _memoryCacheEntryOptions);
			}
		}

		return lazy.Value;
	}
}
