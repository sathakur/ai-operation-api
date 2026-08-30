AI Operation API -> Function Inventory Integration
================================================

Files
-----
Program.cs
Services/FunctionInventoryService.cs
Controllers/CentralInventoryController.cs

Do NOT add the Function key to source code or GitHub.

Azure App Service settings
--------------------------
FunctionInventory__BaseUrl=https://func-ai-operation-inventory-c0btabdee8g0bge7.westeurope-01.azurewebsites.net
FunctionInventory__Key=<YOUR_FUNCTION_KEY>

BaseUrl must NOT contain /api/inventory and must NOT contain ?code=...

Backend test endpoints after deployment
---------------------------------------
GET /api/central-inventory/subscriptions
GET /api/central-inventory/vms
GET /api/central-inventory/resource-groups
GET /api/central-inventory/summary
GET /api/central-inventory/subnets
GET /api/central-inventory/resources

These endpoints remain protected by:
[Authorize]
[RequiredScope("Inventory.Read")]

Architecture
------------
Frontend -> Entra token -> App Service API -> x-functions-key ->
Inventory Function -> Function Managed Identity -> Azure Resource Graph
