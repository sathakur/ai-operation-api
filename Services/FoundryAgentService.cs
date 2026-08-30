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
    private readonly AzureAgentToolService _toolService;
    private readonly ChatPresentationBuilder _presentationBuilder;

    private const int MaxToolRounds = 6;

    public FoundryAgentService(
        IConfiguration configuration,
        ILogger<FoundryAgentService> logger,
        AzureAgentToolService toolService,
        ChatPresentationBuilder presentationBuilder)
    {
        _configuration = configuration;
        _logger = logger;
        _toolService = toolService;
        _presentationBuilder = presentationBuilder;
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
            _configuration["Foundry:AgentVersion"];

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

        if (string.IsNullOrWhiteSpace(agentVersion))
        {
            throw new InvalidOperationException(
                "Foundry:AgentVersion is not configured.");
        }

        var credential =
            new DefaultAzureCredential();

        var projectClient =
            new AIProjectClient(
                endpoint: new Uri(projectEndpoint),
                tokenProvider: credential);

        var agentReference =
            new AgentReference(
                name: agentName,
                version: agentVersion);

        var responsesClient =
            projectClient
                .ProjectOpenAIClient
                .GetProjectResponsesClientForAgent(
                    agentReference);

        _logger.LogInformation(
            "Calling Foundry agent. CorrelationId={CorrelationId}, Agent={AgentName}, Version={AgentVersion}, User={User}",
            correlationId,
            agentName,
            agentVersion,
            username);

        var inputItems =
            new List<ResponseItem>
            {
                ResponseItem.CreateUserMessageItem(message)
            };

        string? previousResponseId = null;
        ResponseResult? finalResponse = null;

        // Capture the latest successful inventory tool result.
        // This is later converted into the fixed presentation contract.
        string? lastSuccessfulToolName = null;
        string? lastSuccessfulToolOutput = null;

        for (var round = 1; round <= MaxToolRounds; round++)
        {
            var options =
                new CreateResponseOptions
                {
                    PreviousResponseId =
                        previousResponseId
                };

            foreach (var item in inputItems)
            {
                options.InputItems.Add(item);
            }

            // Do NOT add options.Tools.
            // Tool definitions are owned by the persisted Foundry Agent version.

            var result =
                await responsesClient.CreateResponseAsync(
                    options,
                    cancellationToken);

            var response =
                result.Value;

            finalResponse =
                response;

            previousResponseId =
                response.Id;

            inputItems.Clear();

            var functionCalled =
                false;

            foreach (var responseItem in response.OutputItems)
            {
                inputItems.Add(responseItem);

                if (responseItem is not FunctionCallResponseItem functionCall)
                {
                    continue;
                }

                functionCalled =
                    true;

                _logger.LogInformation(
                    "Foundry requested inventory tool {ToolName}. CorrelationId={CorrelationId}, Round={Round}",
                    functionCall.FunctionName,
                    correlationId,
                    round);

                string toolOutput;
                var toolSucceeded = false;

                try
                {
                    toolOutput =
                        await _toolService.ExecuteAsync(
                            functionCall.FunctionName,
                            cancellationToken);

                    toolSucceeded = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Inventory tool {ToolName} failed. CorrelationId={CorrelationId}",
                        functionCall.FunctionName,
                        correlationId);

                    toolOutput =
                        """
                        {
                          "error": "Inventory retrieval failed.",
                          "instruction": "Do not infer that the count is zero and do not claim that no Azure resources exist."
                        }
                        """;
                }

                if (toolSucceeded)
                {
                    lastSuccessfulToolName =
                        functionCall.FunctionName;

                    lastSuccessfulToolOutput =
                        toolOutput;
                }

                inputItems.Add(
                    ResponseItem.CreateFunctionCallOutputItem(
                        functionCall.CallId,
                        toolOutput));
            }

            if (!functionCalled)
            {
                break;
            }
        }

        if (finalResponse is null)
        {
            throw new InvalidOperationException(
                "Foundry returned no response.");
        }

        var unresolvedCalls =
            finalResponse.OutputItems
                .OfType<FunctionCallResponseItem>()
                .Any();

        if (unresolvedCalls)
        {
            throw new InvalidOperationException(
                $"Foundry exceeded the maximum tool-call rounds ({MaxToolRounds}).");
        }

        var foundryAnswer =
            finalResponse.GetOutputText();

        var presentation =
            _presentationBuilder.Build(
                lastSuccessfulToolName,
                lastSuccessfulToolOutput);

        /*
         * Important:
         * For structured inventory responses, use the deterministic summary
         * generated from the actual tool data instead of displaying a second
         * free-form Markdown/list/table produced by the model.
         *
         * Foundry still decides which approved tool to call.
         * Azure data and displayed counts come from the tool result.
         */
        var answer =
            presentation?.Summary;

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer =
                foundryAnswer;
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer =
                "I could not produce a final answer from the Azure inventory data.";
        }

        return new ChatResponse
        {
            Answer = answer,
            Presentation = presentation,
            CorrelationId = correlationId,
            User = username,
            AgentName = agentName,
            AgentVersion = agentVersion
        };
    }
}

#pragma warning restore OPENAI001
