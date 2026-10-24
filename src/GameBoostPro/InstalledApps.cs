using Microsoft.Win32;

namespace GameBoostPro;

public sealed record InstalledApp(string Name, string Publisher, string Version, string Location, long? SizeKiB, bool Optional);

public static class InstalledApps
{
    public static bool Optional(string name, string publisher, bool system, bool update, bool noRemove)
    {
        if (system || update || noRemove) return false;
        // Unknown packages remain informational. Do not infer safety from size or age.
        return (name.Equals("Google Chrome", StringComparison.OrdinalIgnoreCase) && publisher.Equals("Google LLC", StringComparison.OrdinalIgnoreCase)) ||
            (name.StartsWith("Mozilla Firefox", StringComparison.OrdinalIgnoreCase) && publisher.Equals("Mozilla", StringComparison.OrdinalIgnoreCase)) ||
            (name.StartsWith("VLC media player", StringComparison.OrdinalIgnoreCase) && publisher.Equals("VideoLAN", StringComparison.OrdinalIgnoreCase)) ||
            (name.Equals("Spotify", StringComparison.OrdinalIgnoreCase) && publisher.Equals("Spotify AB", StringComparison.OrdinalIgnoreCase)) ||
            (name.StartsWith("OBS Studio", StringComparison.OrdinalIgnoreCase) && publisher.Equals("OBS Project", StringComparison.OrdinalIgnoreCase));
    }
    public static string LocationGroup(InstalledApp app, string volume)
    {
        if (string.IsNullOrWhiteSpace(app.Location) || !Path.IsPathFullyQualified(app.Location)) return "Localização desconhecida";
        try
        {
            if (!Directory.Exists(app.Location) || !StorageSafety.NoLinks(app.Location)) return "Localização desconhecida";
            return StorageSafety.Within(app.Location, volume) ? "Na unidade escolhida" : "Outra unidade";
        }
        catch (Exception ex) when (StorageSafety.AccessError(ex) || ex is ArgumentException) { return "Localização desconhecida"; }
    }
    public static bool RequestRemoval(InstalledApp app, string? confirmation, Action openWindows)
    {
        if (!app.Optional || confirmation != "DESINSTALAR") return false;
        openWindows(); // Exceptions propagate; opening settings is never recorded as removal.
        return true;
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static List<InstalledApp> Read()
    {
        var result = new List<InstalledApp>();
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var id in uninstall.GetSubKeyNames())
                {
                    try
                    {
                        using var key = uninstall.OpenSubKey(id); if (key is null) continue;
                        string Value(string name) => Convert.ToString(key.GetValue(name))?.Trim() ?? "";
                        var name = Value("DisplayName"); if (name.Length == 0) continue;
                        var publisher = Value("Publisher"); var size = Value("EstimatedSize");
                        result.Add(new(name, publisher, Value("DisplayVersion"), Value("InstallLocation"), long.TryParse(size, out var n) && n >= 0 ? n : null,
                            Optional(name, publisher, Value("SystemComponent") != "" && Value("SystemComponent") != "0",
                                Value("ReleaseType") != "" || Value("ParentKeyName") != "", Value("NoRemove") != "" && Value("NoRemove") != "0")));
                    }
                    catch (Exception ex) when (StorageSafety.AccessError(ex)) { Program.Log("Aplicativo inacessível no registro: " + ex.Message); }
                }
            }
            catch (Exception ex) when (StorageSafety.AccessError(ex)) { Program.Log("Lista de aplicativos parcialmente indisponível: " + ex.Message); }
        }
        return result.DistinctBy(a => (a.Name, a.Version, a.Location)).OrderBy(a => a.Name).ToList();
    }
}
