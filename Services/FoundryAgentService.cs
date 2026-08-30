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

    private const int MaxToolRounds = 6;

    public FoundryAgentService(
        IConfiguration configuration,
        ILogger<FoundryAgentService> logger,
        AzureAgentToolService toolService)
    {
        _configuration = configuration;
        _logger = logger;
        _toolService = toolService;
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

        /*
         * IMPORTANT:
         *
         * Function tools are declared on the persisted Foundry Agent version.
         * Do NOT add options.Tools here.
         *
         * Foundry returns FunctionCallResponseItem objects when a tool is needed.
         * The ASP.NET backend executes only the approved tool implementation and
         * returns the output by call ID.
         *
         * The model never receives ARM tokens or the Function key.
         */
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

        for (var round = 1; round <= MaxToolRounds; round++)
        {
            var options =
                new CreateResponseOptions
                {
                    PreviousResponseId = previousResponseId
                };

            foreach (var item in inputItems)
            {
                options.InputItems.Add(item);
            }

            /*
             * DO NOT ADD options.Tools.
             * The stored Foundry Agent version owns the tool definitions.
             */

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
                /*
                 * Preserve the response item in the continuation request.
                 * This keeps the function-call context and call ID.
                 */
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

                try
                {
                    toolOutput =
                        await _toolService.ExecuteAsync(
                            functionCall.FunctionName,
                            cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Inventory tool {ToolName} failed. CorrelationId={CorrelationId}",
                        functionCall.FunctionName,
                        correlationId);

                    /*
                     * Never convert retrieval failure into an empty inventory.
                     */
                    toolOutput =
                        """
                        {
                          "error": "Inventory retrieval failed.",
                          "instruction": "Do not infer that the count is zero and do not claim that no Azure resources exist."
                        }
                        """;
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

        /*
         * If MaxToolRounds is reached while the model is still asking for
         * functions, fail explicitly instead of returning fabricated text.
         */
        var unresolvedCalls =
            finalResponse.OutputItems
                .OfType<FunctionCallResponseItem>()
                .Any();

        if (unresolvedCalls)
        {
            throw new InvalidOperationException(
                $"Foundry exceeded the maximum tool-call rounds ({MaxToolRounds}).");
        }

        var answer =
            finalResponse.GetOutputText();

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer =
                "I could not produce a final answer from the Azure inventory data.";
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
