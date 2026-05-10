using LuSplit.Application.Shared.Ports;

namespace LuSplit.App.Services;

/// <summary>
/// MAUI implementation of <see cref="IClipboardPort"/>.
/// </summary>
public sealed class MauiClipboardAdapter : IClipboardPort
{
    public async Task SetTextAsync(string text)
    {
        try
        {
            await Clipboard.SetTextAsync(text);
        }
        catch
        {
            // Clipboard access can fail on some platforms — non-fatal
        }
    }
}
