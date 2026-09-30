namespace Kwestie.Api.Contracts.Authentication;

public sealed record RegistrationErrorsResponse(IReadOnlyList<string> Errors);
