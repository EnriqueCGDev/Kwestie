using Kwestie.Api.Contracts.Kwesties;
using Kwestie.Application.Kwesties.Create;
using Kwestie.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kwestie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceId}/kwesties")]
public sealed class KwestiesController(CreateKwestieHandler create) : ControllerBase
{
    private readonly CreateKwestieHandler _create = create;

    [HttpPost]
    [ProducesResponseType<CreateKwestieResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromRoute] Guid workspaceId, [FromBody] CreateKwestieRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty)
            return Unauthorized();

        try
        {
            var result = await _create.HandleAsync(new CreateKwestieCommand(
                workspaceId, request.Title!, request.Description, request.Priority, userId, null), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new CreateKwestieResponse(result.Id));
        }
        catch (DomainException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid kwestie.", detail: exception.Message);
        }
        catch (CreateKwestieAccessDeniedException)
        {
            return Forbid();
        }
    }
}
