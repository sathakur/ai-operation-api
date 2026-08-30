using System.Text.Json;
namespace AIInventory.Api.Services;
public class AzureAgentToolService
{
    private readonly AzureInventoryService _inventoryService;
    private readonly ILogger<AzureAgentToolService> _logger;
    public AzureAgentToolService(AzureInventoryService inventoryService, ILogger<AzureAgentToolService> logger)
    { _inventoryService = inventoryService; _logger = logger; }

    public async Task<string> GetSubscriptionsAsync()
    {
        _logger.LogInformation("Agent tool started: get_subscriptions");
        var x = await _inventoryService.GetSubscriptionsAsync();
        return JsonSerializer.Serialize(x.Select(s => new { subscriptionId=s.SubscriptionId, displayName=s.DisplayName, state=s.State }));
    }
    public async Task<string> GetVmsAsync()
    {
        _logger.LogInformation("Agent tool started: get_vms");
        var x = await _inventoryService.GetVmsAsync();
        return JsonSerializer.Serialize(x.Select(v => new { name=v.Name, subscriptionId=v.SubscriptionId, resourceGroup=v.ResourceGroup, location=v.Location, id=v.Id }));
    }
    public async Task<string> GetVmCountAsync()
    {
        _logger.LogInformation("Agent tool started: get_vm_count");
        var count = await _inventoryService.GetVmCountAsync();
        return JsonSerializer.Serialize(new { count });
    }
    public async Task<string> GetStorageAccountsAsync()
    {
        _logger.LogInformation("Agent tool started: get_storage_accounts");
        var x = await _inventoryService.GetStorageAccountsAsync();
        return JsonSerializer.Serialize(x.Select(a => new { name=a.Name, subscriptionId=a.SubscriptionId, resourceGroup=a.ResourceGroup, location=a.Location, id=a.Id }));
    }
    public async Task<string> GetSqlServersAsync()
    {
        _logger.LogInformation("Agent tool started: get_sql_servers");
        var x = await _inventoryService.GetSqlServersAsync();
        return JsonSerializer.Serialize(x.Select(s => new { name=s.Name, subscriptionId=s.SubscriptionId, resourceGroup=s.ResourceGroup, location=s.Location, id=s.Id }));
    }
}
