using System.Text.Json;

namespace AIInventory.Api.Services;

public class AzureAgentToolService
{
    private readonly FunctionInventoryService _inventoryService;
    private readonly ILogger<AzureAgentToolService> _logger;

    public AzureAgentToolService(
        FunctionInventoryService inventoryService,
        ILogger<AzureAgentToolService> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string toolName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing approved inventory tool {ToolName}.",
            toolName);

        return toolName switch
        {
            "get_subscriptions" =>
                await GetSubscriptionsAsync(cancellationToken),

            "get_vms" =>
                await GetVmsAsync(cancellationToken),

            "get_vm_count" =>
                await GetVmCountAsync(cancellationToken),

            "get_resource_groups" =>
                await GetResourceGroupsAsync(cancellationToken),

            "get_resource_summary" =>
                await GetResourceSummaryAsync(cancellationToken),

            "get_subnets" =>
                await GetSubnetsAsync(cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unsupported Foundry tool '{toolName}'.")
        };
    }

    public async Task<string> GetSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            await _inventoryService.GetSubscriptionsAsync(
                cancellationToken);

        return result.GetRawText();
    }

    public async Task<string> GetVmsAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            await _inventoryService.GetVmsAsync(
                top: 1000,
                cancellationToken);

        return result.GetRawText();
    }

    public async Task<string> GetVmCountAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            await _inventoryService.GetVmsAsync(
                top: 2000,
                cancellationToken);

        var count = 0;

        if (result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            count = data.GetArrayLength();
        }

        return JsonSerializer.Serialize(new
        {
            count
        });
    }

    public async Task<string> GetResourceGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            await _inventoryService.GetResourceGroupsAsync(
                top: 1000,
                cancellationToken);

        return result.GetRawText();
    }

    public async Task<string> GetResourceSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            await _inventoryService.GetSummaryAsync(
                cancellationToken);

        return result.GetRawText();
    }

    public async Task<string> GetSubnetsAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            await _inventoryService.GetSubnetsAsync(
                top: 1000,
                cancellationToken);

        return result.GetRawText();
    }
}
