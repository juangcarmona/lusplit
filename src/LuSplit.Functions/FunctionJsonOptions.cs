using System.Text.Json;

namespace LuSplit.Functions;

/// <summary>
/// Shared JSON serializer options for all function endpoints.
/// Uses <see cref="JsonSerializerDefaults.Web"/> to match the camelCase
/// naming convention used by <c>JsonContent.Create()</c> in .NET 10+.
/// </summary>
internal static class FunctionJsonOptions
{
    public static readonly JsonSerializerOptions Value = new(JsonSerializerDefaults.Web);
}
