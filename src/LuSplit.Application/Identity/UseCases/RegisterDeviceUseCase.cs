using System.Security.Cryptography;
using LuSplit.Application.Identity.Ports;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Application.Identity.UseCases;

/// <summary>
/// Registers the current device with the control plane:
/// 1. Generates a UUID device ID.
/// 2. Generates an RSA keypair.
/// 3. Stores the private key in secure storage.
/// 4. Posts the public key to the control plane.
/// Second registration for the same device ID is idempotent (same deviceId returned).
/// </summary>
public sealed class RegisterDeviceUseCase
{
    private readonly IDeviceRegistrationPort _registrationPort;
    private readonly ISecureKeyStoragePort _keyStorage;

    public RegisterDeviceUseCase(
        IDeviceRegistrationPort registrationPort,
        ISecureKeyStoragePort keyStorage)
    {
        _registrationPort = registrationPort;
        _keyStorage = keyStorage;
    }

    public async Task<RegisterDeviceResult> ExecuteAsync(
        string deviceName,
        string platform,
        CancellationToken ct = default)
    {
        var deviceId = Guid.NewGuid().ToString("N");

        using var rsa = RSA.Create(2048);
        var privateKeyBytes = rsa.ExportRSAPrivateKey();
        var publicKeyBytes = rsa.ExportRSAPublicKey();

        await _keyStorage.StorePrivateKeyAsync(deviceId, privateKeyBytes, ct);

        var request = new RegisterDeviceRequest(deviceId, deviceName, platform, publicKeyBytes);
        var response = await _registrationPort.RegisterDeviceAsync(request, ct);

        return new RegisterDeviceResult(response.DeviceId, publicKeyBytes);
    }
}

public sealed record RegisterDeviceResult(string DeviceId, byte[] PublicKey);
