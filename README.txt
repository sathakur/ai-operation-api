Replace these files in the backend repo:

AIInventory.Api.csproj
Services/FoundryAgentService.cs
Services/AzureAgentToolService.cs

Why:
1. Removes Microsoft.Agents.AI.Foundry preview package and AgentReference dependency.
2. Uses the current Azure AI Projects + ProjectOpenAIClient prompt-agent invocation pattern.
3. Uses DefaultAzureCredential so App Service managed identity is used in Azure.
4. Updates AzureAgentToolService to match the repository's actual AzureInventoryService methods:
   GetSubscriptionsAsync(CancellationToken)
   GetVirtualMachinesAsync(CancellationToken)
5. VM count is calculated from GetVirtualMachinesAsync.

No change is required to AzureInventoryService.cs for this build fix.
