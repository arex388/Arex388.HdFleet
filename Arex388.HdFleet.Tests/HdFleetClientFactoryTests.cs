using Arex388.HdFleet.Testing;
using Arex388.HdFleet.Tests.Unit;
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

	[Fact]
	public void CreateClient_SameKeyDifferentTimeouts_ReturnDifferentInstances() {
		using var provider = CreateProvider();

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();

		var a = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1"
		});
		var b = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1",
			Timeout = TimeSpan.FromSeconds(30)
		});
		var c = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1",
			Timeout = TimeSpan.FromSeconds(30)
		});

		a.Should().NotBeSameAs(b, "a later caller with a different timeout must not inherit the first caller's");
		b.Should().BeSameAs(c);
	}

	[Fact]
	public void CreateClient_NullAndDefaultTimeout_ReturnSameInstance() {
		using var provider = CreateProvider();

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();

		var a = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1"
		});
		var b = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1",
			Timeout = TimeSpan.FromSeconds(100)
		});

		a.Should().BeSameAs(b, "null and an explicit 100 seconds are the same effective timeout");
	}

	[Fact]
	public void CreateClient_SizeLimitedCache_ReturnsSameInstance() {
		using var provider = TestClients.CreateFactoryProvider(
			new MockHttpMessageHandler(),
			services => services.AddMemoryCache(o => o.SizeLimit = 2));

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();

		var a = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1"
		});
		var b = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1"
		});

		a.Should().NotBeNull();
		a.Should().BeSameAs(b, "the entry must be stored under a size-limited cache, not silently skipped");
	}

	[Fact]
	public void CreateClient_SizeLimitBelowClientCount_ReturnsClientForEveryCall() {
		using var provider = TestClients.CreateFactoryProvider(
			new MockHttpMessageHandler(),
			services => services.AddMemoryCache(o => o.SizeLimit = 1));

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();

		var clients = Enumerable.Range(1, 3).Select(i => factory.CreateClient(new HdFleetClientOptions {
			Key = $"key-{i}"
		})).ToList();

		clients.Should().HaveCount(3).And.NotContainNulls();
		clients.Should().OnlyHaveUniqueItems("each key gets its own client whether the cache kept the entry or evicted it");

		var again = factory.CreateClient(new HdFleetClientOptions {
			Key = "key-3"
		});

		again.Should().NotBeNull();
	}

	[Fact]
	public void CreateClient_ZeroTimeout_Throws() {
		using var provider = CreateProvider();

		var factory = provider.GetRequiredService<IHdFleetClientFactory>();

		var act = () => factory.CreateClient(new HdFleetClientOptions {
			Key = "key-1",
			Timeout = TimeSpan.Zero
		});

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	private static ServiceProvider CreateProvider() {
		var services = new ServiceCollection();

		services.AddHdFleet()
				.AddHttpClient(nameof(IHdFleetClient), hc => hc.BaseAddress = new Uri("https://localhost:9/api/external/v1/"))
				.ConfigurePrimaryHttpMessageHandler(() => new MockHttpMessageHandler());

		return services.BuildServiceProvider();
	}
}
