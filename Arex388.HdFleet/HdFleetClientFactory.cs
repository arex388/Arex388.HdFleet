using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Arex388.HdFleet;

internal sealed class HdFleetClientFactory(
	IServiceProvider services,
	IMemoryCache cache) :
	IHdFleetClientFactory {
	private static readonly MemoryCacheEntryOptions _memoryCacheEntryOptions = new() {
		SlidingExpiration = TimeSpan.FromHours(1),
		AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
	};

	private readonly IServiceProvider _services = services;
	private readonly IMemoryCache _cache = cache;
	private readonly object _missLock = new();

	/// <inheritdoc />
	public IHdFleetClient CreateClient(
		HdFleetClientOptions options) {
		var key = $"{nameof(Arex388)}.{nameof(HdFleet)}.Key[{options.Key}]";

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
