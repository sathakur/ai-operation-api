using System.Text.Json;
using AIInventory.Api.Models;

namespace AIInventory.Api.Services;

public class ChatPresentationBuilder
{
    public ChatPresentation? Build(
        string? toolName,
        string? toolOutput)
    {
        if (string.IsNullOrWhiteSpace(toolName) ||
            string.IsNullOrWhiteSpace(toolOutput))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(toolOutput);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("error", out _))
            {
                return null;
            }

            return toolName switch
            {
                "get_subscriptions" =>
                    BuildSubscriptions(root),

                "get_vms" =>
                    BuildVirtualMachines(root),

                "get_vm_count" =>
                    BuildMetric(
                        root,
                        "Virtual Machines",
                        "virtual machines"),

                "get_resource_groups" =>
                    BuildResourceGroups(root),

                "get_resource_summary" =>
                    BuildResourceSummary(root),

                "get_subnets" =>
                    BuildSubnets(root),

                "get_resources" =>
                    BuildResources(root),

                "get_storage_accounts" =>
                    BuildStorageAccounts(root),

                "get_resource_count" =>
                    BuildMetric(
                        root,
                        "Azure Resources",
                        "Azure resources"),

                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ChatPresentation BuildSubscriptions(
        JsonElement root)
    {
        var rows = GetUniqueDataRows(
            root,
            "subscriptionId");

        var presentationRows =
            rows.Select(item =>
                Row(
                    ("Subscription", GetString(item, "subscriptionName")),
                    ("Subscription ID", GetString(item, "subscriptionId")),
                    ("State", GetString(item, "state"))
                ))
                .ToList();

        return Table(
            "Azure Subscriptions",
            $"{presentationRows.Count} subscriptions found.",
            new[]
            {
                "Subscription",
                "Subscription ID",
                "State"
            },
            presentationRows,
            presentationRows.Count);
    }

    private static ChatPresentation BuildVirtualMachines(
        JsonElement root)
    {
        var rows = GetUniqueDataRows(root, "id");

        var presentationRows =
            rows.Select(item =>
                Row(
                    ("Name", GetString(item, "vmName")),
                    ("Location", FormatLocation(GetString(item, "location"))),
                    ("Subscription", GetString(item, "subscriptionName")),
                    ("Resource Group", GetString(item, "resourceGroup")),
                    ("VM Size", GetString(item, "vmSize")),
                    ("OS", GetString(item, "osType")),
                    ("Power State", FormatPowerState(GetString(item, "powerState"))),
                    ("Private IP", GetString(item, "privateIp")),
                    ("Public IP", GetString(item, "publicIp"))
                ))
                .ToList();

        return Table(
            "Azure Virtual Machines",
            $"{presentationRows.Count} virtual machines found.",
            new[]
            {
                "Name",
                "Location",
                "Subscription",
                "Resource Group",
                "VM Size",
                "OS",
                "Power State",
                "Private IP",
                "Public IP"
            },
            presentationRows,
            presentationRows.Count);
    }

    private static ChatPresentation BuildStorageAccounts(
        JsonElement root)
    {
        var rows = GetUniqueDataRows(root, "id");

        var presentationRows =
            rows.Select(item =>
                Row(
                    ("Name", GetString(item, "name")),
                    ("Location", FormatLocation(GetString(item, "location"))),
                    ("Subscription", GetString(item, "subscriptionName")),
                    ("Resource Group", GetString(item, "resourceGroup"))
                ))
                .ToList();

        return Table(
            "Azure Storage Accounts",
            $"{presentationRows.Count} storage accounts found.",
            new[]
            {
                "Name",
                "Location",
                "Subscription",
                "Resource Group"
            },
            presentationRows,
            presentationRows.Count);
    }

    private static ChatPresentation BuildResourceGroups(
        JsonElement root)
    {
        var rows = GetUniqueDataRows(root, "id");

        var presentationRows =
            rows.Select(item =>
                Row(
                    ("Resource Group", GetString(item, "resourceGroup")),
                    ("Subscription", GetString(item, "subscriptionName")),
                    ("Location", FormatLocation(GetString(item, "location")))
                ))
                .ToList();

        return Table(
            "Azure Resource Groups",
            $"{presentationRows.Count} resource groups found.",
            new[]
            {
                "Resource Group",
                "Subscription",
                "Location"
            },
            presentationRows,
            presentationRows.Count);
    }

    private static ChatPresentation BuildSubnets(
        JsonElement root)
    {
        var rows = GetUniqueDataRows(root, "id");

        var presentationRows =
            rows.Select(item =>
                Row(
                    ("VNet", GetString(item, "vnetName")),
                    ("Subnet", GetString(item, "subnetName")),
                    ("Address Prefix", GetString(item, "addressPrefix")),
                    ("Location", FormatLocation(GetString(item, "location"))),
                    ("Subscription", GetString(item, "subscriptionName")),
                    ("Resource Group", GetString(item, "resourceGroup")),
                    ("NSG", GetString(item, "nsgName")),
                    ("Route Table", GetString(item, "routeTableName"))
                ))
                .ToList();

        return Table(
            "Azure Subnets",
            $"{presentationRows.Count} subnets found.",
            new[]
            {
                "VNet",
                "Subnet",
                "Address Prefix",
                "Location",
                "Subscription",
                "Resource Group",
                "NSG",
                "Route Table"
            },
            presentationRows,
            presentationRows.Count);
    }

    private static ChatPresentation BuildResources(
        JsonElement root)
    {
        var rows = GetUniqueDataRows(root, "id");

        var presentationRows =
            rows.Select(item =>
                Row(
                    ("Name", GetString(item, "name")),
                    ("Type", GetString(item, "type")),
                    ("Location", FormatLocation(GetString(item, "location"))),
                    ("Subscription", GetString(item, "subscriptionName")),
                    ("Resource Group", GetString(item, "resourceGroup"))
                ))
                .ToList();

        return Table(
            "Azure Resources",
            $"{presentationRows.Count} resources returned.",
            new[]
            {
                "Name",
                "Type",
                "Location",
                "Subscription",
                "Resource Group"
            },
            presentationRows,
            presentationRows.Count);
    }

    private static ChatPresentation BuildResourceSummary(
        JsonElement root)
    {
        var rows = GetDataRows(root);
        var presentationRows =
            new List<Dictionary<string, object?>>();

        long totalResources = 0;

        foreach (var item in rows)
        {
            var resourceType =
                GetString(item, "type");

            var count =
                GetInt64(item, "resourceCount");

            totalResources += count;

            presentationRows.Add(
                Row(
                    ("Resource Type", resourceType),
                    ("Count", count)
                ));
        }

        return Table(
            "Azure Resource Summary",
            $"{totalResources} Azure resources found across {presentationRows.Count} resource types.",
            new[]
            {
                "Resource Type",
                "Count"
            },
            presentationRows,
            totalResources);
    }

    private static ChatPresentation BuildMetric(
        JsonElement root,
        string title,
        string noun)
    {
        var count = GetInt64(root, "count");

        return new ChatPresentation
        {
            ResponseType = "metric",
            Title = title,
            Summary = $"{count} {noun} found.",
            Total = count
        };
    }

    private static ChatPresentation Table(
        string title,
        string summary,
        IEnumerable<string> columns,
        List<Dictionary<string, object?>> rows,
        long total)
    {
        return new ChatPresentation
        {
            ResponseType = "table",
            Title = title,
            Summary = summary,
            Columns = columns.ToList(),
            Rows = rows,
            Total = total
        };
    }

    private static Dictionary<string, object?> Row(
        params (string Key, object? Value)[] values)
    {
        var row =
            new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in values)
        {
            row[key] = value;
        }

        return row;
    }

