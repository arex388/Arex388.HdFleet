using System.Runtime.CompilerServices;

namespace Arex388.HdFleet.Tests;

/// <summary>
/// A fact that runs against the live HD Fleet API, spends real quota, and
/// consumes fresh-poll floors. Skipped unless the HDFLEET_LIVE_TESTS
/// environment variable is set to "1" AND the key user secret is present —
/// a bare test run never hits the API.
/// </summary>
public sealed class LiveFactAttribute :
	FactAttribute {
	public LiveFactAttribute(
		[CallerFilePath] string? sourceFilePath = null,
		[CallerLineNumber] int sourceLineNumber = -1) :
		base(sourceFilePath, sourceLineNumber) {
		if (Environment.GetEnvironmentVariable("HDFLEET_LIVE_TESTS") != "1") {
			Skip = "Live HD Fleet tests are opt-in. Set HDFLEET_LIVE_TESTS=1 and configure the key user secret to run them.";
		} else if (string.IsNullOrEmpty(Config.Key)) {
			Skip = "HDFLEET_LIVE_TESTS is set but the key user secret is missing.";
		}
	}
}
