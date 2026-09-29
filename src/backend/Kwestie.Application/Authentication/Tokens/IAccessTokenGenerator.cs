namespace Kwestie.Application.Authentication.Tokens;

public interface IAccessTokenGenerator
{
    AccessTokenResult Generate(Guid userId);
}
