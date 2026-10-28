using Arex388.HdFleet.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;

namespace Arex388.HdFleet.Tests.Unit;

/// <summary>
/// Builds IHdFleetClient instances backed by mock HTTP handlers. The named
/// client's base address is unroutable so a wiring regression fails fast
/// instead of hitting the live API.
/// </summary>
internal static class TestClients {
	public const string ApiKey = "unit-test-key";

	public static IHdFleetClient Create(
		HttpMessageHandler handler) => CreateProvider(handler).GetRequiredService<IHdFleetClient>();

	public static ServiceProvider CreateProvider(
		HttpMessageHandler handler,
		string key = ApiKey,
		TimeSpan? timeout = null,
		Action<IServiceCollection>? configure = null) {
		var services = new ServiceCollection();

		configure?.Invoke(services);

		services.AddHdFleet(new HdFleetClientOptions {
			Key = key,
			Timeout = timeout
		}).AddHttpClient(nameof(IHdFleetClient), hc => hc.BaseAddress = new Uri("https://localhost:9/api/external/v1/"))
		  .ConfigurePrimaryHttpMessageHandler(() => handler);

		return services.BuildServiceProvider();
	}

	public static ServiceProvider CreateFactoryProvider(
		HttpMessageHandler handler,
		Action<IServiceCollection>? configure = null) {
		var services = new ServiceCollection();

		configure?.Invoke(services);

		services.AddHdFleet()
				.AddHttpClient(nameof(IHdFleetClient), hc => hc.BaseAddress = new Uri("https://localhost:9/api/external/v1/"))
				.ConfigurePrimaryHttpMessageHandler(() => handler);

		return services.BuildServiceProvider();
	}

	/// <summary>Serves the shared TestData/Responses fixtures by route.</summary>
	public static IHdFleetClient CreateWithFixtures() => Create(new MockHttpMessageHandler());

	/// <summary>Serves the same JSON body for every request, recording each request URI.</summary>
	public static IHdFleetClient CreateWithJson(
		string json,
		out CapturingHandler handler) {
		handler = new CapturingHandler(json);

		return Create(handler);
	}

	public static string ReadFixture(
		string fileName) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Responses", fileName));
}

internal sealed class CapturingHandler(
	string json,
	HttpStatusCode statusCode = HttpStatusCode.OK,
	Action<HttpResponseMessage>? configure = null) :
	HttpMessageHandler {
	public IList<string?> Bodies { get; } = [];
	public IList<HttpMethod> Methods { get; } = [];
	public IList<Uri> Requests { get; } = [];

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken) {
		Bodies.Add(request.Content is null
			? null
			: await request.Content.ReadAsStringAsync(cancellationToken));
		Methods.Add(request.Method);
		Requests.Add(request.RequestUri!);

		var response = new HttpResponseMessage(statusCode) {
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};

		configure?.Invoke(response);

		return response;
	}
}

/// <summary>
/// Serves a fixed sequence of (status, json) responses, then repeats the last
/// one, recording each request URI.
/// </summary>
internal sealed class SequenceHandler(
	params (HttpStatusCode StatusCode, string Json)[] responses) :
	HttpMessageHandler {
	public IList<Uri> Requests { get; } = [];

	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken) {
		Requests.Add(request.RequestUri!);

		var (statusCode, json) = responses[Math.Min(Requests.Count - 1, responses.Length - 1)];

		var response = new HttpResponseMessage(statusCode) {
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};

		//	Retry-After: 0 keeps retry-path tests fast — only jitter delays remain.
		response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);

		return Task.FromResult(response);
	}
}

internal sealed class DelayingHandler :
	HttpMessageHandler {
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken) {
		await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

		return new HttpResponseMessage(HttpStatusCode.OK) {
			Content = new StringContent("{}", Encoding.UTF8, "application/json")
		};
	}
}

/// <summary>
/// Succeeds immediately with OK headers, then stalls the body until the read
/// is cancelled — a transfer that dies mid-download.
/// </summary>
internal sealed class DelayedBodyHandler :
	HttpMessageHandler {
	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
			Content = new DelayedContent()
		});

	private sealed class DelayedContent :
		HttpContent {
		protected override Task SerializeToStreamAsync(
			Stream stream,
			TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);

		protected override Task SerializeToStreamAsync(
			Stream stream,
			TransportContext? context,
			CancellationToken cancellationToken) => Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

		protected override bool TryComputeLength(
			out long length) {
			length = 0;

			return false;
		}
	}
}
