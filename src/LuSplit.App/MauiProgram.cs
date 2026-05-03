using LuSplit.App.Features.Activity;
using LuSplit.App.Features.Auth;
using LuSplit.App.Features.Devices;
using LuSplit.App.Services;
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
using LuSplit.Application.Revocation.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Infrastructure.ControlPlane;
using LuSplit.Infrastructure.Crypto;
using LuSplit.Infrastructure.Groups;
using LuSplit.Infrastructure.Identity;
using LuSplit.Infrastructure.Sync;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
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

        // Register device use case (needed for post-sign-in device registration)
        builder.Services.AddTransient<LuSplit.Application.Identity.UseCases.RegisterDeviceUseCase>();

        // Session service — single app-level source of truth for auth state
        builder.Services.AddSingleton<SessionService>();

        // Invitation + Auth ViewModels
        builder.Services.AddTransient<AuthenticationViewModel>(sp =>
            new AuthenticationViewModel(sp.GetRequiredService<SessionService>()));
        builder.Services.AddTransient<InvitationLandingViewModel>(sp =>
            new InvitationLandingViewModel(
                sp.GetRequiredService<LuSplit.Application.Invitations.UseCases.AcceptInvitationUseCase>(),
                sp.GetRequiredService<LuSplit.Application.Invitations.UseCases.DeclineInvitationUseCase>(),
                sp.GetRequiredService<LuSplit.Application.Shared.Ports.IAuthPort>(),
                DeviceInfo.Current.Idiom.ToString()));

        // Authentication (MSAL)
        builder.Services.AddSingleton<IPublicClientApplication>(sp =>
            PublicClientApplicationBuilder
                .Create(AuthConfig.MobileClientId)
                .WithAuthority(AuthConfig.Authority)
                .WithRedirectUri($"msal{AuthConfig.MobileClientId}://auth")
                .Build());
        builder.Services.AddSingleton<IAuthPort>(sp =>
            new MsalAuthAdapter(
                sp.GetRequiredService<IPublicClientApplication>(),
                new[] { AuthConfig.RequiredScope },
#if ANDROID
                () => Platform.CurrentActivity
#else
                null
#endif
            ));

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
            if (!string.IsNullOrWhiteSpace(AuthConfig.FunctionsBaseUrl))
                httpClient.BaseAddress = new Uri(AuthConfig.FunctionsBaseUrl);
            var authPort = sp.GetRequiredService<IAuthPort>();
            return new ControlPlaneHttpClient(httpClient, ct => authPort.GetAccessTokenAsync(ct));
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
        builder.Services.AddSingleton<ISecureKeyStoragePort>(sp => sp.GetRequiredService<SecureKeyStorageAdapter>());
        builder.Services.AddTransient<RotateGroupKeyUseCase>();

        // Group membership (requires IGroupMembershipRepository from AppDataService — wired via factory)

        // Conflict + activity
        builder.Services.AddSingleton<ConflictFlagStore>();
        builder.Services.AddTransient<Features.Expenses.ConflictReviewPromptViewModel>();
        builder.Services.AddTransient<IActivityFeedDataService, ActivityFeedDataService>();

        // Application ports backed by SQLite (resolved via AppDataService)
        builder.Services.AddSingleton<IIdGenerator>(new GuidIdGenerator());
        builder.Services.AddSingleton<IClock>(new UtcClock());

        // Lazy proxies for SQLite-backed ports (needed because SQLite is initialized async)
        builder.Services.AddSingleton<SharedGroupStateRepositoryProxy>(sp =>
            new SharedGroupStateRepositoryProxy(sp.GetRequiredService<AppDataService>()));
        builder.Services.AddSingleton<ISharedGroupStateRepository>(sp =>
            sp.GetRequiredService<SharedGroupStateRepositoryProxy>());
        builder.Services.AddSingleton<ActivityEntryPortProxy>(sp =>
            new ActivityEntryPortProxy(sp.GetRequiredService<AppDataService>()));
        builder.Services.AddSingleton<IActivityEntryPort>(sp =>
            sp.GetRequiredService<ActivityEntryPortProxy>());
        builder.Services.AddSingleton<GroupRepositoryProxy>(sp =>
            new GroupRepositoryProxy(sp.GetRequiredService<AppDataService>()));
        builder.Services.AddSingleton<IGroupRepository>(sp =>
            sp.GetRequiredService<GroupRepositoryProxy>());
        builder.Services.AddTransient<ActivityFeedViewModel>(sp =>
            new ActivityFeedViewModel(sp.GetRequiredService<IActivityFeedDataService>()));

        // Revocation
        builder.Services.AddTransient<RevokeMemberUseCase>(sp =>
            new RevokeMemberUseCase(
                sp.GetRequiredService<LuSplit.Application.Revocation.Ports.IRevocationPort>(),
                sp.GetRequiredService<ISharedGroupStateRepository>(),
                sp.GetRequiredService<IActivityEntryPort>(),
                sp.GetRequiredService<IIdGenerator>(),
                sp.GetRequiredService<IClock>(),
                sp.GetRequiredService<RotateGroupKeyUseCase>()));

        // New pages
        builder.Services.AddTransient<ShareGroupPage>();
        builder.Services.AddTransient<LuSplit.Application.Groups.UseCases.CreateSharedGroupUseCase>();
        builder.Services.AddTransient<LuSplit.Application.Groups.UseCases.ConvertGroupToSharedUseCase>();
        builder.Services.AddTransient<ShareGroupViewModel>();
        builder.Services.AddTransient<MemberListPage>();
        builder.Services.AddTransient<MemberListViewModel>();
        builder.Services.AddTransient<DeviceManagementPage>();
        builder.Services.AddTransient<DeviceManagementViewModel>();
        builder.Services.AddTransient<ActivityFeedPage>();
        builder.Services.AddTransient<InvitePage>();
        builder.Services.AddTransient<InviteViewModel>();
        builder.Services.AddTransient<ConvertGroupViewModel>();

        return builder.Build();
    }
}