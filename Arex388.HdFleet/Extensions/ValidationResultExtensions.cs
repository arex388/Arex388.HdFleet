namespace FluentValidation.Results;

internal static class ValidationResultExtensions {
	public static IReadOnlyList<string> ToErrors(
		this ValidationResult validationResult) => validationResult.Errors.Select(
		e => e.ErrorMessage).ToList();
}
