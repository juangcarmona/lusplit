namespace LuSplit.Application.Shared.Ports;

/// <summary>
/// Abstracts the platform system share sheet so invitation sharing
/// is testable without MAUI runtime dependencies.
/// </summary>
public interface IShareSheetPort
{
    /// <summary>
    /// Opens the system share sheet with the given text content.
    /// Returns true if the share completed successfully, false if cancelled or failed.
    /// </summary>
    Task<bool> ShareTextAsync(string title, string text, CancellationToken ct = default);
}
