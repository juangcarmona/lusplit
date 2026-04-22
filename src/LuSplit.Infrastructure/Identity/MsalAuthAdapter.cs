using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace LuSplit.Infrastructure.Identity;

public sealed class MsalAuthAdapter : LuSplit.Application.Shared.Ports.IAuthPort
{
    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;
    private readonly Func<object?>? _parentWindowProvider;
    private AuthenticationResult? _lastResult;

    public MsalAuthAdapter(
        IPublicClientApplication app,
        string[] scopes,
        Func<object?>? parentWindowProvider = null)
    {
        _app = app;
        _scopes = scopes;
        _parentWindowProvider = parentWindowProvider;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var account = (await _app.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();
        if (account is null)
            return null;

        try
        {
            var result = await _app
                .AcquireTokenSilent(_scopes, account)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);

            _lastResult = result;
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        var builder = _app.AcquireTokenInteractive(_scopes);

        if (OperatingSystem.IsAndroid())
        {
            var parent = _parentWindowProvider?.Invoke()
                         ?? throw new InvalidOperationException(
                             "Android activity is not available for interactive sign-in.");

            builder = builder.WithParentActivityOrWindow(parent);
        }

        var result = await builder
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        _lastResult = result;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        foreach (var account in accounts)
        {
            await _app.RemoveAsync(account).ConfigureAwait(false);
        }

        _lastResult = null;
    }

    public async Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        if (_lastResult?.Account?.HomeAccountId?.Identifier is { Length: > 0 } id)
            return id;

        var account = (await _app.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();
        return account?.HomeAccountId?.Identifier;
    }

    public async Task<LuSplit.Application.Shared.Ports.SignedInUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var account = _lastResult?.Account
            ?? (await _app.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault();

        if (account is null)
            return null;

        var userId = account.HomeAccountId.Identifier;
        var username = account.Username;
        // ClaimsPrincipal is not available on IAccount; use the preferred_username claim
        // from the last authentication result if present, otherwise fall back to username.
        var displayName = _lastResult?.ClaimsPrincipal
            ?.FindFirst("name")
            ?.Value
            ?? _lastResult?.ClaimsPrincipal
            ?.FindFirst("preferred_username")
            ?.Value;

        return new LuSplit.Application.Shared.Ports.SignedInUser(userId, username, displayName);
    }
}