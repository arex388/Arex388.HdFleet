using FluentAssertions;
using System.Globalization;

namespace Arex388.HdFleet.Tests.Unit;

public sealed class IntegrationsTests {
	[Fact]
	public async Task Integrations_Deserialize() {
		var client = TestClients.CreateWithFixtures();

		var response = await client.ListIntegrationsAsync();

		response.Success.Should().BeTrue();
		response.Integrations.Should().HaveCount(2);

		var healthy = response.Integrations[0];

		healthy.Id.Should().Be("int-1");
		healthy.Partner.Should().Be("hdfleet-upstream");
		healthy.Mode.Should().Be("pull");
		healthy.Status.Should().Be("healthy");
		healthy.LastSuccessAt.Should().Be(DateTimeOffset.Parse("2026-07-31T17:50:00Z", CultureInfo.InvariantCulture));
		healthy.LastErrorAt.Should().BeNull();

		var degraded = response.Integrations[1];

		degraded.Id.Should().Be("int-2");
		degraded.Status.Should().Be("degraded");
		degraded.DisplayName.Should().BeNull();
		degraded.LastErrorAt.Should().NotBeNull();
	}

	[Fact]
	public async Task ListIntegrations_Default_BuildsExactEndpoint() {
		var client = TestClients.CreateWithJson("{ \"data\": [] }", out var handler);

		await client.ListIntegrationsAsync();

		handler.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/api/external/v1/integrations?limit=200");
	}
}
