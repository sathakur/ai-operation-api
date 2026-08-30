using AIInventory.Api.Models;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI.Foundry;

namespace AIInventory.Api.Services;
public class FoundryAgentService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoundryAgentService> _logger;
    public FoundryAgentService(IConfiguration configuration, ILogger<FoundryAgentService> logger)
    { _configuration = configuration; _logger = logger; }

    public async Task<ChatResponse> RunAsync(string message, string correlationId, string username)
    {
        var endpoint = _configuration["Foundry:ProjectEndpoint"];
        var name = _configuration["Foundry:AgentName"];
        var version = _configuration["Foundry:AgentVersion"];
        if (string.IsNullOrWhiteSpace(endpoint)) throw new InvalidOperationException("Foundry:ProjectEndpoint is not configured.");
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Foundry:AgentName is not configured.");
        if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("Foundry:AgentVersion is not configured.");

        var projectClient = new AIProjectClient(new Uri(endpoint), new ManagedIdentityCredential());
        FoundryAgent agent = projectClient.AsAIAgent(new AgentReference(name, version));

        _logger.LogInformation("Calling Foundry agent. CorrelationId={CorrelationId}, Agent={Agent}, Version={Version}, User={User}",
            correlationId, name, version, username);

        var result = await agent.RunAsync(message);
        return new ChatResponse {
            Answer = result?.ToString() ?? "The Foundry agent returned no response.",
            CorrelationId = correlationId, User = username, AgentName = name, AgentVersion = version
        };
    }
}
