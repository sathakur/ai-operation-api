FOUNDY TOOL-CALLING INTEGRATION
===============================

Purpose
-------
Wire the existing Microsoft Foundry agent to the central inventory Function App.

Flow
----
Frontend
  -> authenticated ai-operation-api
  -> Foundry decides which approved tool is needed
  -> ASP.NET executes AzureAgentToolService
  -> FunctionInventoryService
  -> Azure Function
  -> Function Managed Identity
  -> Azure Resource Graph
  -> tool output back to Foundry
  -> final natural-language answer

Files in this package
---------------------
Program.cs
Services/AzureAgentToolService.cs
Services/FoundryAgentService.cs

Prerequisite
------------
The previous central inventory integration must already exist:
Services/FunctionInventoryService.cs

Required App Service settings
-----------------------------
FunctionInventory__BaseUrl=https://func-ai-operation-inventory-c0btabdee8g0bge7.westeurope-01.azurewebsites.net
FunctionInventory__Key=<secret function key>

Existing Foundry settings remain:
Foundry__ProjectEndpoint=<project endpoint>
Foundry__AgentName=azure-operations-agent
Foundry__AgentVersion=4

Security
--------
- Function key stays in App Service settings only.
- Foundry never receives the Function key.
- Foundry never receives an ARM access token.
- No arbitrary KQL is accepted.
- Only approved strongly typed tools can execute.
- A tool failure is returned as an error and must not be interpreted as zero resources.

Approved tools
--------------
get_subscriptions
get_vms
get_vm_count
get_resource_groups
get_resource_summary
get_subnets

Copy steps
----------
1. Back up current files.
2. Replace Program.cs with this package's Program.cs.
3. Replace Services/AzureAgentToolService.cs.
4. Replace Services/FoundryAgentService.cs.
5. Keep Services/FunctionInventoryService.cs from the previous integration.
6. Commit and push.
7. Let GitHub Actions build/deploy.
8. Test /health.
9. Test direct /api/central-inventory/subscriptions.
10. Test Foundry UI:
    - "show me subscription names"
    - "total vm count"
    - "show me resource groups"

If GitHub Actions fails to compile, stop at the FIRST compiler error and correct that error before changing anything else.
