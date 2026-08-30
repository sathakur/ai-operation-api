using System.Text.Json;

namespace AIInventory.Api.Services;

public class AzureAgentToolService
{
    private readonly AzureInventoryService _inventoryService;
    private readonly ILogger<AzureAgentToolService> _logger;

    public AzureAgentToolService(
        AzureInventoryService inventoryService,
        ILogger<AzureAgentToolService> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<string> GetSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Agent tool started: get_subscriptions");

        var subscriptions =
            await _inventoryService.GetSubscriptionsAsync(
                cancellationToken);

        var result =
            subscriptions.Select(s => new
            {
                subscriptionId = s.SubscriptionId,
                displayName = s.DisplayName,
                state = s.State
            });

        return JsonSerializer.Serialize(result);
    }

    public async Task<string> GetVmsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Agent tool started: get_vms");

        var vms =
            await _inventoryService.GetVirtualMachinesAsync(
                cancellationToken);

        var result =
            vms.Select(vm => new
            {
                id = vm.Id,
                name = vm.Name,
                subscriptionId = vm.SubscriptionId,
                resourceGroup = vm.ResourceGroup,
                location = vm.Location,
                vmSize = vm.VmSize,
                osType = vm.OsType
            });

        return JsonSerializer.Serialize(result);
    }

    public async Task<string> GetVmCountAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Agent tool started: get_vm_count");

        var vms =
            await _inventoryService.GetVirtualMachinesAsync(
                cancellationToken);

        return JsonSerializer.Serialize(
            new
            {
                count = vms.Count
            });
    }
}
