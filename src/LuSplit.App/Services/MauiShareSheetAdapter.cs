using LuSplit.Application.Shared.Ports;

namespace LuSplit.App.Services;

/// <summary>
/// MAUI implementation of <see cref="IShareSheetPort"/> using <see cref="Share.RequestAsync"/>.
/// </summary>
public sealed class MauiShareSheetAdapter : IShareSheetPort
{
    public async Task<bool> ShareTextAsync(string title, string text, CancellationToken ct = default)
    {
        try
        {
            await Share.RequestAsync(new ShareTextRequest
            {
                Title = title,
                Text = text,
            });
            // MAUI Share.RequestAsync does not reliably report cancellation on all platforms.
            // We treat completion without exception as success.
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }
}
