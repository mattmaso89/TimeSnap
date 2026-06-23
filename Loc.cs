using Microsoft.Windows.ApplicationModel.Resources;

namespace TimeSnap;

// Resolves .resw strings via the Windows App SDK's own MRT-core resource
// system, which works without an MSIX package identity. The OS-level
// Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride throws
// ("Operation is not valid due to the current state of the object") in this
// unpackaged app because it requires package identity — so forcing a
// language is done per-lookup via a ResourceContext qualifier instead of a
// process-wide override.
internal static class Loc
{
    private static readonly ResourceManager _manager = new();
    private static readonly ResourceMap _map = ResolveResourceMap();

    // null = follow the OS-preferred language; otherwise a BCP-47 tag like "de-DE".
    private static string? _languageOverride;

    public static void SetLanguageOverride(string? bcp47Language) => _languageOverride = bcp47Language;

    public static string Get(string key)
    {
        var context = _manager.CreateResourceContext();
        if (_languageOverride is not null)
            context.QualifierValues[KnownResourceQualifierName.Language] = _languageOverride;

        return _map.GetValue(key, context)?.ValueAsString ?? key;
    }

    private static ResourceMap ResolveResourceMap()
    {
        try
        {
            // .resw files under Strings\<lang>\Resources.resw compile into a
            // "Resources" subtree of the main resource map.
            return _manager.MainResourceMap.GetSubtree("Resources");
        }
        catch
        {
            return _manager.MainResourceMap;
        }
    }
}
