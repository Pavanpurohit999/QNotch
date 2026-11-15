using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using QNotch.Core;
using QNotch.Core.Updates;

var settingsDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "QNotch updater preferences " + Guid.NewGuid().ToString("N")));
Environment.SetEnvironmentVariable("QNOTCH_DATA_DIR", settingsDirectory);
var current = new Version(0, 4, 0, 0);
var previous = new Version(0, 3, 0, 0);
var exe = File.ReadAllBytes(Environment.ProcessPath!);
// Orbit Desk ships with no update feed (Brand.UpdateRepository is empty). These checks exercise the real
// validation against a placeholder feed, the way it behaves once Brand.UpdateRepository names the fork's repository.
const string Feed = "https://github.com/example/orbit-desk";
static UpdateRelease? Read(string json, Version installed, bool selfContained) => UpdateRelease.Read(json, installed, selfContained, Feed);
var failures = 0;
var checks = 0;
await Test("normalizes versions and rejects preview tags", () =>
{
    Assert(UpdateRelease.ParseVersion("v0.4.0+commit") == current);
    Assert(UpdateRelease.ParseVersion("0.4") == current);
    Assert(UpdateRelease.ParseVersion("v0.5.0-beta.1") is null);
    return Task.CompletedTask;
});
await Test("new stable release selects the installed variant", () =>
{
    Assert(Read(ReleaseJson(exe), previous, false) is { IsZip: false, Version: var v } && v == current);
    Assert(Read(ReleaseJson(exe, zip: true), previous, true) is { IsZip: true });
    Assert(Read(ReleaseJson(exe), current, false) is null);
    Assert(Read(ReleaseJson(exe), new Version(1, 0, 0, 0), false) is null);
    Assert(Read(ReleaseJson(exe, draft: true), previous, false) is null);
    Assert(Read(ReleaseJson(exe, preview: true), previous, false) is null);
    return Task.CompletedTask;
});
await Test("Mac channel tags never become Windows updates, even at a higher version", () =>
{
    using var fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "release-tags.json")));
    foreach (var fixture in fixtures.RootElement.EnumerateArray())
    {
        if (fixture.GetProperty("platform").GetString() != "macos") continue;
        var tag = fixture.GetProperty("tag").GetString()!;
        var json = ReleaseJson(exe, tag: tag, assetName: "QNotch-macos-arm64.dmg");
        Assert(Read(json, previous, false) is null);
        Assert(Read(json, previous, true) is null);
    }
    return Task.CompletedTask;
});
await Test("Mac assets do not change selection of either legacy Windows variant", () =>
{
    foreach (var zip in new[] { false, true })
    {
        var root = JsonNode.Parse(ReleaseJson(exe, zip: zip))!;
        var assets = root["assets"]!.AsArray();
        var mac = assets[0]!.DeepClone();
        mac["name"] = "QNotch-macos-arm64.dmg";
        mac["browser_download_url"] = $"{Feed}/releases/download/v0.4.0/QNotch-macos-arm64.dmg";
        assets.Insert(0, mac);
        Assert(Read(root.ToJsonString(), previous, zip) is { } update && update.IsZip == zip);
    }
    return Task.CompletedTask;
});
await Test("Windows-style tags with only Mac downloads still fail closed", async () =>
{
    var json = ReleaseJson(exe, assetName: "QNotch-macos-arm64.dmg");
    await Reject(() => Task.FromResult(Read(json, previous, false)));
    await Reject(() => Task.FromResult(Read(json, previous, true)));
});
await Test("rejects missing assets, foreign URLs and missing checksums", async () =>
{
    await Reject(() => Task.FromResult(Read(ReleaseJson(exe, zip: true), previous, false)));
    await Reject(() => Task.FromResult(Read(ReleaseJson(exe, url: "https://example.com/QNotch.exe"), previous, false)));
    await Reject(() => Task.FromResult(Read(ReleaseJson(exe, digest: ""), previous, false)));
});
await Test("checks the fixed GitHub endpoint and handles no releases", async () =>
{
    var requested = new List<string>();
    using var http = new HttpClient(new FakeHandler(request =>
    {
        requested.Add(request.RequestUri!.AbsoluteUri);
        Assert(request.Headers.UserAgent.ToString().StartsWith("QNotch/"));
        // releases/latest is 404 without releases; the repository itself exists.
        return new HttpResponseMessage(request.RequestUri.AbsoluteUri.EndsWith("/releases/latest") ? HttpStatusCode.NotFound : HttpStatusCode.OK);
    }));
    var client = new UpdateClient(http, Feed);
    Assert(await client.CheckAsync(previous, false, default) is null && client.NoReleasesPublished);
    Assert(requested.SequenceEqual(["https://api.github.com/repos/example/orbit-desk/releases/latest", "https://api.github.com/repos/example/orbit-desk"]));
});
await Test("with no feed configured, nothing is requested and no release is accepted", async () =>
{
    using var http = new HttpClient(new FakeHandler(_ => throw new Exception("No request may be sent without a feed.")));
    Assert(await new UpdateClient(http, "").CheckAsync(previous, false, default) is null);
    if (!Brand.UpdatesConfigured) Assert(await new UpdateClient(http).CheckAsync(previous, false, default) is null);
    await Reject(() => Task.FromResult(UpdateRelease.Read(ReleaseJson(exe), previous, false, "")));
});
await Test("reports GitHub rate limiting", async () =>
{
    using var http = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));
    await Reject(() => new UpdateClient(http, Feed).CheckAsync(previous, false, default));
});
await Test("a valid release keeps its name, notes and date", () =>
{
    var json = JsonNode.Parse(ReleaseJson(exe, tag: "v0.5.0"))!;
    json["name"] = "Orbit Desk 0.5.0"; json["body"] = "Glass theme."; json["published_at"] = "2026-11-15T10:00:00Z";
    var release = Read(json.ToJsonString(), previous, false)!;
    Assert(release.Title == "Orbit Desk 0.5.0" && release.NotesText == "Glass theme." && release.PublishedAt == new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));
    return Task.CompletedTask;
});
await Test("missing optional fields fall back to readable text, never null", () =>
{
    var release = Read(ReleaseJson(exe, tag: "v0.5.0"), previous, false)!; // no name, body or published_at
    Assert(release.Title == "v0.5.0" && release.NotesText == UpdateRelease.NoNotesText && release.PublishedText == UpdateRelease.NoDateText);
    return Task.CompletedTask;
});
await Test("null fields fall back too, and draft/prerelease null mean false", () =>
{
    var json = JsonNode.Parse(ReleaseJson(exe, tag: "v0.5.0"))!;
    json["name"] = null; json["body"] = "null"; json["published_at"] = null; json["draft"] = null; json["prerelease"] = null;
    var release = Read(json.ToJsonString(), previous, false)!;
    Assert(release.Title == "v0.5.0" && release.NotesText == UpdateRelease.NoNotesText && release.PublishedText == UpdateRelease.NoDateText);
    foreach (var text in new[] { release.Title, release.NotesText, release.PublishedText }) Assert(!text.Contains("null"));
    return Task.CompletedTask;
});
await Test("empty, missing or null assets report that no download exists", async () =>
{
    foreach (var assets in new JsonNode?[] { new JsonArray(), null })
    {
        var json = JsonNode.Parse(ReleaseJson(exe, tag: "v0.5.0"))!;
        json["assets"] = assets;
        try { Read(json.ToJsonString(), previous, false); throw new Exception("accepted a release without assets"); }
        catch (InvalidDataException ex) { Assert(ex.Message == UpdateRelease.NoAssetsText); }
    }
    var noAssets = JsonNode.Parse(ReleaseJson(exe, tag: "v0.5.0"))!.AsObject();
    noAssets.Remove("assets");
    await Reject(() => Task.FromResult(Read(noAssets.ToJsonString(), previous, false)));
});
await Test("a release without a tag, invalid JSON or a non-object response is rejected with a message", async () =>
{
    var json = JsonNode.Parse(ReleaseJson(exe))!.AsObject();
    json.Remove("tag_name");
    foreach (var bad in new[] { json.ToJsonString(), "{ not json", "[]", "null" })
    {
        try { Read(bad, previous, false); throw new Exception("accepted " + bad); }
        catch (InvalidDataException ex) { Assert(ex.Message.Length > 0 && !ex.Message.Contains("null")); }
    }
    await Task.CompletedTask;
});
await Test("an empty feed (repository exists, no releases) is not an error", async () =>
{
    using var http = new HttpClient(new FakeHandler(request => new HttpResponseMessage(
        request.RequestUri!.AbsoluteUri.EndsWith("/releases/latest") ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = new StringContent("{}") }));
    var client = new UpdateClient(http, Feed);
    Assert(await client.CheckAsync(previous, false, default) is null && client.NoReleasesPublished);
});
await Test("a repository that does not exist is reported, not shown as up to date", async () =>
{
    using var http = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
    var client = new UpdateClient(http, Feed);
    try { await client.CheckAsync(previous, false, default); throw new Exception("missing repository accepted"); }
    catch (HttpRequestException ex) { Assert(ex.Message.Contains("not found") && !client.NoReleasesPublished); }
});
await Test("a network failure or timeout surfaces as a rejected check", async () =>
{
    using var offline = new HttpClient(new FakeHandler(_ => throw new HttpRequestException("No such host is known.", new System.Net.Sockets.SocketException(11001))));
    await Reject(() => new UpdateClient(offline, Feed).CheckAsync(previous, false, default));
    using var slow = new HttpClient(new FakeHandler(_ => throw new TaskCanceledException("The request timed out.")));
    await Reject(() => new UpdateClient(slow, Feed).CheckAsync(previous, false, default));
});
await Test("the service shows 'No releases published yet' for an empty feed", async () =>
{
    var settings = new GeneralSettings { UpdateChecksChoiceMade = true };
    using var http = new HttpClient(new FakeHandler(request => new HttpResponseMessage(
        request.RequestUri!.AbsoluteUri.EndsWith("/releases/latest") ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content = new StringContent("{}") }));
    using var updates = new UpdateService(settings, Dispatcher.CurrentDispatcher, new SettingsStore(), () => { }, http, Feed);
    await updates.CheckAsync();
    Assert(updates.State.Status.StartsWith("No releases published yet.") && !updates.State.HasUpdate && updates.State.ReleaseDetails == "");
});
await Test("the service shows release details with fallbacks for an available update", async () =>
{
    var settings = new GeneralSettings { UpdateChecksChoiceMade = true };
    using var http = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReleaseJson(exe, tag: "v9.0.0")) }));
    using var updates = new UpdateService(settings, Dispatcher.CurrentDispatcher, new SettingsStore(), () => { }, http, Feed);
    await updates.CheckAsync();
    Assert(updates.State.HasUpdate && updates.State.ReleaseDetails == $"v9.0.0 · {UpdateRelease.NoDateText}\n{UpdateRelease.NoNotesText}");
    Assert(!updates.State.AvailableText.Contains("null") && updates.State.AvailableText.StartsWith(Brand.ProductName));
});
await Test("verifies a valid exe download", () => InDirectory(async dir =>
{
    await Download(exe, dir);
    Assert(File.ReadAllBytes(Path.Combine(dir, "QNotch.exe")).SequenceEqual(exe));
}));
await Test("extracts and verifies the self-contained package", () => InDirectory(async dir =>
{
    await Download(Zip(exe), dir, zip: true);
    Assert(File.ReadAllBytes(Path.Combine(dir, "QNotch.exe")).SequenceEqual(exe));
}));
await Test("rejects a tampered download before installation", () => InDirectory(async dir =>
{
    await Reject(() => Download(exe, dir, digest: new string('0', 64)));
    Assert(!File.Exists(Path.Combine(dir, "QNotch.exe")));
}));
await Test("rejects an incomplete download", () => InDirectory(async dir =>
{
    await Reject(() => Download(exe[..100], dir, expectedSize: exe.Length));
    Assert(!File.Exists(Path.Combine(dir, "QNotch.exe")));
}));
await Test("rejects archive traversal", () => InDirectory(async dir =>
{
    await Reject(() => Download(Zip(exe, "../QNotch.exe"), dir, zip: true));
    Assert(!File.Exists(Path.Combine(dir, "QNotch.exe")));
}));
await Test("rejects archives with extra files", () => InDirectory(async dir =>
{
    await Reject(() => Download(Zip(exe, extraFile: true), dir, zip: true));
    Assert(!File.Exists(Path.Combine(dir, "QNotch.exe")));
}));
await Test("rejects a binary whose version differs from the tag", () => InDirectory(async dir =>
{
    await Reject(() => Download(exe, dir, tag: "v0.5.0"));
}));
await Test("honors download cancellation", () => InDirectory(async dir =>
{
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    await Reject(() => Download(exe, dir, cancellation: cancellation.Token));
}));
await Test("helper atomically replaces and keeps the previous exe", () => HelperTest("success"));
await Test("helper restores the old exe when Explorer fails", () => HelperTest("restart-failure"));
await Test("helper leaves a locked executable intact", () => HelperTest("locked"));
await Test("helper waits for the running app to exit", () => HelperTest("wait"));
await Test("helper refuses a staging directory outside the install folder", () => HelperTest("invalid-path"));
await Test("new settings enable update checks and ask for a choice", () =>
{
    var settings = JsonSerializer.Deserialize<GeneralSettings>("{}")!;
    var store = new SettingsStore();
    using var updates = new UpdateService(settings, Dispatcher.CurrentDispatcher, store, () => { });
    Assert(settings.CheckForUpdates && updates.State.NeedsPreferenceChoice);
    return Task.CompletedTask;
});
await Test("old defaults are enabled once and keeping them on is remembered", () =>
{
    var settings = JsonSerializer.Deserialize<GeneralSettings>("{\"CheckForUpdates\":false}")!;
    var store = new SettingsStore();
    using var updates = new UpdateService(settings, Dispatcher.CurrentDispatcher, store, () => { });
    Assert(settings.CheckForUpdates && updates.State.NeedsPreferenceChoice);
    updates.ChooseAutomaticChecks(true);
    store.Flush();
    var saved = JsonSerializer.Deserialize<GeneralSettings>(File.ReadAllText(Path.Combine(settingsDirectory, "general.json")), SettingsStore.Json)!;
    using var restarted = new UpdateService(saved, Dispatcher.CurrentDispatcher, new SettingsStore(), () => { });
    Assert(saved.CheckForUpdates && saved.UpdateChecksChoiceMade && !restarted.State.NeedsPreferenceChoice);
    return Task.CompletedTask;
});
await Test("turning off is remembered across restart without another prompt", () =>
{
    var settings = new GeneralSettings();
    var store = new SettingsStore();
    using var updates = new UpdateService(settings, Dispatcher.CurrentDispatcher, store, () => { });
    updates.ChooseAutomaticChecks(false);
    store.Flush();
    var saved = JsonSerializer.Deserialize<GeneralSettings>(File.ReadAllText(Path.Combine(settingsDirectory, "general.json")), SettingsStore.Json)!;
    using var restarted = new UpdateService(saved, Dispatcher.CurrentDispatcher, new SettingsStore(), () => { });
    Assert(!saved.CheckForUpdates && saved.UpdateChecksChoiceMade && !restarted.State.NeedsPreferenceChoice);
    return Task.CompletedTask;
});
await Test("using the toggle also answers the first-run choice", () =>
{
    var settings = new GeneralSettings();
    var store = new SettingsStore();
    using var updates = new UpdateService(settings, Dispatcher.CurrentDispatcher, store, () => { });
    settings.CheckForUpdates = false;
    Assert(settings.UpdateChecksChoiceMade && !updates.State.NeedsPreferenceChoice);
    store.Flush();
    return Task.CompletedTask;
});
await Test("update notifications remain suppressed for the same version after restart", async () =>
{
    var settings = new GeneralSettings { UpdateChecksChoiceMade = true };
    var store = new SettingsStore();
    HttpClient Client() => new(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(ReleaseJson(exe, tag: "v9.0.0")) })); // newer than the app itself (OrbitDesk 0.5.0)
    using var updates = new UpdateService(settings, Dispatcher.CurrentDispatcher, store, () => { }, Client(), Feed);
    var events = 0;
    updates.UpdateAvailable += () => events++;
    await updates.CheckAsync(true);
    Assert(events == 1 && updates.ShouldNotify);
    updates.MarkNotified();
    await updates.CheckAsync(true);
    Assert(events == 2 && !updates.ShouldNotify);
    store.Flush();
    var saved = JsonSerializer.Deserialize<GeneralSettings>(File.ReadAllText(Path.Combine(settingsDirectory, "general.json")), SettingsStore.Json)!;
    using var restarted = new UpdateService(saved, Dispatcher.CurrentDispatcher, new SettingsStore(), () => { }, Client(), Feed);
    await restarted.CheckAsync(true);
    Assert(!restarted.ShouldNotify);
});
await Test("turning checks off cancels an in-flight automatic check", async () =>
{
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var settings = new GeneralSettings { UpdateChecksChoiceMade = true };
    var store = new SettingsStore();
    var http = new HttpClient(new AsyncFakeHandler(async (_, cancellation) =>
    {
        started.SetResult();
        await Task.Delay(Timeout.Infinite, cancellation);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }));
    using var updates = new UpdateService(settings, Dispatcher.CurrentDispatcher, store, () => { }, http, Feed);
    var check = updates.CheckAsync(true);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    updates.ChooseAutomaticChecks(false);
    await check.WaitAsync(TimeSpan.FromSeconds(5));
    Assert(!settings.CheckForUpdates && !updates.State.IsBusy && !updates.State.HasUpdate);
    Assert(updates.State.Status == "Automatic checks are off.");
    store.Flush();
});
if (args.Contains("--live")) await Test("reads both actual GitHub release variants", async () =>
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    var client = new UpdateClient(http); // the shipped feed (Brand.UpdateRepository)
    Assert(await client.CheckAsync(new Version(0, 0, 0, 0), false, default) is { IsZip: false });
    Assert(await client.CheckAsync(new Version(0, 0, 0, 0), true, default) is { IsZip: true });
});
if (args.Contains("--verify-publish"))
{
    await Test("published exe embeds the framework-dependent runtime configuration", () =>
    {
        VerifyPublishedRuntime(Path.GetFullPath("dist/QNotch.exe"), false);
        return Task.CompletedTask;
    });
    await Test("published self-contained exe embeds its runtime configuration", () =>
    {
        VerifyPublishedRuntime(Path.GetFullPath("dist/self-contained/QNotch.exe"), true);
        return Task.CompletedTask;
    });
    await Test("published archive contains only the exact self-contained executable", () =>
    {
        using var archive = ZipFile.OpenRead(Path.GetFullPath("dist/QNotch-self-contained.zip"));
        Assert(archive.Entries.Count == 1 && archive.Entries[0].FullName == "QNotch.exe");
        Assert(archive.Entries[0].Length is > 0 and <= UpdateRelease.MaxDownloadSize);
        using var entry = archive.Entries[0].Open();
        using var published = File.OpenRead(Path.GetFullPath("dist/self-contained/QNotch.exe"));
        Assert(archive.Entries[0].Length == published.Length);
        Assert(SHA256.HashData(entry).SequenceEqual(SHA256.HashData(published)));
        return Task.CompletedTask;
    });
    if (Array.IndexOf(args, "--release-version") is var index && index >= 0)
        await Test("both published executable versions match the release tag", () =>
        {
            Assert(index + 1 < args.Length);
            var version = UpdateRelease.ParseVersion(args[index + 1]);
            Assert(version is not null);
            UpdateClient.ValidateExecutable(Path.GetFullPath("dist/QNotch.exe"), version!);
            UpdateClient.ValidateExecutable(Path.GetFullPath("dist/self-contained/QNotch.exe"), version!);
            return Task.CompletedTask;
        });
}
Console.WriteLine($"{checks - failures}/{checks} update checks passed.");
if (Directory.Exists(settingsDirectory)) Directory.Delete(settingsDirectory, true); // exact absolute test-owned directory above
return failures == 0 ? 0 : 1;

