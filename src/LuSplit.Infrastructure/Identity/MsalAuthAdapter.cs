using LuSplit.Application.Shared.Ports;
using Microsoft.Identity.Client;

namespace LuSplit.Infrastructure.Identity;

/// <summary>
/// MSAL-backed implementation of <see cref="IAuthPort"/>.
/// Uses <see cref="IPublicClientApplication"/> for interactive and silent token acquisition.
/// </summary>
public sealed class MsalAuthAdapter : IAuthPort
{
    private readonly IPublicClientApplication _pca;
    private readonly string[] _scopes;

    private IAccount? _currentAccount;

    public MsalAuthAdapter(IPublicClientApplication pca, string[] scopes)
    {
        _pca = pca;
        _scopes = scopes;
    }

    public async Task SignInAsync(CancellationToken ct)
    {
        var result = await _pca.AcquireTokenInteractive(_scopes)
            .ExecuteAsync(ct);
        _currentAccount = result.Account;
    }

    public async Task SignOutAsync(CancellationToken ct)
    {
        if (_currentAccount is not null)
        {
            await _pca.RemoveAsync(_currentAccount);
            _currentAccount = null;
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        try
        {
            var accounts = await _pca.GetAccountsAsync();
            var account = _currentAccount ?? accounts.FirstOrDefault();
            if (account is null)
                return null;

            var result = await _pca.AcquireTokenSilent(_scopes, account)
                .ExecuteAsync(ct);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    public async Task<string?> GetCurrentUserIdAsync(CancellationToken ct)
    {
        if (_currentAccount is not null)
            return _currentAccount.HomeAccountId.Identifier;

        var accounts = await _pca.GetAccountsAsync();
        return accounts.FirstOrDefault()?.HomeAccountId.Identifier;
    }
}
