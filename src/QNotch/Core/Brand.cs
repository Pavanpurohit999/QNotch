namespace QNotch.Core;

/// <summary>
/// Orbit Desk product identity. Orbit Desk is a fork of QNotch (MIT, Copyright (c) 2026 Soma Somorjai);
/// see NOTICE.md. Change these values to rebrand. Internal namespaces keep the QNotch name to stay merge-friendly with upstream.
/// </summary>
internal static class Brand
{
    public const string ProductName = "Orbit Desk";

    /// <summary>
    /// GitHub repository (https://github.com/owner/name) whose releases feed the updater. Empty disables update checks entirely,
    /// so the fork can never download an upstream QNotch build over itself. Set it once you publish your own releases.
    /// </summary>
    public const string UpdateRepository = "";

    public static bool UpdatesConfigured => UpdateRepository.Length > 0;

    public static string UpdateApi => UpdateApiFor(UpdateRepository);

    /// <summary>The GitHub "latest release" API address for a feed repository, or empty when there is no feed.</summary>
    public static string UpdateApiFor(string repository) => repository.StartsWith("https://github.com/", StringComparison.Ordinal)
        ? "https://api.github.com/repos/" + repository["https://github.com/".Length..] + "/releases/latest"
        : "";
}
