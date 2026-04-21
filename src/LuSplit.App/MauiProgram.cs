using LuSplit.App.Features.Activity;
using LuSplit.App.Features.Auth;
using LuSplit.App.Features.Devices;
using LuSplit.App.Features.Expenses.AddExpense;
using LuSplit.App.Features.Expenses.ExpenseDetails;
using LuSplit.App.Features.Groups.ArchivedGroups;
using LuSplit.App.Features.Groups.ArchivedGroupView;
using LuSplit.App.Features.Groups.GroupDetails;
using LuSplit.App.Features.Groups.GroupSwitcher;
using LuSplit.App.Features.Groups.GroupTimeline;
using LuSplit.App.Features.Home.Home;
using LuSplit.App.Features.Invitations;
using LuSplit.App.Features.Members;
using LuSplit.App.Features.Payments.RecordPayment;
using LuSplit.App.Features.Payments.Settlement;
using LuSplit.App.Features.Settings.Settings;
using LuSplit.App.Features.SharedGroups;
using LuSplit.App.Services;
using LuSplit.App.Services.Localization;
using LuSplit.App.Services.Persistence;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Groups.Queries;
using LuSplit.Application.Invitations.Queries;
using LuSplit.Application.KeyManagement.Ports;
using LuSplit.Application.KeyManagement.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Infrastructure.ControlPlane;
using LuSplit.Infrastructure.Crypto;
using LuSplit.Infrastructure.Groups;
using LuSplit.Infrastructure.Sync;
using Microsoft.Extensions.Logging;
using Plugin.MauiMtAdmob;

namespace LuSplit.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        LocalizationHelper.ApplyPersistedLanguage();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMTAdmob()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "BrandRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "BrandMedium");
                fonts.AddFont("OpenSans-Semibold.ttf", "BrandSemiBold");
            });

        builder.Services.AddSingleton<AppDataService>();
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<GroupSwitcherPage>();
        builder.Services.AddTransient<GroupPage>();
        builder.Services.AddTransient<GroupDetailsPage>();
        builder.Services.AddTransient<AddExpensePage>();
        builder.Services.AddTransient<ExpenseDetailsPage>();
        builder.Services.AddTransient<SettlementPage>();
        builder.Services.AddTransient<RecordPaymentPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ArchivedGroupsPage>();
        builder.Services.AddTransient<ArchivedGroupViewPage>();
        builder.Services.AddTransient<InvitationLandingPage>();
        builder.Services.AddTransient<AuthenticationPage>();

        // Invitation + Auth ViewModels
        builder.Services.AddTransient<AuthenticationViewModel>(sp =>
            new AuthenticationViewModel(sp.GetRequiredService<LuSplit.Application.Shared.Ports.IAuthPort>()));
        builder.Services.AddTransient<InvitationLandingViewModel>(sp =>
            new InvitationLandingViewModel(
                sp.GetRequiredService<LuSplit.Application.Invitations.UseCases.AcceptInvitationUseCase>(),
                sp.GetRequiredService<LuSplit.Application.Invitations.UseCases.DeclineInvitationUseCase>(),
                sp.GetRequiredService<LuSplit.Application.Shared.Ports.IAuthPort>(),
                DeviceInfo.Current.Idiom.ToString()));

        // Sync infrastructure
        builder.Services.AddSingleton<AesGcmEncryptionAdapter>();
        builder.Services.AddSingleton<RsaKeyWrapAdapter>();
        builder.Services.AddSingleton<SecureKeyStorageAdapter>();
        builder.Services.AddSingleton<GroupKeyProvider>(sp =>
            new GroupKeyProvider(
                sp.GetRequiredService<SecureKeyStorageAdapter>(),
                sp.GetRequiredService<RsaKeyWrapAdapter>()));
        builder.Services.AddSingleton<ControlPlaneHttpClient>(sp =>
        {
            var httpClient = new HttpClient();
            // Auth token provider — wired up when IAuthPort is registered in a later phase.
            return new ControlPlaneHttpClient(httpClient, _ => Task.FromResult<string?>(null));
        });
        builder.Services.AddSingleton<SasTokenProvider>();
        builder.Services.AddSingleton<BlobSyncAdapter>();
        builder.Services.AddSingleton<SyncOrchestrationService>(sp =>
        {
            var dataService = sp.GetRequiredService<AppDataService>();
            var syncPort = sp.GetRequiredService<BlobSyncAdapter>();
            var encryption = sp.GetRequiredService<AesGcmEncryptionAdapter>();
            var keyProvider = sp.GetRequiredService<GroupKeyProvider>();
            var deviceId = DeviceInfo.Current.Idiom.ToString();
            return new SyncOrchestrationService(
                () => dataService.BuildSyncGroupUseCaseAsync(syncPort, encryption, keyProvider),
                deviceId);
        });

        // Control plane adapters
        builder.Services.AddSingleton<LuSplit.Application.Identity.Ports.IDeviceRegistrationPort, LuSplit.Infrastructure.ControlPlane.DeviceRegistrationAdapter>();
        builder.Services.AddSingleton<LuSplit.Application.Groups.Ports.IGroupRegistrationPort, LuSplit.Infrastructure.ControlPlane.GroupRegistrationAdapter>();
        builder.Services.AddSingleton<LuSplit.Application.Invitations.Ports.IInvitationPort, LuSplit.Infrastructure.ControlPlane.InvitationAdapter>();
        builder.Services.AddSingleton<LuSplit.Application.Revocation.Ports.IRevocationPort, LuSplit.Infrastructure.ControlPlane.MemberRevocationAdapter>();

        // Key management
        builder.Services.AddSingleton<IKeyWrapPort>(sp => sp.GetRequiredService<RsaKeyWrapAdapter>());
        builder.Services.AddSingleton<IKeyRotationPort, KeyRotationAdapter>();
        builder.Services.AddSingleton<IEncryptionPort>(sp => sp.GetRequiredService<AesGcmEncryptionAdapter>());
        builder.Services.AddTransient<RotateGroupKeyUseCase>();

        // Group membership (requires IGroupMembershipRepository from AppDataService — wired via factory)

        // Conflict + activity
        builder.Services.AddSingleton<ConflictFlagStore>();
        builder.Services.AddTransient<Features.Expenses.ConflictReviewPromptViewModel>();
        builder.Services.AddTransient<ActivityFeedViewModel>();

        // New pages
        builder.Services.AddTransient<ShareGroupPage>();
        builder.Services.AddTransient<ShareGroupViewModel>();
        builder.Services.AddTransient<MemberListPage>();
        builder.Services.AddTransient<MemberListViewModel>();
        builder.Services.AddTransient<DeviceManagementPage>();
        builder.Services.AddTransient<DeviceManagementViewModel>();
        builder.Services.AddTransient<ActivityFeedPage>();

        return builder.Build();
    }
}