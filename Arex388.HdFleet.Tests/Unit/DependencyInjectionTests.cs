using Arex388.HdFleet.Testing;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class DependencyInjectionTests {
	[Fact]
	public void AddHdFleet_RegistersFactoryMode() {
		var services = new ServiceCollection();

		services.AddHdFleet();

		using var provider = services.BuildServiceProvider();

		provider.GetService<IHdFleetClientFactory>().Should().NotBeNull();
		provider.GetService<IMemoryCache>().Should().NotBeNull();
		provider.GetService<IValidator<ListLatestLocations.Request>>().Should().NotBeNull("file-scoped validators must register via the includeInternalTypes scan");
		provider.GetService<IValidator<ListVehicles.Request>>().Should().NotBeNull();
		provider.GetService<IHdFleetClient>().Should().BeNull("the factory overload does not register a single-account client");
	}

	[Fact]
	public void AddHdFleet_WithOptions_RegistersSingleAccountMode() {
		var services = new ServiceCollection();

		services.AddHdFleet(new HdFleetClientOptions {
			Key = "single-account-key"
		}).AddHttpClient(nameof(IHdFleetClient), hc => hc.BaseAddress = new Uri("https://localhost:9/api/external/v1/"))
		  .ConfigurePrimaryHttpMessageHandler(() => new MockHttpMessageHandler());

		using var provider = services.BuildServiceProvider();

		provider.GetService<IHdFleetClient>().Should().NotBeNull();
		provider.GetService<HdFleetClientOptions>().Should().NotBeNull();
		provider.GetService<IMemoryCache>().Should().NotBeNull("the single-account overload must also register the memory cache — the fresh-poll gate needs it in both modes");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void AddHdFleet_NonPositiveTimeout_Throws(
		int milliseconds) {
		var services = new ServiceCollection();

		var act = () => services.AddHdFleet(new HdFleetClientOptions {
			Key = "single-account-key",
			Timeout = TimeSpan.FromMilliseconds(milliseconds)
		});

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void AddHdFleet_DisablesHttpClientTimeout() {
		var services = new ServiceCollection();

		services.AddHdFleet();

		using var provider = services.BuildServiceProvider();

		var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IHdFleetClient));

		httpClient.Timeout.Should().Be(Timeout.InfiniteTimeSpan, "the library's own deadline is the only one");
	}
}
