using Arex388.HdFleet.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Arex388.HdFleet.Benchmarks;

/// <summary>
/// Creates a service provider configured for benchmarking with mocked HTTP responses.
/// </summary>
internal static class BenchmarkServiceProvider {
	public static IServiceProvider Create() {
		var services = new ServiceCollection();

		//  The unroutable base address is a fail-safe: if the mock handler wiring
		//  ever regresses, benchmarks fail instantly instead of draining live quota.
		services.AddHdFleet()
				.AddHttpClient(nameof(IHdFleetClient), hc => hc.BaseAddress = new Uri("https://localhost:9/api/external/v1/"))
				.ConfigurePrimaryHttpMessageHandler(() => new MockHttpMessageHandler());

		return services.BuildServiceProvider();
	}

	public static IHdFleetClient CreateClient(
		IServiceProvider services) {
		var factory = services.GetRequiredService<IHdFleetClientFactory>();

		return factory.CreateClient(new HdFleetClientOptions {
			Key = "benchmark-mock-key"
		});
	}
}
