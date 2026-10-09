using Kwestie.Api.Contracts.Kwesties;
using Kwestie.Application.Kwesties.Create;
using Kwestie.Application.Kwesties.List;
using Kwestie.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kwestie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceId}/kwesties")]
public sealed class KwestiesController(CreateKwestieHandler create, ListKwestiesHandler list) : ControllerBase
{
    private readonly CreateKwestieHandler _create = create;
    private readonly ListKwestiesHandler _list = list;

    [HttpGet]
    [ProducesResponseType<KwestieSummaryResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List([FromRoute] Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty)
            return Unauthorized();

        var result = await _list.HandleAsync(new ListKwestiesQuery(workspaceId, userId), cancellationToken);
        // Match the individual Workspace endpoint's uniform empty 404.
        return result is null ? NotFound(null) : Ok(result.Select(kwestie => new KwestieSummaryResponse(
            kwestie.KwestieId, kwestie.Title, kwestie.Description,
            (int)kwestie.Status, (int)kwestie.Priority, kwestie.CreatedAt)).ToArray());
    }

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
