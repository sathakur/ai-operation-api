using AIInventory.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace AIInventory.Api.Controllers;

[ApiController]
[Route("api/central-inventory")]
[Authorize]
[RequiredScope("Inventory.Read")]
public class CentralInventoryController : ControllerBase
{
    private readonly FunctionInventoryService _inventoryService;
    private readonly ILogger<CentralInventoryController> _logger;

    public CentralInventoryController(
        FunctionInventoryService inventoryService,
        ILogger<CentralInventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions(
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _inventoryService.GetSubscriptionsAsync(
                cancellationToken),
            "subscriptions");
    }

    [HttpGet("vms")]
    public async Task<IActionResult> GetVms(
        [FromQuery] int top = 1000,
        CancellationToken cancellationToken = default)
    {
        top = ClampTop(top);

        return await ExecuteAsync(
            () => _inventoryService.GetVmsAsync(
                top,
                cancellationToken),
            "VM inventory");
    }

    [HttpGet("resource-groups")]
    public async Task<IActionResult> GetResourceGroups(
        [FromQuery] int top = 1000,
        CancellationToken cancellationToken = default)
    {
        top = ClampTop(top);

        return await ExecuteAsync(
            () => _inventoryService.GetResourceGroupsAsync(
                top,
                cancellationToken),
            "resource groups");
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            () => _inventoryService.GetSummaryAsync(
                cancellationToken),
            "resource summary");
    }

    [HttpGet("subnets")]
    public async Task<IActionResult> GetSubnets(
        [FromQuery] int top = 1000,
        CancellationToken cancellationToken = default)
    {
        top = ClampTop(top);

        return await ExecuteAsync(
            () => _inventoryService.GetSubnetsAsync(
                top,
                cancellationToken),
            "subnets");
    }

    [HttpGet("resources")]
    public async Task<IActionResult> GetResources(
        [FromQuery] int top = 1000,
        CancellationToken cancellationToken = default)
    {
        top = ClampTop(top);

        return await ExecuteAsync(
            () => _inventoryService.GetResourcesAsync(
                top: top,
                resourceType: null,
                ct: cancellationToken),
            "resources");
    }

    private async Task<IActionResult> ExecuteAsync(
        Func<Task<System.Text.Json.JsonElement>> action,
        string operationName)
    {
        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Central inventory configuration error while retrieving {OperationName}.",
                operationName);

            return Problem(
                title: "Central inventory is not configured",
                detail:
                    "The backend inventory Function configuration is missing.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Central inventory Function call failed while retrieving {OperationName}.",
                operationName);

            return Problem(
                title: "Unable to retrieve central Azure inventory",
                detail:
                    "The backend could not retrieve data from the inventory Function App.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while retrieving {OperationName}.",
                operationName);

            return Problem(
                title: "Unable to retrieve central Azure inventory",
                detail:
                    "An unexpected backend error occurred.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static int ClampTop(int top)
    {
        if (top < 1)
        {
            return 1;
        }

        return Math.Min(top, 2000);
    }
}
