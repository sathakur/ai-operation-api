using System.Security.Claims;
using AIInventory.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace AIInventory.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
[RequiredScope("Inventory.Read")]
public class InventoryController : ControllerBase
{
    private readonly AzureInventoryService _azureInventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        AzureInventoryService azureInventoryService,
        ILogger<InventoryController> logger)
    {
        _azureInventoryService = azureInventoryService;
        _logger = logger;
    }

    [HttpGet("whoami")]
    public IActionResult WhoAmI()
    {
        var result = new
        {
            name = User.Identity?.Name
                ?? User.FindFirst("name")?.Value,
            preferredUsername =
                User.FindFirst("preferred_username")?.Value
                ?? User.FindFirst(ClaimTypes.Upn)?.Value,
            objectId =
                User.FindFirst("oid")?.Value
                ?? User.FindFirst(
                    "http://schemas.microsoft.com/identity/claims/objectidentifier"
                )?.Value,
            tenantId =
                User.FindFirst("tid")?.Value
                ?? User.FindFirst(
                    "http://schemas.microsoft.com/identity/claims/tenantid"
                )?.Value,
            scope = User.FindFirst("scp")?.Value
        };

        return Ok(result);
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions(
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _azureInventoryService.GetSubscriptionsAsync(
                    cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve Azure subscriptions using delegated user access.");

            return Problem(
                title: "Unable to retrieve Azure subscriptions",
                detail:
                    "The backend could not obtain or use the delegated Azure Management token. Check Azure Service Management delegated permission, consent, and the user's Azure RBAC access.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("vms")]
    public async Task<IActionResult> GetVirtualMachines(
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _azureInventoryService.GetVirtualMachinesAsync(
                    cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve Azure VM inventory using delegated user access.");

            return Problem(
                title: "Unable to retrieve Azure VM inventory",
                detail:
                    "The backend could not obtain or use the delegated Azure Management token. Check Azure Service Management delegated permission, consent, and the user's Azure RBAC access.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