async Task Test(string name, Func<Task> test)
{
    checks++;
    try { await test(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex); }
}
static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
static void VerifyPublishedRuntime(string exePath, bool selfContained)
{
    // Inspect the bundle with the installed SDK, without executing the published app or opening any dialogs.
    var dotnetRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "..", "..", ".."));
    var sdk = Directory.GetDirectories(Path.Combine(dotnetRoot, "sdk"))
        .Select(path => (Path: path, Version: Version.TryParse(Path.GetFileName(path), out var v) ? v : null))
        .Where(item => item.Version?.Major == 10).OrderByDescending(item => item.Version).First().Path;
    var hostModel = Assembly.LoadFrom(Path.Combine(sdk, "Microsoft.NET.HostModel.dll"));
    var isBundle = hostModel.GetType("Microsoft.NET.HostModel.Bundle.Bundler")!.GetMethod("IsBundle")!;
    object?[] arguments = [exePath, 0L];
    Assert((bool)isBundle.Invoke(null, arguments)!);
    using var reader = new BinaryReader(File.OpenRead(exePath));
    Assert(reader.BaseStream.Length is > 0 and <= UpdateRelease.MaxDownloadSize);
    reader.BaseStream.Position = (long)arguments[1]!;
    Assert(reader.ReadUInt32() is 2 or 6);
    Assert(reader.ReadUInt32() == 0 && reader.ReadInt32() > 0);
    _ = reader.ReadString(); // bundle id
    _ = reader.ReadInt64(); _ = reader.ReadInt64(); // deps.json location
    var offset = reader.ReadInt64();
    var size = reader.ReadInt64();
    Assert(offset > 0 && size is > 0 and < 65536 && offset + size <= reader.BaseStream.Length);
    reader.BaseStream.Position = offset;
    using var config = JsonDocument.Parse(reader.ReadBytes((int)size));
    var runtime = config.RootElement.GetProperty("runtimeOptions");
    var hasFrameworks = runtime.TryGetProperty("frameworks", out var frameworks);
    var hasIncluded = runtime.TryGetProperty("includedFrameworks", out var included);
    Assert(selfContained ? hasIncluded && !hasFrameworks : hasFrameworks && !hasIncluded);
    var names = (selfContained ? included : frameworks).EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToArray();
    Assert(names.Contains("Microsoft.NETCore.App") && names.Contains("Microsoft.WindowsDesktop.App"));
}
static async Task Reject(Func<Task> action)
{
    try { await action(); }
    catch (Exception ex) when (ex is InvalidDataException or HttpRequestException or OperationCanceledException) { return; }
    throw new Exception("Expected the invalid update to be rejected.");
}
static string ReleaseJson(byte[] bytes, bool zip = false, bool draft = false, bool preview = false, string? url = null, string? digest = null, long? size = null, string tag = "v0.4.0", string? assetName = null)
{
    var name = assetName ?? (zip ? "QNotch-self-contained.zip" : "QNotch.exe");
    return JsonSerializer.Serialize(new
    {
        tag_name = tag, draft, prerelease = preview,
        assets = new[] { new { name, state = "uploaded", size = size ?? bytes.Length,
            browser_download_url = url ?? $"{Feed}/releases/download/{tag}/{name}",
            digest = "sha256:" + (digest ?? Convert.ToHexString(SHA256.HashData(bytes))) } },
    });
}
async Task Download(byte[] bytes, string dir, bool zip = false, string? digest = null, long? expectedSize = null, string tag = "v0.4.0", CancellationToken cancellation = default)
{
    using var http = new HttpClient(new FakeHandler(request =>
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentLength = expectedSize ?? bytes.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request };
    }));
    var release = Read(ReleaseJson(bytes, zip, digest: digest, size: expectedSize, tag: tag), previous, zip)!;
    await new UpdateClient(http, Feed).DownloadAsync(release, dir, null, cancellation);
}
static byte[] Zip(byte[] bytes, string name = "QNotch.exe", bool extraFile = false)
{
    using var memory = new MemoryStream();
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
    {
        using (var entry = zip.CreateEntry(name).Open()) entry.Write(bytes);
        if (extraFile) zip.CreateEntry("unexpected.dll");
    }
    return memory.ToArray();
}
static async Task InDirectory(Func<string, Task> test)
{
    var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "QNotch updater tests ' ü " + Guid.NewGuid().ToString("N")));
    Directory.CreateDirectory(root);
    try { await test(root); }
    finally { Directory.Delete(root, true); } // exact test-owned directory, verified absolute above
}
static async Task HelperTest(string mode) => await InDirectory(async root =>
{
    var target = Path.Combine(root, "QNotch.exe");
    File.WriteAllText(target, "original");
    var stage = Path.Combine(root, ".qnotch-update-" + Guid.NewGuid().ToString("N"));
    if (mode == "invalid-path") stage = Path.Combine(root, "outside", Path.GetFileName(stage));
    Directory.CreateDirectory(stage);
    File.WriteAllText(Path.Combine(stage, "QNotch.exe"), "updated");
    var result = Path.Combine(root, "result.txt");
    var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
    Process? parent = null;
    FileStream? locked = null;
    try
    {
        if (mode == "wait")
        {
            parent = Process.Start(new ProcessStartInfo(powershell) { UseShellExecute = false, CreateNoWindow = true,
                ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 3" } })!;
        }
        if (mode == "locked") locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
        var info = new ProcessStartInfo(powershell) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(AppContext.BaseDirectory, "HelperHarness.ps1"),
            "-Target", target, "-WorkDir", stage, "-ParentId", (parent?.Id ?? int.MaxValue).ToString(),
            "-ParentStarted", (parent?.StartTime.ToUniversalTime().Ticks ?? 0).ToString(), "-ResultFile", result, "-Mode", mode }) info.ArgumentList.Add(arg);
        using var helper = Process.Start(info)!;
        var errors = helper.StandardError.ReadToEndAsync();
        var output = helper.StandardOutput.ReadToEndAsync();
        await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        var details = await output + await errors;
        if (mode is "success" or "wait")
        {
            Assert(helper.ExitCode == 0 && File.ReadAllText(target) == "updated");
            Assert(File.ReadAllText(Path.Combine(root, ".QNotch.previous.exe")) == "original");
            Assert(File.ReadAllText(result).StartsWith("Updated to"));
            Assert(!Directory.Exists(stage));
            if (parent is not null) Assert(parent.HasExited);
        }
        else
        {
            if (helper.ExitCode != 1 || File.ReadAllText(target) != "original") throw new Exception(details + File.ReadAllText(result));
            Assert(File.ReadAllText(result).StartsWith("Update failed:"));
            if (mode == "invalid-path") Assert(!File.Exists(result + ".restart"));
        }
    }
    finally { locked?.Dispose(); if (parent is not null) { if (!parent.HasExited) parent.Kill(); parent.Dispose(); } }
});

sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(reply(request));
    }
}

sealed class AsyncFakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => reply(request, cancellationToken);
}
