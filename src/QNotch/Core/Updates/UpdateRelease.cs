using System.Text.Json;

namespace QNotch.Core.Updates;

/// <summary>Only stable releases and the exact assets built by our release workflow are eligible.</summary>
internal sealed record UpdateRelease(Version Version, string Tag, Uri Download, long Size, string Sha256, bool IsZip,
    string? Name = null, string? Notes = null, DateTimeOffset? PublishedAt = null)
{
    public static string Repository => Brand.UpdateRepository;
    public const long MaxDownloadSize = 256 * 1024 * 1024;
    public static string DisplayVersion(Version version) => version.ToString(version.Revision > 0 ? 4 : 3);

    public static Version? ParseVersion(string? text)
    {
        text = text?.Trim().TrimStart('v', 'V').Split('+')[0];
        return Version.TryParse(text, out var v) && v.Major >= 0
            ? new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision)) : null;
    }

    /// <param name="repository">The feed the download must come from; defaults to <see cref="Brand.UpdateRepository"/>.</param>
    public static UpdateRelease? Read(string json, Version installed, bool selfContained, string? repository = null)
    {
        repository ??= Repository;
        if (repository.Length == 0) throw new InvalidDataException("No update feed is configured.");
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { throw new InvalidDataException("GitHub returned release information that is not valid JSON."); }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("GitHub returned release information in an unexpected format.");
            if (Flag(root, "draft") || Flag(root, "prerelease")) return null;
            var tag = Text(root, "tag_name");
            if (tag.Length == 0) throw new InvalidDataException("The latest release has no version tag.");
            var version = ParseVersion(tag);
            if (version is null || version <= installed) return null;
            var name = selfContained ? "QNotch-self-contained.zip" : "QNotch.exe";
            var assets = root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().ToArray() : [];
            foreach (var asset in assets)
            {
                if (asset.ValueKind != JsonValueKind.Object || Text(asset, "name") != name || Text(asset, "state") != "uploaded") continue;
                var expected = $"{repository}/releases/download/{Uri.EscapeDataString(tag)}/{name}";
                if (!Uri.TryCreate(Text(asset, "browser_download_url"), UriKind.Absolute, out var url)
                    || !string.Equals(url.AbsoluteUri, expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"The release download is not from the {Brand.ProductName} repository.");
                var size = asset.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out var n) ? n : 0;
                var digest = Text(asset, "digest");
                if (size is <= 0 or > MaxDownloadSize || !digest.StartsWith("sha256:", StringComparison.Ordinal)
                    || digest.Length != 71 || !digest.AsSpan(7).ToString().All(Uri.IsHexDigit))
                    throw new InvalidDataException("This release has no valid download size or SHA-256 checksum.");
                DateTimeOffset? published = DateTimeOffset.TryParse(Text(root, "published_at"), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var p) ? p : null;
                return new(version, tag, url, size, digest[7..], selfContained, Text(root, "name"), Text(root, "body"), published);
            }
            throw new InvalidDataException(assets.Length == 0 ? NoAssetsText
                : $"This release does not have a download for your {Brand.ProductName} installation yet.");
        }
    }

    public const string NoAssetsText = "No downloadable assets available.";
    public const string NoNotesText = "No release notes provided.";
    public const string NoDateText = "Date unavailable.";

    /// <summary>The release name, or the tag when GitHub has none. Never null or "null".</summary>
    public string Title => Clean(Name) ?? Tag;
    public string NotesText => Clean(Notes) ?? NoNotesText;
    public string PublishedText => PublishedAt is { } d ? d.ToLocalTime().ToString("d MMM yyyy", System.Globalization.CultureInfo.CurrentCulture) : NoDateText;

    static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) || text.Trim() == "null" ? null : text.Trim();
    static bool Flag(JsonElement e, string property) => e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.True;
    /// <summary>A string property, or empty when it is missing, null or not a string.</summary>
    static string Text(JsonElement e, string property) =>
        e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
