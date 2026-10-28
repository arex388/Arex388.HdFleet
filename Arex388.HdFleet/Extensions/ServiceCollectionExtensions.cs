using Arex388.HdFleet;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
			AddFreshPollGate(services);

			return services.AddMemoryCache()
						   .AddValidatorsFromAssemblyContaining<IHdFleetClient>(includeInternalTypes: true, lifetime: ServiceLifetime.Singleton)
						   .AddSingleton<IHdFleetClientFactory, HdFleetClientFactory>();
		}

		/// <summary>
		/// Add the HD Fleet API client for interacting with a single account.
		/// </summary>
		/// <param name="options">The client's configuration options.</param>
		/// <returns>The services collection.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><see cref="HdFleetClientOptions.Timeout"/> is zero, negative, or longer than <see cref="int.MaxValue"/> milliseconds.</exception>
		public IServiceCollection AddHdFleet(HdFleetClientOptions options) {
			HdFleetClientOptions.ThrowIfInvalid(options);
			AddHdFleetHttpClient(services);
			AddFreshPollGate(services);

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

	//  One gate per container, so every client on a key shares its in-flight
	//  requests; the factory keeps one client per key and timeout, and two
	//  clients on one key would otherwise coalesce nothing between them.
	private static void AddFreshPollGate(
		IServiceCollection services) => services.TryAddSingleton<IFreshPollGate, FreshPollGate>();

	//  HttpClient.Timeout is disabled: the client applies
	//  HdFleetClientOptions.Timeout per request through a linked token, so
	//  there is one deadline, one detection path (TimedOut), and a configured
	//  timeout above the BCL's 100 seconds is honoured.
	private static void AddHdFleetHttpClient(
		IServiceCollection services) => services.AddHttpClient(nameof(IHdFleetClient), hc => {
			hc.BaseAddress = _baseAddress;
			hc.Timeout = Timeout.InfiniteTimeSpan;
		}).AddHttpMessageHandler(static () => new HdFleetRetryHandler());
}
