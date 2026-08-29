namespace AIInventory.Api.Models;

public record AzureSubscription(
    string SubscriptionId,
    string DisplayName,
    string? State);
