using Android.App;
using Android.Content;
using Android.Runtime;
using Microsoft.Identity.Client;

namespace LuSplit.App;

/// <summary>
/// Receives the MSAL browser redirect callback and hands control back to
/// <see cref="AuthenticationContinuationHelper"/> so that the
/// <c>AcquireTokenInteractive</c> task completes.
///
/// The intent-filter for this activity is declared in <c>AndroidManifest.xml</c>
/// using the <c>${MSAL_REDIRECT_SCHEME}</c> manifest placeholder, which MSBuild
/// resolves to <c>msal{ClientId}</c> at build time from
/// <c>LuSplit.App.secrets.props</c>.  This keeps the client-id in a single
/// source of truth (the props file) and avoids hardcoding it in code or
/// duplicating it across attributes.
///
/// The <see cref="RegisterAttribute"/> gives this class a stable Java name so
/// the manifest can reference it without depending on CRC-mangled names.
///
/// Attributes: Exported (reachable from browser), NoHistory (removed from
/// back-stack so the user cannot navigate back into the system browser).
/// </summary>
[Activity(Exported = true, NoHistory = true)]
[Register("com.jgcarmona.lusplit.MsalActivity")]
public class MsalActivity : BrowserTabActivity
{
}
