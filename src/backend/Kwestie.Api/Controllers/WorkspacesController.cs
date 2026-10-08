using Kwestie.Api.Contracts.Workspaces;
using Kwestie.Application.Workspaces.Create;
using Kwestie.Application.Workspaces.Get;
using Kwestie.Application.Workspaces.List;
using Kwestie.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kwestie.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces")]
public sealed class WorkspacesController(
    CreateWorkspaceHandler create,
    ListWorkspacesHandler list,
    GetWorkspaceHandler get) : ControllerBase
{
    private readonly CreateWorkspaceHandler _create = create;
    private readonly ListWorkspacesHandler _list = list;
    private readonly GetWorkspaceHandler _get = get;

    [HttpPost]
    [ProducesResponseType<CreateWorkspaceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var result = await _create.HandleAsync(
                new CreateWorkspaceCommand(request.Name!, userId), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new CreateWorkspaceResponse(result.WorkspaceId));
        }
        catch (DomainException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid workspace.", detail: exception.Message);
        }
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WorkspaceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _list.HandleAsync(new ListWorkspacesQuery(userId), cancellationToken);
        return Ok(result.Select(workspace => new WorkspaceResponse(
            workspace.WorkspaceId, workspace.Name, workspace.CreatedAt)).ToArray());
    }

    [HttpGet("{workspaceId}")]
    [ProducesResponseType<WorkspaceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _get.HandleAsync(new GetWorkspaceQuery(workspaceId, userId), cancellationToken);
        // The null-valued overload keeps every unavailable case's response body empty.
        return result is null ? NotFound(null) : Ok(new WorkspaceResponse(
            result.WorkspaceId, result.Name, result.CreatedAt));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;
}
