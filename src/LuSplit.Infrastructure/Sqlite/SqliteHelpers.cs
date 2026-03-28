using System.Text.Json;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Infrastructure.Sqlite;

internal static class SplitJson
{
    public static string SerializeDefinition(SplitDefinition definition)
        => JsonSerializer.Serialize(ToDto(definition));

    public static JsonElement SerializeDefinitionToElement(SplitDefinition definition)
        => JsonSerializer.SerializeToElement(ToDto(definition));
    public static SplitDefinition ParseDefinition(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("components", out var componentsElement) || componentsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Invalid split definition JSON: missing components");
        }

        var components = new List<SplitComponent>();
        foreach (var componentElement in componentsElement.EnumerateArray())
        {
            var type = componentElement.GetProperty("type").GetString();
            if (string.Equals(type, "FIXED", StringComparison.Ordinal))
            {
                var shares = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (var property in componentElement.GetProperty("shares").EnumerateObject())
                {
                    shares[property.Name] = property.Value.GetInt64();
                }

                components.Add(new FixedSplitComponent(shares));
                continue;
            }

            var participants = componentElement.GetProperty("participants")
                .EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty)
                .ToArray();

            var mode = componentElement.GetProperty("mode").GetString() switch
            {
                "EQUAL" => RemainderMode.Equal,
                "WEIGHT" => RemainderMode.Weight,
                "PERCENT" => RemainderMode.Percent,
                var unknown => throw new InvalidOperationException($"Unknown split remainder mode: {unknown}")
            };

            Dictionary<string, string>? weights = null;
            if (componentElement.TryGetProperty("weights", out var weightsElement) && weightsElement.ValueKind == JsonValueKind.Object)
            {
                weights = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in weightsElement.EnumerateObject())
                {
                    weights[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            Dictionary<string, int>? percents = null;
            if (componentElement.TryGetProperty("percents", out var percentsElement) && percentsElement.ValueKind == JsonValueKind.Object)
            {
                percents = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var property in percentsElement.EnumerateObject())
                {
                    percents[property.Name] = property.Value.GetInt32();
                }
            }

            components.Add(new RemainderSplitComponent(participants, mode, weights, percents));
        }

        return new SplitDefinition(components);
    }

    private static object ToDto(SplitDefinition definition)
        => new
        {
            components = definition.Components.Select(ToComponentDto).ToArray()
        };

    private static object ToComponentDto(SplitComponent component)
        => component switch
        {
            FixedSplitComponent fixedComponent => new
            {
                type = "FIXED",
                shares = fixedComponent.Shares
            },
            RemainderSplitComponent remainderComponent => new
            {
                type = "REMAINDER",
                participants = remainderComponent.Participants,
                mode = remainderComponent.Mode switch
                {
                    RemainderMode.Equal => "EQUAL",
                    RemainderMode.Weight => "WEIGHT",
                    RemainderMode.Percent => "PERCENT",
                    _ => throw new ArgumentOutOfRangeException(nameof(remainderComponent.Mode), remainderComponent.Mode, "Unknown mode")
                },
                weights = remainderComponent.Weights,
                percents = remainderComponent.Percents
            },
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown split component")
        };
}

internal static class SqliteEnumConverters
{
    internal static ConsumptionCategory ParseConsumptionCategory(string value)
        => value switch
        {
            "FULL" => ConsumptionCategory.Full,
            "HALF" => ConsumptionCategory.Half,
            "CUSTOM" => ConsumptionCategory.Custom,
            _ => throw new InvalidOperationException($"Unknown consumption category: {value}")
        };

    internal static string ToConsumptionCategoryString(ConsumptionCategory category)
        => category switch
        {
            ConsumptionCategory.Full => "FULL",
            ConsumptionCategory.Half => "HALF",
            ConsumptionCategory.Custom => "CUSTOM",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown consumption category")
        };

    internal static TransferType ParseTransferType(string value)
        => value switch
        {
            "GENERATED" => TransferType.Generated,
            "MANUAL" => TransferType.Manual,
            _ => throw new InvalidOperationException($"Unknown transfer type: {value}")
        };

    internal static string ToTransferTypeString(TransferType value)
        => value switch
        {
            TransferType.Generated => "GENERATED",
            TransferType.Manual => "MANUAL",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown transfer type")
        };
}
