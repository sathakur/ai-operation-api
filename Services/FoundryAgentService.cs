using AIInventory.Api.Models;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Responses;

namespace AIInventory.Api.Services;

#pragma warning disable OPENAI001

public class FoundryAgentService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoundryAgentService> _logger;

    public FoundryAgentService(
        IConfiguration configuration,
        ILogger<FoundryAgentService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ChatResponse> RunAsync(
        string message,
        string correlationId,
        string username,
        CancellationToken cancellationToken = default)
    {
        var projectEndpoint =
            _configuration["Foundry:ProjectEndpoint"];

        var agentName =
            _configuration["Foundry:AgentName"];

        var agentVersion =
            _configuration["Foundry:AgentVersion"] ?? "";

        if (string.IsNullOrWhiteSpace(projectEndpoint))
        {
            throw new InvalidOperationException(
                "Foundry:ProjectEndpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new InvalidOperationException(
                "Foundry:AgentName is not configured.");
        }

        var credential =
            new DefaultAzureCredential();

        var projectClient =
            new AIProjectClient(
                endpoint: new Uri(projectEndpoint),
                tokenProvider: credential);

        var conversation =
            await projectClient
                .ProjectOpenAIClient
                .GetProjectConversationsClient()
                .CreateProjectConversationAsync(
                    cancellationToken: cancellationToken);

        var responsesClient =
            projectClient
                .ProjectOpenAIClient
                .GetProjectResponsesClientForAgent(
                    defaultAgent: agentName,
                    defaultConversationId: conversation.Value.Id);

        _logger.LogInformation(
            "Calling Foundry agent. CorrelationId={CorrelationId}, Agent={AgentName}, ConfiguredVersion={AgentVersion}, User={User}",
            correlationId,
            agentName,
            agentVersion,
            username);

        var response =
            await responsesClient.CreateResponseAsync(
                message,
                cancellationToken: cancellationToken);

        var answer =
            response.Value.GetOutputText();

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer =
                "The Foundry agent returned no text response.";
        }

        return new ChatResponse
        {
            Answer = answer,
            CorrelationId = correlationId,
            User = username,
            AgentName = agentName,
            AgentVersion = agentVersion
        };
    }
}

#pragma warning restore OPENAI001
