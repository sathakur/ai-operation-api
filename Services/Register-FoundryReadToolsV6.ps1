$ErrorActionPreference = "Stop"

$ProjectEndpoint = "https://meta-instruct.services.ai.azure.com/api/projects/MetaProject"
$AgentName = "azure-operations-agent"
$SourceVersion = "5"
$ApiVersion = "v1"

$Token = az account get-access-token `
    --scope "https://ai.azure.com/.default" `
    --query accessToken `
    -o tsv

if ([string]::IsNullOrWhiteSpace($Token)) {
    throw "Could not acquire Microsoft Foundry token."
}

$Headers = @{
    Authorization = "Bearer $Token"
    "Content-Type" = "application/json"
}

$sourceUri = "$ProjectEndpoint/agents/$AgentName/versions/$SourceVersion?api-version=$ApiVersion"

Write-Host "Reading agent version $SourceVersion..."
$Existing = Invoke-RestMethod -Method GET -Uri $sourceUri -Headers $Headers

if ($null -eq $Existing.definition) {
    throw "Existing agent response did not contain a definition."
}

$Definition = $Existing.definition

$MandatoryInstructions = @"
You are the read-only Azure Operations inventory assistant.

MANDATORY INVENTORY RULES:
1. Any question about actual Azure subscriptions, VMs, resource groups, subnets, storage accounts, resource counts, resource summaries, or existing resources MUST use an inventory tool before answering.
2. Never claim there are zero/no resources unless a successful tool result explicitly proves it.
3. Never invent names, counts, locations, states, IPs, types, or resource details.
4. If a tool fails, say inventory retrieval failed. Do not treat failure as an empty result.
5. Use get_resource_summary for resource summary requests.
6. Use get_storage_accounts for storage account requests.
7. Use get_resources for general resource inventory.
8. Use get_resource_count for total resource count.
9. Use get_vm_count for total VM count and get_vms for VM names/details.
10. Use get_subscriptions for subscription inventory.
11. Use get_resource_groups for resource groups.
12. Use get_subnets for subnets.
13. These tools are read-only. Never imply that an Azure resource was modified.
14. Final answers must be based only on successful tool output.
"@

$OldInstructions = ""
if ($null -ne $Definition.instructions) {
    $OldInstructions = [string]$Definition.instructions
}

$Combined = $MandatoryInstructions
if (-not [string]::IsNullOrWhiteSpace($OldInstructions)) {
    $Combined += "`n`nAdditional existing guidance:`n$OldInstructions"
}

$Definition | Add-Member -NotePropertyName instructions -NotePropertyValue $Combined -Force

function New-EmptyTool($Name, $Description) {
    return @{
        type = "function"
        name = $Name
        description = $Description
        parameters = @{
            type = "object"
            properties = @{}
            required = @()
            additionalProperties = $false
        }
        strict = $true
    }
}

$Tools = @(
    (New-EmptyTool "get_subscriptions" "Retrieve real Azure subscription inventory."),
    (New-EmptyTool "get_vms" "Retrieve real Azure virtual machine inventory."),
    (New-EmptyTool "get_vm_count" "Retrieve the real total Azure VM count."),
    (New-EmptyTool "get_resource_groups" "Retrieve real Azure resource group inventory."),
    (New-EmptyTool "get_resource_summary" "Retrieve real Azure resource counts grouped by resource type. Must be used for resource summary requests."),
    (New-EmptyTool "get_subnets" "Retrieve real Azure subnet inventory."),
    (New-EmptyTool "get_resources" "Retrieve general Azure resource inventory."),
    (New-EmptyTool "get_storage_accounts" "Retrieve real Azure Storage Account inventory. Must be used for storage account requests."),
    (New-EmptyTool "get_resource_count" "Retrieve the real total Azure resource count.")
)

$Definition | Add-Member -NotePropertyName tools -NotePropertyValue $Tools -Force

$Body = @{
    name = $AgentName
    description = "Read-only Azure operations agent with grounded inventory tools."
    definition = $Definition
} | ConvertTo-Json -Depth 100

$newVersionUri = "$ProjectEndpoint/agents?api-version=$ApiVersion"

Write-Host "Creating new agent version..."
$Created = Invoke-RestMethod `
    -Method POST `
    -Uri $newVersionUri `
    -Headers $Headers `
    -Body $Body

Write-Host ""
Write-Host "SUCCESS"
Write-Host "Agent name    : $($Created.name)"
Write-Host "Agent version : $($Created.version)"
Write-Host "Status        : $($Created.status)"
Write-Host ""
Write-Host "Set App Service variable:"
Write-Host "Foundry__AgentVersion=$($Created.version)"
