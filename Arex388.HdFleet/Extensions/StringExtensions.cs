using System.Diagnostics.CodeAnalysis;

namespace System;

internal static class StringExtensions {
	public static bool HasValue(
		[NotNullWhen(true)]
		this string? value) => !string.IsNullOrEmpty(value);
}
