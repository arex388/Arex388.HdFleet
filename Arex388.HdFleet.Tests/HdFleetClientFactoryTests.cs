using Arex388.HdFleet.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Arex388.HdFleet.Tests;

public sealed class HdFleetClientFactoryTests {
	[Fact]
	public void CreateClient_SameKey_ReturnsSameInstance() {
		using var provider = CreateProvider();

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();

		var a = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1"
		});
		var b = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1"
		});

		a.Should().BeSameAs(b);
	}

	[Fact]
	public void CreateClient_DifferentKeys_ReturnDifferentInstances() {
		using var provider = CreateProvider();

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();

		var a = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1"
		});
		var b = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-2"
		});

		a.Should().NotBeSameAs(b);
	}

	private static ServiceProvider CreateProvider() {
		var services = new ServiceCollection();

		services.AddHdFleet()
				.AddHttpClient(nameof(IHdFleetClient), hc => hc.BaseAddress = new Uri("https://localhost:9/api/external/v1/"))
				.ConfigurePrimaryHttpMessageHandler(() => new MockHttpMessageHandler());

		return services.BuildServiceProvider();
	}
}
