using System.Net;
using System.Text;

namespace Arex388.HdFleet.Testing;

/// <summary>
/// Mock HTTP handler that returns canned JSON responses from the shared
/// <c>TestData/Responses</c> fixtures. Shared by the unit tests and the
/// benchmarks so neither ever hits the live HD Fleet API.
/// </summary>
internal sealed class MockHttpMessageHandler :
	HttpMessageHandler {
	private readonly string _responsesDir;
	private readonly Dictionary<string, string> _responseCache = new();

	public MockHttpMessageHandler() {
		_responsesDir = Path.Combine(AppContext.BaseDirectory, "Responses");

		if (!Directory.Exists(_responsesDir)) {
			throw new DirectoryNotFoundException($"Responses directory not found at: {_responsesDir}. Ensure the TestData/Responses fixtures are linked into the project.");
		}

		LoadResponses();
	}

	private void LoadResponses() {
		foreach (var file in Directory.GetFiles(_responsesDir, "*.json")) {
			var fileName = Path.GetFileName(file);
			var content = File.ReadAllText(file);

			_responseCache[fileName] = content;
		}

		if (_responseCache.Count == 0) {
			throw new InvalidOperationException($"No JSON response files found in: {_responsesDir}. Ensure the TestData/Responses fixtures are linked into the project.");
		}
	}

	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken) {
		var path = request.RequestUri?.AbsolutePath ?? string.Empty;
		var fileName = GetResponseFileName(path);

		if (!_responseCache.TryGetValue(fileName, out var json)) {
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) {
				Content = new StringContent($"No cached response for: {path}")
			});
		}

		var response = new HttpResponseMessage(HttpStatusCode.OK) {
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};

		return Task.FromResult(response);
	}

	private static string GetResponseFileName(
		string path) {
		var cleanPath = path.Replace("/api/external/v1/", "").Split('?')[0];

		if (cleanPath == "locations/latest") {
			return "locations-latest.json";
		}

		if (cleanPath == "locations/history") {
			return "locations-history.json";
		}

		if (cleanPath.StartsWith("vehicles/", StringComparison.Ordinal)) {
			return "vehicle.json";
		}

		if (cleanPath == "vehicles") {
			return "vehicles.json";
		}

		if (cleanPath == "drivers") {
			return "drivers.json";
		}

		return cleanPath.Replace('/', '-') + ".json";
	}
}
