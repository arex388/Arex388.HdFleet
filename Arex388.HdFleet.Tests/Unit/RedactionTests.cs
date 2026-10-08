using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Arex388.HdFleet.Tests.Unit;

//	An error page can echo the bearer token. Nothing captured off the wire may
//	carry the raw key — it is replaced with [redacted] before it is stored.
public sealed class RedactionTests {
	private const string Redacted = "[redacted]";

	[Fact]
	public async Task ErrorBodyEchoingKey_IsRedacted_InBodyExcerptAndErrors() {
		var json = TestClients.ReadFixture("error-echoes-key.json");

		json.Should().Contain(TestClients.ApiKey, "the fixture must echo the configured key for this test to mean anything");

		var client = TestClients.Create(new CapturingHandler(json, HttpStatusCode.Unauthorized));

		var response = await client.ListVehiclesAsync();

		response.Status.Should().Be(ResponseStatus.Unauthorized);
		response.Errors.Should().NotContain(e => e.Contains(TestClients.ApiKey, StringComparison.Ordinal));
		response.Errors[1].Should().Be("unauthorized: The API key [redacted] is not valid. (Authorization: Bearer [redacted] was rejected.)");
		response.Metadata.Should().NotBeNull();
		response.Metadata.BodyExcerpt.Should().NotBeNull()
				.And.Contain(Redacted)
				.And.NotContain(TestClients.ApiKey);
	}

	[Fact]
	public async Task StreamException_ErrorBodyEchoingKey_MessageIsRedacted() {
		var json = TestClients.ReadFixture("error-echoes-key.json");
		var client = TestClients.Create(new CapturingHandler(json, HttpStatusCode.Unauthorized));

		var act = async () => {
			await foreach (var _ in client.StreamVehiclesAsync()) {
			}
		};

		var exception = (await act.Should().ThrowAsync<HdFleetException>()).Which;

		exception.Message.Should().Contain(Redacted).And.NotContain(TestClients.ApiKey);
		exception.Errors.Should().NotContain(e => e.Contains(TestClients.ApiKey, StringComparison.Ordinal));
		exception.Metadata!.BodyExcerpt.Should().NotContain(TestClients.ApiKey);
	}

	[Fact]
	public async Task ResponseHeaderEchoingKey_IsRedacted() {
		var client = TestClients.Create(new CapturingHandler("{ \"data\": [] }", HttpStatusCode.OK, r => r.Headers.TryAddWithoutValidation("X-Echo", "Bearer " + TestClients.ApiKey)));

		var response = await client.ListVehiclesAsync();

		response.Success.Should().BeTrue();
		response.Metadata!.Headers["x-echo"].Should().Be("Bearer " + Redacted, "header keys are case-insensitive and values are redacted");
		response.Metadata.Headers.Values.Should().NotContain(v => v.Contains(TestClients.ApiKey, StringComparison.Ordinal));
	}

	[Fact]
	public async Task KeyStraddlingTheExcerptCut_IsRedactedWhole() {
		//	The key starts two characters before the 512-character cut, so a
		//	truncate-then-redact implementation would leak its prefix.
		var body = new string('x', 510) + TestClients.ApiKey + new string('y', 100);
		var client = TestClients.Create(new CapturingHandler(body, HttpStatusCode.BadRequest));

		var response = await client.ListVehiclesAsync();

		response.Metadata!.BodyExcerpt.Should().HaveLength(512)
				.And.StartWith(new string('x', 510) + "[r")
				.And.NotContain("unit");
	}

	[Fact]
	public async Task SecondEchoStraddlingTheCut_LeaksNoPartOfTheKey() {
		//	Redacting the first echo shortens the text by three characters, so a
		//	redact-a-window-then-cut implementation pulls the second, partially
		//	windowed echo into the excerpt.
		var body = TestClients.ApiKey + new string('x', 500) + TestClients.ApiKey + new string('y', 100);
		var client = TestClients.Create(new CapturingHandler(body, HttpStatusCode.BadRequest));

		var response = await client.ListVehiclesAsync();

		var excerpt = response.Metadata!.BodyExcerpt!;

		excerpt.Should().HaveLength(512);
		AssertNoKeyFragment(excerpt, TestClients.ApiKey);
	}

