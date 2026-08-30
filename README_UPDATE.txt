Foundry Agent backend update

NEW:
Controllers/ChatController.cs
Models/ChatRequest.cs
Models/ChatResponse.cs
Services/AzureAgentToolService.cs
Services/FoundryAgentService.cs

MODIFY:
Program.cs
appsettings.json
AIInventory.Api.csproj

KEEP YOUR EXISTING:
Controllers/InventoryController.cs
Services/AzureInventoryService.cs
existing inventory model files

App Service settings:
Foundry__ProjectEndpoint=https://meta-instruct.services.ai.azure.com/api/projects/MetaProject
Foundry__AgentName=azure-operations-agent
Foundry__AgentVersion=4

Do not put secrets in appsettings.json.
