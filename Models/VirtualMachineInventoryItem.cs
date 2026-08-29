namespace AIInventory.Api.Models;

public record VirtualMachineInventoryItem(
    string? Id,
    string? Name,
    string? SubscriptionId,
    string? ResourceGroup,
    string? Location,
    string? VmSize,
    string? OsType);