    private static List<JsonElement> GetDataRows(
        JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return new List<JsonElement>();
        }

        return data
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToList();
    }

    private static List<JsonElement> GetUniqueDataRows(
        JsonElement root,
        string preferredIdentityProperty)
    {
        var rows = GetDataRows(root);

        var seen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var unique =
            new List<JsonElement>();

        foreach (var item in rows)
        {
            var identity =
                GetString(
                    item,
                    preferredIdentityProperty);

            if (string.IsNullOrWhiteSpace(identity))
            {
                identity = item.GetRawText();
            }

            if (seen.Add(identity))
            {
                unique.Add(item);
            }
        }

        return unique;
    }

    private static string GetString(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(
                propertyName,
                out var value))
        {
            return "";
        }

        return value.ValueKind switch
        {
            JsonValueKind.String =>
                value.GetString() ?? "",

            JsonValueKind.Number =>
                value.ToString(),

            JsonValueKind.True =>
                "true",

            JsonValueKind.False =>
                "false",

            _ =>
                value.ToString()
        };
    }

    private static long GetInt64(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(
                propertyName,
                out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                out number))
        {
            return number;
        }

        return 0;
    }

    private static string FormatPowerState(
        string powerState)
    {
        if (string.IsNullOrWhiteSpace(powerState))
        {
            return "";
        }

        const string prefix = "PowerState/";

        if (powerState.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase))
        {
            powerState =
                powerState[prefix.Length..];
        }

        return powerState switch
        {
            "running" => "Running",
            "stopped" => "Stopped",
            "deallocated" => "Deallocated",
            "starting" => "Starting",
            "stopping" => "Stopping",
            "deallocating" => "Deallocating",
            _ => powerState
        };
    }

    private static string FormatLocation(
        string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return "";
        }

        return location.ToLowerInvariant() switch
        {
            "eastus" => "East US",
            "eastus2" => "East US 2",
            "westus" => "West US",
            "westus2" => "West US 2",
            "westus3" => "West US 3",
            "centralus" => "Central US",
            "northcentralus" => "North Central US",
            "southcentralus" => "South Central US",
            "westcentralus" => "West Central US",
            "northeurope" => "North Europe",
            "westeurope" => "West Europe",
            "uksouth" => "UK South",
            "ukwest" => "UK West",
            "eastasia" => "East Asia",
            "southeastasia" => "Southeast Asia",
            "centralindia" => "Central India",
            "southindia" => "South India",
            "westindia" => "West India",
            "australiaeast" => "Australia East",
            "australiasoutheast" => "Australia Southeast",
            _ => location
        };
    }
}
