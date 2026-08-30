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
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Executing approved inventory tool {ToolName}.",
            toolName);

        return toolName switch
        {
            "get_subscriptions" => await GetSubscriptionsAsync(ct),
            "get_vms" => await GetVmsAsync(ct),
            "get_vm_count" => await GetVmCountAsync(ct),
            "get_resource_groups" => await GetResourceGroupsAsync(ct),
            "get_resource_summary" => await GetResourceSummaryAsync(ct),
            "get_subnets" => await GetSubnetsAsync(ct),
            "get_resources" => await GetResourcesAsync(ct),
            "get_storage_accounts" => await GetStorageAccountsAsync(ct),
            "get_resource_count" => await GetResourceCountAsync(ct),
            _ => throw new InvalidOperationException(
                $"Unsupported Foundry tool '{toolName}'.")
        };
    }

    private async Task<string> GetSubscriptionsAsync(CancellationToken ct) =>
        (await _inventoryService.GetSubscriptionsAsync(ct)).GetRawText();

    private async Task<string> GetVmsAsync(CancellationToken ct) =>
        (await _inventoryService.GetVmsAsync(1000, ct)).GetRawText();

    private async Task<string> GetResourceGroupsAsync(CancellationToken ct) =>
        (await _inventoryService.GetResourceGroupsAsync(1000, ct)).GetRawText();

    private async Task<string> GetResourceSummaryAsync(CancellationToken ct) =>
        (await _inventoryService.GetSummaryAsync(ct)).GetRawText();

    private async Task<string> GetSubnetsAsync(CancellationToken ct) =>
        (await _inventoryService.GetSubnetsAsync(1000, ct)).GetRawText();

    private async Task<string> GetResourcesAsync(CancellationToken ct) =>
        (await _inventoryService.GetResourcesAsync(1000, null, ct)).GetRawText();

    private async Task<string> GetStorageAccountsAsync(CancellationToken ct) =>
        (await _inventoryService.GetStorageAccountsAsync(1000, ct)).GetRawText();

    private async Task<string> GetVmCountAsync(CancellationToken ct)
    {
        var result = await _inventoryService.GetVmsAsync(2000, ct);
        var count = GetDataArrayCount(result);
        return JsonSerializer.Serialize(new { count });
    }

    private async Task<string> GetResourceCountAsync(CancellationToken ct)
    {
        var result = await _inventoryService.GetSummaryAsync(ct);
        long total = 0;

        if (result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in data.EnumerateArray())
            {
                if (!row.TryGetProperty("count", out var c))
                    continue;

                if (c.ValueKind == JsonValueKind.Number && c.TryGetInt64(out var n))
                    total += n;
                else if (c.ValueKind == JsonValueKind.String &&
                         long.TryParse(c.GetString(), out n))
                    total += n;
            }
        }

        return JsonSerializer.Serialize(new { count = total });
    }

    private static int GetDataArrayCount(JsonElement result)
    {
        if (result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
            return data.GetArrayLength();

        return 0;
    }
}
