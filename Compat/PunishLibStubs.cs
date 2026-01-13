namespace PunishLib;

/// <summary>
/// Minimal PunishLib shim for builds that don't include the original PunishLib dependency.
/// Only includes the surface ParseLord2 uses.
/// </summary>
public static class PunishLibMain
{
    public static void Init(object pluginInterface, string pluginName)
    {
        // No-op shim.
    }
}

public static class ImGuiMethods
{
    public static class AboutTab
    {
        public static void Draw(string pluginName)
        {
            // No-op shim.
        }
    }
}
