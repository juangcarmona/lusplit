using System.Text.Json;

namespace LuSplit.Infrastructure.ControlPlane;

/// <summary>
/// Shared JSON serializer options for all control-plane adapters.
/// Uses <see cref="JsonSerializerDefaults.Web"/> (camelCase, case-insensitive)
/// to match the server-side function endpoints.
/// </summary>
internal static class ControlPlaneJsonOptions
{
    public static readonly JsonSerializerOptions Value = new(JsonSerializerDefaults.Web);
}
