namespace LuSplit.Application.Shared.Ports;

public sealed record SignedInUser(
    string UserId,
    string Username,
    string? DisplayName);

public interface IAuthPort
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct);
    Task SignInAsync(CancellationToken ct);
    Task SignOutAsync(CancellationToken ct);
    Task<string?> GetCurrentUserIdAsync(CancellationToken ct);
    Task<SignedInUser?> GetCurrentUserAsync(CancellationToken ct);
}
