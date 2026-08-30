using System.Text;
using System.Text.Json;

namespace AIInventory.Api.Services;

public class FunctionInventoryService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FunctionInventoryService> _logger;

    public FunctionInventoryService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FunctionInventoryService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<JsonElement> GetSubscriptionsAsync(CancellationToken ct = default) =>
        SendInventoryRequestAsync("listSubscriptions", null, ct);

    public Task<JsonElement> GetVmsAsync(int top = 1000, CancellationToken ct = default) =>
        SendInventoryRequestAsync("listVMDetails",
            new Dictionary<string, object?> { ["top"] = top }, ct);

    public Task<JsonElement> GetResourceGroupsAsync(int top = 1000, CancellationToken ct = default) =>
        SendInventoryRequestAsync("listResourceGroups",
            new Dictionary<string, object?> { ["top"] = top }, ct);

    public Task<JsonElement> GetSummaryAsync(CancellationToken ct = default) =>
        SendInventoryRequestAsync("summary", null, ct);

    public Task<JsonElement> GetSubnetsAsync(int top = 1000, CancellationToken ct = default) =>
        SendInventoryRequestAsync("listSubnets",
            new Dictionary<string, object?> { ["top"] = top }, ct);

    public Task<JsonElement> GetResourcesAsync(
        int top = 1000,
        string? resourceType = null,
        CancellationToken ct = default)
    {
        var filters = new Dictionary<string, object?> { ["top"] = top };

        if (!string.IsNullOrWhiteSpace(resourceType))
            filters["resourceType"] = resourceType;

        return SendInventoryRequestAsync("listResources", filters, ct);
    }

    public Task<JsonElement> GetStorageAccountsAsync(
        int top = 1000,
        CancellationToken ct = default) =>
        GetResourcesAsync(top, "microsoft.storage/storageaccounts", ct);

    private async Task<JsonElement> SendInventoryRequestAsync(
        string intent,
        Dictionary<string, object?>? filters,
        CancellationToken ct)
    {
        var baseUrl = _configuration["FunctionInventory:BaseUrl"]?.TrimEnd('/');
        var functionKey = _configuration["FunctionInventory:Key"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("FunctionInventory:BaseUrl is not configured.");

        if (string.IsNullOrWhiteSpace(functionKey))
            throw new InvalidOperationException("FunctionInventory:Key is not configured.");

        var requestBody = new Dictionary<string, object?> { ["intent"] = intent };
        if (filters is not null && filters.Count > 0)
            requestBody["filters"] = filters;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/inventory");
        request.Headers.Add("x-functions-key", functionKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        _logger.LogInformation(
            "Calling central inventory Function for intent {Intent}.",
            intent);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Central inventory Function failed for intent {Intent}. HTTP {StatusCode}.",
                intent,
                (int)response.StatusCode);

            throw new HttpRequestException(
                $"Central inventory Function returned HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
