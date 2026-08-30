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

        /*
         * We intentionally do not give the model:
         * - ARM tokens
         * - Function keys
         * - arbitrary KQL
         * - arbitrary PowerShell / CLI execution
         *
         * Foundry can only request one of the strongly-typed tools below.
         * The ASP.NET backend executes the tool and returns only its result.
         */
        var responsesClient =
            projectClient
                .ProjectOpenAIClient
                .GetProjectResponsesClientForAgent(
                    defaultAgent: agentName);

        _logger.LogInformation(
            "Calling Foundry agent. CorrelationId={CorrelationId}, Agent={AgentName}, ConfiguredVersion={AgentVersion}, User={User}",
            correlationId,
            agentName,
            agentVersion,
            username);

        var tools = CreateInventoryTools();

        List<ResponseItem> inputItems =
        [
            ResponseItem.CreateUserMessageItem(message)
        ];

        ResponseResult? finalResponse = null;

        for (var round = 1; round <= MaxToolRounds; round++)
        {
            var options = new CreateResponseOptions();

            foreach (var item in inputItems)
            {
                options.InputItems.Add(item);
            }

            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            var result =
                await responsesClient.CreateResponseAsync(
                    options,
                    cancellationToken);

            var response = result.Value;
            finalResponse = response;

            var toolCalls =
                response.OutputItems
                    .OfType<FunctionCallResponseItem>()
                    .ToList();

            if (toolCalls.Count == 0)
            {
                break;
            }

            /*
             * Add all model output items back to the next request so the
             * function-call items and their call IDs remain in context.
             */
            inputItems.AddRange(response.OutputItems);

            foreach (var toolCall in toolCalls)
            {
                _logger.LogInformation(
                    "Foundry requested tool {ToolName}. CorrelationId={CorrelationId}",
                    toolCall.FunctionName,
                    correlationId);

                string toolOutput;

                try
                {
                    toolOutput =
                        await _toolService.ExecuteAsync(
                            toolCall.FunctionName,
                            cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Inventory tool {ToolName} failed. CorrelationId={CorrelationId}",
                        toolCall.FunctionName,
                        correlationId);

                    /*
                     * Return an explicit tool error to the model.
                     * Do not turn a retrieval failure into "zero resources".
                     */
                    toolOutput =
                        """
                        {
                          "error": "Inventory retrieval failed. Do not infer that the resource count is zero and do not claim that no resources exist."
                        }
                        """;
                }

                inputItems.Add(
                    ResponseItem.CreateFunctionCallOutputItem(
                        toolCall.CallId,
                        toolOutput));
            }
        }

        if (finalResponse is null)
        {
            throw new InvalidOperationException(
                "Foundry returned no response.");
        }

        var answer =
            finalResponse.GetOutputText();

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer =
                "I could not produce a final answer from the inventory data.";
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

    private static IReadOnlyList<ResponseTool> CreateInventoryTools()
    {
        /*
         * These tools intentionally accept no arbitrary query text.
         * Each name maps to a fixed, approved backend operation.
         */

        return
        [
            ResponseTool.CreateFunctionTool(
                functionName: "get_subscriptions",
                functionDescription:
                    "Return the Azure subscriptions visible to the central inventory service. Use this for subscription names, subscription IDs, subscription count, or subscription state.",
                functionParameters: EmptyObjectSchema(),
                strictModeEnabled: true),

            ResponseTool.CreateFunctionTool(
                functionName: "get_vms",
                functionDescription:
                    "Return Azure virtual machine inventory. Use this when the user asks for VM names, VM details, VM locations, resource groups, sizes, operating systems, power state, or IP information.",
                functionParameters: EmptyObjectSchema(),
                strictModeEnabled: true),

            ResponseTool.CreateFunctionTool(
                functionName: "get_vm_count",
                functionDescription:
                    "Return the total count of Azure virtual machines visible to the central inventory service. Prefer this tool when the user asks only for the total VM count.",
                functionParameters: EmptyObjectSchema(),
                strictModeEnabled: true),

            ResponseTool.CreateFunctionTool(
                functionName: "get_resource_groups",
                functionDescription:
                    "Return Azure resource groups visible to the central inventory service.",
                functionParameters: EmptyObjectSchema(),
                strictModeEnabled: true),

            ResponseTool.CreateFunctionTool(
                functionName: "get_resource_summary",
                functionDescription:
                    "Return resource counts grouped by Azure resource type. Use this for inventory summaries and questions such as how many resources of each type exist.",
                functionParameters: EmptyObjectSchema(),
                strictModeEnabled: true),

            ResponseTool.CreateFunctionTool(
                functionName: "get_subnets",
                functionDescription:
                    "Return Azure subnet inventory visible to the central inventory service.",
                functionParameters: EmptyObjectSchema(),
                strictModeEnabled: true)
        ];
    }

    private static BinaryData EmptyObjectSchema()
    {
        return BinaryData.FromString(
            """
            {
              "type": "object",
              "properties": {},
              "required": [],
              "additionalProperties": false
            }
            """);
    }
}

#pragma warning restore OPENAI001
