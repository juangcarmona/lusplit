namespace LuSplit.Application.Shared.Ports;

/// <summary>
/// Abstracts clipboard access for testability.
/// </summary>
public interface IClipboardPort
{
    Task SetTextAsync(string text);
}
