using Kwestie.Api.Contracts.Authentication;
using Kwestie.Application.Authentication.Login;
using Kwestie.Application.Authentication.Logout;
using Kwestie.Application.Authentication.Refresh;
using Kwestie.Application.Authentication.Register;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kwestie.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthenticationController(
    RegisterUserHandler registration,
    LoginUserHandler login,
    RefreshSessionHandler refresh,
    LogoutSessionHandler logout) : ControllerBase
{
    private const string RefreshCookieName = "kwestie_refresh_token";
    private readonly RegisterUserHandler _registration = registration;
    private readonly LoginUserHandler _login = login;
    private readonly RefreshSessionHandler _refresh = refresh;
    private readonly LogoutSessionHandler _logout = logout;

    [HttpPost("register")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<RegistrationErrorsResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _registration.HandleAsync(
            new RegisterUserCommand(request.Email, request.Password), cancellationToken);
        if (!result.Succeeded)
            return BadRequest(new RegistrationErrorsResponse(result.Errors));

        return StatusCode(StatusCodes.Status201Created, new RegisterResponse(result.UserId!.Value));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _login.HandleAsync(
            new LoginUserCommand(request.Email, request.Password), cancellationToken);
        if (!result.Succeeded)
            return Unauthorized();

        WriteRefreshCookie(result.RefreshToken!, result.RefreshTokenExpiresAtUtc!.Value);
        return Ok(new AuthenticationResponse(result.UserId!.Value, result.AccessToken!,
            result.AccessTokenExpiresAtUtc!.Value, result.RefreshTokenExpiresAtUtc.Value));
    }

    [HttpPost("refresh")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(token))
            return RejectRefresh();

        var result = await _refresh.HandleAsync(new RefreshSessionCommand(token), cancellationToken);
        if (!result.Succeeded)
            return RejectRefresh();

        WriteRefreshCookie(result.RefreshToken!, result.RefreshTokenExpiresAtUtc!.Value);
        return Ok(new AuthenticationResponse(result.UserId!.Value, result.AccessToken!,
            result.AccessTokenExpiresAtUtc!.Value, result.RefreshTokenExpiresAtUtc.Value));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _logout.HandleAsync(new LogoutSessionCommand(Request.Cookies[RefreshCookieName]), cancellationToken);
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());
        return NoContent();
    }

    private void WriteRefreshCookie(string token, DateTimeOffset expiresAtUtc)
    {
        var options = RefreshCookieOptions();
        options.Expires = expiresAtUtc;
        Response.Cookies.Append(RefreshCookieName, token, options);
    }

    private UnauthorizedResult RejectRefresh()
    {
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions());
        return Unauthorized();
    }

    private static CookieOptions RefreshCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth"
    };
}
