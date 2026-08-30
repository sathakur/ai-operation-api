$ErrorActionPreference = "Stop"

$ProjectEndpoint = "https://meta-instruct.services.ai.azure.com/api/projects/MetaProject"
$AgentName = "azure-operations-agent"
$SourceVersion = "5"
$ApiVersion = "v1"

Write-Host "Getting Microsoft Foundry access token..."

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

$sourceUri =
    $ProjectEndpoint +
    "/agents/" +
    $AgentName +
    "/versions/" +
    $SourceVersion +
    "?api-version=" +
    $ApiVersion

Write-Host "Source URI: $sourceUri"
Write-Host "Reading agent version $SourceVersion..."

$Existing =
    Invoke-RestMethod `
        -Method GET `
        -Uri $sourceUri `
        -Headers $Headers

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

$CombinedInstructions = $MandatoryInstructions

if (-not [string]::IsNullOrWhiteSpace($OldInstructions)) {
    $CombinedInstructions +=
        "`n`nAdditional existing guidance:`n" +
        $OldInstructions
}

$Definition |
    Add-Member `
        -NotePropertyName instructions `
        -NotePropertyValue $CombinedInstructions `
        -Force

function New-EmptyTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

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
    (New-EmptyTool -Name "get_subscriptions" -Description "Retrieve real Azure subscription inventory."),
    (New-EmptyTool -Name "get_vms" -Description "Retrieve real Azure virtual machine inventory."),
    (New-EmptyTool -Name "get_vm_count" -Description "Retrieve the real total Azure VM count."),
    (New-EmptyTool -Name "get_resource_groups" -Description "Retrieve real Azure resource group inventory."),
    (New-EmptyTool -Name "get_resource_summary" -Description "Retrieve real Azure resource counts grouped by resource type. Must be used for resource summary requests."),
    (New-EmptyTool -Name "get_subnets" -Description "Retrieve real Azure subnet inventory."),
    (New-EmptyTool -Name "get_resources" -Description "Retrieve general Azure resource inventory."),
    (New-EmptyTool -Name "get_storage_accounts" -Description "Retrieve real Azure Storage Account inventory. Must be used for storage account requests."),
    (New-EmptyTool -Name "get_resource_count" -Description "Retrieve the real total Azure resource count.")
)

$Definition |
    Add-Member `
        -NotePropertyName tools `
        -NotePropertyValue $Tools `
        -Force

# IMPORTANT:
# When creating a NEW VERSION of an EXISTING agent, post to:
# /agents/{agentName}/versions
# Do NOT post to /agents, because that tries to create a brand-new agent
# and returns "Agent already exists".
$Body = @{
    description =
        "Read-only Azure operations agent with grounded inventory tools."
    definition =
        $Definition
} | ConvertTo-Json -Depth 100

$newVersionUri =
    $ProjectEndpoint +
    "/agents/" +
    $AgentName +
    "/versions?api-version=" +
    $ApiVersion

Write-Host "Create version URI: $newVersionUri"
Write-Host "Creating new agent version..."

$Created =
    Invoke-RestMethod `
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
