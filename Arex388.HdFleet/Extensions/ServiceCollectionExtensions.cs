using Arex388.HdFleet;
using FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extensions.
/// </summary>
public static class ServiceCollectionExtensions {
	private static readonly Uri _baseAddress = new("https://integrations.hdfleet.net/api/external/v1/");

	/// <param name="services">The services collection.</param>
	extension(
		IServiceCollection services) {
		/// <summary>
		/// Add the HD Fleet API client factory for interacting with multiple accounts.
		/// </summary>
		/// <returns>The services collection.</returns>
		public IServiceCollection AddHdFleet() {
			AddHdFleetHttpClient(services);

			return services.AddMemoryCache()
						   .AddValidatorsFromAssemblyContaining<IHdFleetClient>(includeInternalTypes: true, lifetime: ServiceLifetime.Singleton)
						   .AddSingleton<IHdFleetClientFactory, HdFleetClientFactory>();
		}

		/// <summary>
		/// Add the HD Fleet API client for interacting with a single account.
		/// </summary>
		/// <param name="options">The client's configuration options.</param>
		/// <returns>The services collection.</returns>
		public IServiceCollection AddHdFleet(HdFleetClientOptions options) {
			AddHdFleetHttpClient(services);

			//  Deviation from Carmine: the single-account overload also
			//  registers the memory cache — the fresh-poll gate needs
			//  IMemoryCache in both modes.
			return services.AddMemoryCache()
						   .AddValidatorsFromAssemblyContaining<IHdFleetClient>(includeInternalTypes: true, lifetime: ServiceLifetime.Singleton)
						   .AddSingleton(options)
						   .AddSingleton<IHdFleetClient>(
							   sp => new HdFleetClient(sp));
		}
	}

	//  ============================================================================
	//  Utilities
	//  ============================================================================

	private static void AddHdFleetHttpClient(
		IServiceCollection services) => services.AddHttpClient(nameof(IHdFleetClient), hc => {
			hc.BaseAddress = _baseAddress;
		});
}
