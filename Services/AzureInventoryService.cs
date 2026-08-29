using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIInventory.Api.Models;
using Microsoft.Identity.Web;

namespace AIInventory.Api.Services;

public class AzureInventoryService
{
    private const string ManagementScope =
        "https://management.azure.com/.default";

    private const string SubscriptionsUrl =
        "https://management.azure.com/subscriptions?api-version=2022-12-01";

    private const string ResourceGraphUrl =
        "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01";

    private readonly HttpClient _httpClient;
    private readonly ITokenAcquisition _tokenAcquisition;

    public AzureInventoryService(
        HttpClient httpClient,
        ITokenAcquisition tokenAcquisition)
    {
        _httpClient = httpClient;
        _tokenAcquisition = tokenAcquisition;
    }

    private async Task<string> GetManagementTokenAsync()
    {
        return await _tokenAcquisition.GetAccessTokenForUserAsync(
            new[] { ManagementScope });
    }

    public async Task<IReadOnlyList<AzureSubscription>> GetSubscriptionsAsync(
        CancellationToken cancellationToken)
    {
        var token = await GetManagementTokenAsync();

        using var request =
            new HttpRequestMessage(HttpMethod.Get, SubscriptionsUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document =
            JsonDocument.Parse(body);

        var subscriptions =
            new List<AzureSubscription>();

        if (!document.RootElement.TryGetProperty(
                "value",
                out var values))
        {
            return subscriptions;
        }

        foreach (var item in values.EnumerateArray())
        {
            subscriptions.Add(
                new AzureSubscription(
                    item.GetProperty("subscriptionId").GetString() ?? "",
                    item.GetProperty("displayName").GetString() ?? "",
                    item.TryGetProperty(
                        "state",
                        out var state)
                        ? state.GetString()
                        : null
                )
            );
        }

        return subscriptions;
    }

    public async Task<IReadOnlyList<VirtualMachineInventoryItem>>
        GetVirtualMachinesAsync(
            CancellationToken cancellationToken)
    {
        var subscriptions =
            await GetSubscriptionsAsync(cancellationToken);

        var subscriptionIds =
            subscriptions
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.SubscriptionId))
                .Select(x => x.SubscriptionId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (subscriptionIds.Length == 0)
        {
            return Array.Empty<VirtualMachineInventoryItem>();
        }

        var token = await GetManagementTokenAsync();

        const string query = """
Resources
| where type =~ "microsoft.compute/virtualmachines"
| project
    id,
    name,
    subscriptionId,
    resourceGroup,
    location,
    vmSize = tostring(properties.hardwareProfile.vmSize),
    osType = tostring(properties.storageProfile.osDisk.osType)
| order by name asc
""";

        var payload = new
        {
            subscriptions = subscriptionIds,
            query
        };

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                ResourceGraphUrl);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        request.Content =
            new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document =
            JsonDocument.Parse(body);

        var result =
            new List<VirtualMachineInventoryItem>();

        if (!document.RootElement.TryGetProperty(
                "data",
                out var data))
        {
            return result;
        }

        foreach (var item in data.EnumerateArray())
        {
            result.Add(
                new VirtualMachineInventoryItem(
                    item.TryGetProperty("id", out var id)
                        ? id.GetString()
                        : null,
                    item.TryGetProperty("name", out var name)
                        ? name.GetString()
                        : null,
                    item.TryGetProperty(
                        "subscriptionId",
                        out var subscriptionId)
                        ? subscriptionId.GetString()
                        : null,
                    item.TryGetProperty(
                        "resourceGroup",
                        out var resourceGroup)
                        ? resourceGroup.GetString()
                        : null,
                    item.TryGetProperty(
                        "location",
                        out var location)
                        ? location.GetString()
                        : null,
                    item.TryGetProperty(
                        "vmSize",
                        out var vmSize)
                        ? vmSize.GetString()
                        : null,
                    item.TryGetProperty(
                        "osType",
                        out var osType)
                        ? osType.GetString()
                        : null
                )
            );
        }

        return result;
    }
}
