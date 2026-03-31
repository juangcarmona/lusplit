using LuSplit.App.Resources.Localization;
using LuSplit.App.Services.Persistence;
using LuSplit.Application.Export.Models;

namespace LuSplit.App.Services.Export;

/// <summary>
/// Orchestrates the two-step export flow: format selection → delivery mode.
/// One shared generation pipeline with two distinct delivery modes:
/// save to device (platform-native) or share via the OS share sheet.
/// </summary>
public static class GroupExportService
{
    public static async Task RunExportFlowAsync(Page page, AppDataService dataService, string groupId)
    {
        // Step 1: pick format
        var formatOptions = new (string Label, ExportFormat Format)[]
        {
            (AppResources.Export_JsonOption, ExportFormat.Json),
            (AppResources.Export_CsvOption, ExportFormat.Csv),
            (AppResources.Export_PdfOption, ExportFormat.Pdf),
        };

        var formatChoice = await page.DisplayActionSheetAsync(
            AppResources.Export_DialogTitle,
            AppResources.Common_Cancel,
            null,
            formatOptions.Select(o => o.Label).ToArray());

        if (string.IsNullOrEmpty(formatChoice) || formatChoice == AppResources.Common_Cancel) return;

        var selectedFormat = Array.Find(formatOptions,
            o => string.Equals(o.Label, formatChoice, StringComparison.Ordinal));
        if (selectedFormat.Label is null) return;

        // Step 2: pick delivery mode
        var deliveryChoice = await page.DisplayActionSheetAsync(
            AppResources.Export_DeliveryDialogTitle,
            AppResources.Common_Cancel,
            null,
            AppResources.Export_SaveToDevice,
            AppResources.Export_Share);

        if (string.IsNullOrEmpty(deliveryChoice) || deliveryChoice == AppResources.Common_Cancel) return;

        // Generate file (format-agnostic delivery: same pipeline for both modes)
        ExportFileResult result;
        try
        {
            result = await dataService.ExportGroupAsync(groupId, selectedFormat.Format);
        }
        catch (Exception ex)
        {
            await page.DisplayAlertAsync(null, string.Format(AppResources.Export_Failed, ex.Message), AppResources.Common_Ok);
            return;
        }

        // Deliver
        if (string.Equals(deliveryChoice, AppResources.Export_SaveToDevice, StringComparison.Ordinal))
            await SaveToDeviceAsync(page, result);
        else
            await ShareAsync(result);
    }

    private static async Task ShareAsync(ExportFileResult result)
    {
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = AppResources.Export_ShareTitle,
            File = new ShareFile(result.FilePath, result.MimeType)
        });
    }

    private static async Task SaveToDeviceAsync(Page page, ExportFileResult result)
    {
        try
        {
#if ANDROID
            await SaveToAndroidDownloadsAsync(result);
            await page.DisplayAlertAsync(null,
                string.Format(AppResources.Export_SavedToDevice, result.FileName),
                AppResources.Common_Ok);
#elif WINDOWS
            await SaveToWindowsDownloadsAsync(result);
            await page.DisplayAlertAsync(null,
                string.Format(AppResources.Export_SavedToDevice, result.FileName),
                AppResources.Common_Ok);
#else
            // iOS / macOS: share sheet is the correct native channel to "Save to Files"
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = AppResources.Export_ShareTitle,
                File = new ShareFile(result.FilePath, result.MimeType)
            });
#endif
        }
        catch (Exception ex)
        {
            await page.DisplayAlertAsync(null,
                string.Format(AppResources.Export_Failed, ex.Message),
                AppResources.Common_Ok);
        }
    }

#if ANDROID
    private static async Task SaveToAndroidDownloadsAsync(ExportFileResult result)
    {
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
        {
            // API 29+: write via MediaStore - no WRITE_EXTERNAL_STORAGE permission needed
            var context = Android.App.Application.Context;
            var values = new Android.Content.ContentValues();
            values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, result.FileName);
            values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, result.MimeType);
            values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, "Download/");

            var uri = context.ContentResolver!.Insert(
                Android.Provider.MediaStore.Downloads.ExternalContentUri, values);
            if (uri is null)
                throw new InvalidOperationException("MediaStore: cannot create entry in Downloads.");

            using var outStream = context.ContentResolver.OpenOutputStream(uri)
                ?? throw new InvalidOperationException("MediaStore: cannot open output stream.");
            using var inStream = File.OpenRead(result.FilePath);
            await inStream.CopyToAsync(outStream);
        }
        else
        {
            // API 23-28: direct file copy - WRITE_EXTERNAL_STORAGE declared with maxSdkVersion="28"
            var downloadsDir = Android.OS.Environment
                .GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)!
                .AbsolutePath;
            var dest = Path.Combine(downloadsDir, result.FileName);
            await Task.Run(() => File.Copy(result.FilePath, dest, overwrite: true));
        }
    }
#endif

#if WINDOWS
    private static Task SaveToWindowsDownloadsAsync(ExportFileResult result)
    {
        var downloadsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var dest = Path.Combine(downloadsDir, result.FileName);
        File.Copy(result.FilePath, dest, overwrite: true);
        return Task.CompletedTask;
    }
#endif
}
