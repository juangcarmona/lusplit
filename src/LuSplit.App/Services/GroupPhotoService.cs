namespace LuSplit.App.Services;

/// <summary>
/// Handles group-photo file I/O: picking/capturing via MediaPicker, copying to the
/// app's group_images directory, and delegating persistence to <see cref="AppDataService"/>.
/// </summary>
internal sealed class GroupPhotoService
{
    private readonly AppDataService _dataService;

    public GroupPhotoService(AppDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>
    /// Lets the user pick or capture a photo and saves it to the app's group images directory.
    /// Returns the destination path, or <c>null</c> if the user cancelled.
    /// </summary>
    public async Task<string?> PickAndSaveAsync(string groupId, bool fromCamera)
    {
        FileResult? result = fromCamera
            ? await MediaPicker.Default.CapturePhotoAsync()
            : await MediaPicker.Default.PickPhotoAsync();

        if (result is null)
            return null;

        var dir = Path.Combine(FileSystem.AppDataDirectory, "group_images");
        Directory.CreateDirectory(dir);
        var destPath = Path.Combine(dir, $"{groupId}.jpg");

        await using (var src = await result.OpenReadAsync())
        await using (var dst = File.OpenWrite(destPath))
        {
            await src.CopyToAsync(dst);
        }

        await _dataService.SaveGroupImageAsync(groupId, destPath);
        return destPath;
    }

    /// <summary>
    /// Deletes the on-disk photo file (if it exists) and clears the stored path in the data service.
    /// </summary>
    public async Task RemoveAsync(string groupId, string? currentImagePath)
    {
        if (!string.IsNullOrEmpty(currentImagePath) && File.Exists(currentImagePath))
            File.Delete(currentImagePath);

        await _dataService.SaveGroupImageAsync(groupId, null);
    }
}
