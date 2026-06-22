using Microsoft.Windows.ApplicationModel.Resources;

namespace TimeSnap;

// Thin wrapper around ResourceLoader — call Loc.Get("Key") anywhere.
// The loader is created lazily; safe to call after OnLaunched has run.
internal static class Loc
{
    private static ResourceLoader? _loader;
    public static string Get(string key) => (_loader ??= new ResourceLoader()).GetString(key);
}
