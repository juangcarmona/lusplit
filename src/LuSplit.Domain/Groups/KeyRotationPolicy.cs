using LuSplit.Domain.Groups;

namespace LuSplit.Domain.Groups;

/// <summary>
/// Stateless rules governing group key rotation.
/// </summary>
public static class KeyRotationPolicy
{
    /// <summary>Key rotation is required whenever a member is revoked.</summary>
    public static bool IsRotationRequired(bool memberWasRevoked) => memberWasRevoked;

    /// <summary>Each new key version must be strictly greater than the current version.</summary>
    public static bool IsVersionMonotonic(int currentVersion, int newVersion) => newVersion > currentVersion;

    /// <summary>
    /// Validates that every non-revoked device has a wrapped key entry in the rotation set.
    /// </summary>
    public static bool AllActiveDevicesHaveKey(
        IReadOnlyList<string> activeDeviceIds,
        IReadOnlyList<WrappedKeyEntry> wrappedKeys)
    {
        var keyDevices = wrappedKeys.Select(k => k.DeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return activeDeviceIds.All(id => keyDevices.Contains(id));
    }
}
