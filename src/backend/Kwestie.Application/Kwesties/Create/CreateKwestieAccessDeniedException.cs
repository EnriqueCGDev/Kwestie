namespace Kwestie.Application.Kwesties.Create;

public sealed class CreateKwestieAccessDeniedException : Exception
{
    public CreateKwestieAccessDeniedException()
        : base("The creator must have an active membership in an existing workspace.")
    {
    }
}
