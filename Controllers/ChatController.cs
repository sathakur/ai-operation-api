using AIInventory.Api.Models;
using AIInventory.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace AIInventory.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
[RequiredScope("Inventory.Read")]
public class ChatController : ControllerBase
{
    private readonly FoundryAgentService _agentService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        FoundryAgentService agentService,
        ILogger<ChatController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                error = "Message is required."
            });
        }

        var correlationId = Guid.NewGuid().ToString();

        var objectId =
            User.FindFirst("oid")?.Value
            ?? "unknown";

        var tenantId =
            User.FindFirst("tid")?.Value
            ?? "unknown";

        var username =
            User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst("upn")?.Value
            ?? User.Identity?.Name
            ?? "unknown";

        var displayName =
            User.FindFirst("name")?.Value
            ?? username;

        _logger.LogInformation(
            "Chat request started. CorrelationId={CorrelationId}, ObjectId={ObjectId}, TenantId={TenantId}, User={User}, DisplayName={DisplayName}",
            correlationId,
            objectId,
            tenantId,
            username,
            displayName);

        try
        {
            var response =
                await _agentService.RunAsync(
                    request.Message,
                    correlationId,
                    username);

            _logger.LogInformation(
                "Chat request completed. CorrelationId={CorrelationId}, ObjectId={ObjectId}, User={User}",
                correlationId,
                objectId,
                username);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Chat request failed. CorrelationId={CorrelationId}, ObjectId={ObjectId}, User={User}",
                correlationId,
                objectId,
                username);

            return Problem(
                title: "Foundry agent call failed",
                detail:
                    $"The agent could not process the request. Correlation ID: {correlationId}",
                statusCode: 500);
        }
    }
}
