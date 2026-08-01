using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Arex388.HdFleet.Tests.Integration;

/// <summary>
/// Live smoke tests. Each run spends real quota and consumes the 15-minute
/// fresh-poll floor — never loop these.
/// </summary>
public sealed class VehiclesTests {
	[LiveFact]
	public async Task ListVehicles_Succeeds() {
		var client = CreateClient();

		var response = await client.ListVehiclesAsync(TestContext.Current.CancellationToken);

		response.Success.Should().BeTrue(string.Join("; ", response.Errors));
	}

	private static IHdFleetClient CreateClient() {
		var services = new ServiceCollection();

		services.AddHdFleet(new HdFleetClientOptions {
			Key = Config.Key
		});

		return services.BuildServiceProvider().GetRequiredService<IHdFleetClient>();
	}
}
