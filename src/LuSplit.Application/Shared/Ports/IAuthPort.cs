namespace LuSplit.Application.Shared.Ports;

public interface IAuthPort
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct);
    Task SignInAsync(CancellationToken ct);
    Task SignOutAsync(CancellationToken ct);
    Task<string?> GetCurrentUserIdAsync(CancellationToken ct);
}