	[Fact]
	public async Task LongKeyEchoedTwice_ExcerptIsFull512Characters() {
		var key = new string('k', 32) + new string('q', 32);
		var body = key + key + new string('x', 1000);

		using var provider = TestClients.CreateProvider(new CapturingHandler(body, HttpStatusCode.BadRequest), key);

		var client = provider.GetRequiredService<IHdFleetClient>();

		var response = await client.ListVehiclesAsync();

		var excerpt = response.Metadata!.BodyExcerpt!;

		excerpt.Should().HaveLength(512);
		excerpt.Should().StartWith(Redacted + Redacted + "xxx");
		AssertNoKeyFragment(excerpt, key);
	}

	[Theory]
	[InlineData("abc\\u002Bdef")]
	[InlineData("abc\\u002bdef")]
	public async Task JsonEscapedKeyEcho_IsRedactedInBodyExcerpt(
		string escapedKey) {
		const string key = "abc+def";
		var body = "{ \"error\": { \"code\": \"unauthorized\", \"message\": \"Bad key " + escapedKey + ".\" } }";

		using var provider = TestClients.CreateProvider(new CapturingHandler(body, HttpStatusCode.Unauthorized), key);

		var client = provider.GetRequiredService<IHdFleetClient>();

		var response = await client.ListVehiclesAsync();

		response.Metadata!.BodyExcerpt.Should().Contain(Redacted).And.NotContain("abc");
		response.Errors[1].Should().Be("unauthorized: Bad key [redacted].", "the envelope's message is unescaped, so the raw form is redacted");
	}

	[Fact]
	public async Task CursorEchoingKey_IsRedactedInExceptionEndpointAndMessage() {
		//	A key with no common English fragments, so the 3-character check
		//	cannot trip on the library's own message text.
		const string key = "zq9-kx7-wv3-jp2";
		var page1 = "{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ], \"nextCursor\": \"" + key + "\" }";
		var handler = new SequenceHandler(
			(HttpStatusCode.OK, page1),
			(HttpStatusCode.InternalServerError, "{}"));

		using var provider = TestClients.CreateProvider(handler, key);

		var client = provider.GetRequiredService<IHdFleetClient>();

		var act = async () => {
			await foreach (var _ in client.StreamVehiclesAsync()) {
			}
		};

		var exception = (await act.Should().ThrowAsync<HdFleetException>()).Which;

		handler.Requests[1].Query.Should().Contain("cursor=" + key, "the outgoing request URL is unchanged");
		exception.Endpoint.Should().Be("vehicles?limit=200&cursor=" + Redacted);
		exception.Message.Should().Contain(Redacted);
		AssertNoKeyFragment(exception.Endpoint, key);
		AssertNoKeyFragment(exception.Message, key);
	}

	[Fact]
	public async Task CursorEchoingUrlEncodedKey_IsRedactedInExceptionEndpoint() {
		//	'+' and '/' reach the endpoint as %2B and %2F.
		const string key = "zq9+kx7/wv3";
		var page1 = "{ \"data\": [ { \"externalVehicleId\": \"V-1\" } ], \"nextCursor\": \"" + key + "\" }";
		var handler = new SequenceHandler(
			(HttpStatusCode.OK, page1),
			(HttpStatusCode.Unauthorized, "{}"));

		using var provider = TestClients.CreateProvider(handler, key);

		var client = provider.GetRequiredService<IHdFleetClient>();

		var act = async () => {
			await foreach (var _ in client.StreamVehiclesAsync()) {
			}
		};

		var exception = (await act.Should().ThrowAsync<HdFleetException>()).Which;

		handler.Requests[1].Query.Should().Contain("cursor=zq9%2Bkx7%2Fwv3");
		exception.Endpoint.Should().Be("vehicles?limit=200&cursor=" + Redacted);
		exception.Message.Should().NotContain("zq9").And.NotContain("wv3");
	}

	private static void AssertNoKeyFragment(
		string excerpt,
		string key) {
		for (var i = 0; i + 3 <= key.Length; i++) {
			excerpt.Should().NotContain(key.Substring(i, 3), "no substring of the key longer than two characters may reach the excerpt");
		}
	}
}
