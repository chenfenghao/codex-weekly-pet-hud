using System.Text.Json;

namespace CodexPetLimitRings.Windows.Services;

public sealed record ResetSignalEvent(string Id, string Kind, string State, DateTimeOffset At, string Summary)
{
    public string Key => $"{Id}|{Kind}|{State}";
    public string Title => UiText.T(Kind == "watch" ? "新的重置信号" : Kind == "banked" ? "新的储备重置公告" : State == "confirmed" ? "社区已确认重置公告" : "新的重置公告（待核实）");
}

public sealed record ResetSignalSnapshot(DateTimeOffset CheckedAt, DateTimeOffset UpdatedAt, DateTimeOffset? LastResetAt, bool Stale, bool ActiveSignal, List<ResetSignalEvent> Events);

public sealed class ResetSignalState
{
    public DateTimeOffset? InitializedAt { get; set; }
    public List<string> SeenKeys { get; set; } = [];
    public ResetSignalEvent? Highlight { get; set; }
    public ResetSignalSnapshot? Snapshot { get; set; }
}

public static class ResetSignalPolicy
{
    // First successful sync establishes a fence: historical events are displayed, never replayed.
    public static List<ResetSignalEvent> Accept(ResetSignalState state, ResetSignalSnapshot snapshot, bool notify)
    {
        if (snapshot.Stale) return [];
        state.SeenKeys ??= [];
        var seen = state.SeenKeys.ToHashSet(StringComparer.Ordinal);
        var initialized = state.InitializedAt is not null;
        var alerts = snapshot.Events.Where(e => initialized && !seen.Contains(e.Key)
            && !(e.Kind == "reset" && e.State != "confirmed" && seen.Contains(e.Id + "|reset|confirmed"))
            && !(e.Kind == "watch" && seen.Any(key => key.StartsWith(e.Id + "|reset|", StringComparison.Ordinal)))
            && e.At <= snapshot.CheckedAt.AddMinutes(5) && e.At >= snapshot.CheckedAt.AddDays(-2)
            && (e.At >= state.InitializedAt!.Value || seen.Any(key => key.StartsWith(e.Id + "|", StringComparison.Ordinal))))
            .OrderBy(e => e.At).ToList();
        state.InitializedAt ??= snapshot.CheckedAt;
        state.Snapshot = snapshot;
        if (state.Highlight is { } previous && !snapshot.Events.Any(e => e.Key == previous.Key)) state.Highlight = null;
        foreach (var e in snapshot.Events) if (seen.Add(e.Key)) state.SeenKeys.Add(e.Key);
        state.SeenKeys = state.SeenKeys.TakeLast(512).ToList();
        if (alerts.Count > 0) state.Highlight = alerts[^1];
        return notify ? alerts : [];
    }
}

public sealed class ResetSignalService : IDisposable
{
    private bool _usePython;
    private readonly HttpClient _client = new(new HttpClientHandler
    {
        Proxy = System.Net.WebRequest.GetSystemWebProxy(), UseProxy = true
    }) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2_000_000 };
    public ResetSignalService()
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexWeeklyPetHud/1.0");
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }
    public async Task<ResetSignalSnapshot> ReadAsync(CancellationToken token = default)
    {
        // Public community data only. This client has no account headers or credentials.
        string[] requests;
        try
        {
            requests = _usePython ? await ReadWithPythonAsync(token) : await Task.WhenAll(
                _client.GetStringAsync("https://codex-reset.com/api/forecast", token),
                _client.GetStringAsync("https://codex-reset.com/api/feed", token));
        }
        catch (HttpRequestException) when (!_usePython && !token.IsCancellationRequested && FindPython() is not null)
        {
            // Some network paths reject HttpClient but accept the installed Python's standard TLS client.
            // No browser impersonation, cookies, or authentication are used by this public-data fallback.
            requests = await ReadWithPythonAsync(token);
            _usePython = true;
            AppLog.Write("Reset radar connected using installed Python networking.");
        }
        using var forecast = JsonDocument.Parse(requests[0]);
        using var feed = JsonDocument.Parse(requests[1]);
        return Parse(forecast.RootElement, feed.RootElement, DateTimeOffset.UtcNow);
    }
    private static string? FindPython()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).ToList();
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
        if (Directory.Exists(installed)) directories.AddRange(Directory.EnumerateDirectories(installed, "Python*"));
        return directories.Select(dir => Path.Combine(dir.Trim('"'), "python.exe"))
            .FirstOrDefault(path => !path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase) && File.Exists(path));
    }
    private static async Task<string[]> ReadWithPythonAsync(CancellationToken token)
    {
        var executable = FindPython() ?? throw new HttpRequestException("Public API networking unavailable.");
        const string script = """
import concurrent.futures,json,urllib.request
def read(name):
    request=urllib.request.Request('https://codex-reset.com/api/'+name,headers={'User-Agent':'CodexWeeklyPetHud/1.0','Accept':'application/json'})
    with urllib.request.urlopen(request,timeout=15) as response:
        payload=response.read(1000001)
        if len(payload)>1000000: raise ValueError('Public API response too large')
        text=payload.decode('utf-8')
        json.loads(text)
        return text
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
    print(json.dumps(list(executor.map(read,('forecast','feed'))),ensure_ascii=True))
""";
        var start = new System.Diagnostics.ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("-I"); start.ArgumentList.Add("-c"); start.ArgumentList.Add(script);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var process = new System.Diagnostics.Process { StartInfo = start };
        try
        {
            process.Start();
            using var cancellation = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            await errors; // Drain stderr, but never forward remote errors into UI or logs.
            var json = await output;
            if (process.ExitCode != 0) throw new HttpRequestException("Public API request failed in compatibility mode.");
            var result = JsonSerializer.Deserialize<string[]>(json);
            return result is { Length: 2 } ? result : throw new JsonException("Public API response incomplete.");
        }
        catch (System.ComponentModel.Win32Exception) { throw new HttpRequestException("Public API network helper unavailable."); }
    }
    public static ResetSignalSnapshot Parse(JsonElement forecast, JsonElement feed, DateTimeOffset now)
    {
        if (forecast.ValueKind != JsonValueKind.Object || feed.ValueKind != JsonValueKind.Object
            || !forecast.TryGetProperty("last_reset_at", out _) || !feed.TryGetProperty("events", out var list) || list.ValueKind != JsonValueKind.Array)
            throw new JsonException("Reset radar schema unavailable.");
        var updated = Date(forecast, "updated_at") ?? throw new JsonException("Reset radar timestamp missing.");
        var fetched = Date(feed, "fetched_at") ?? DateTimeOffset.MinValue;
        var stale = Flag(feed, "stale") || updated < now.AddHours(-1) || fetched < now.AddHours(-1) || updated > now.AddMinutes(5) || fetched > now.AddMinutes(5);
        var events = new Dictionary<string, ResetSignalEvent>(StringComparer.Ordinal);
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var active = false;
        if (forecast.TryGetProperty("latest_alert", out var alert) && alert.ValueKind == JsonValueKind.Object)
        {
            var kind = Text(alert, "kind"); var state = Text(alert, "state");
            var at = Date(alert, "source_at"); var id = Text(alert, "id");
            var score = Number(alert, "score") ?? Number(forecast, "signal_score") ?? 0;
            var strong = score >= 83 || Text(forecast, "signal_tier") is "strong" or "83" or "93";
            var validState = state is not ("cancelled" or "canceled" or "expired" or "retracted" or "dismissed");
            if (!validState) blocked.Add(id);
            var watchActive = kind == "watch" && strong && at >= now.AddDays(-2)
                && (Text(forecast, "alert_event_id").Length > 0 || Text(forecast, "mode") != "model");
            if (at is not null && id.Length > 0 && validState && (kind == "reset" || watchActive))
            {
                var item = new ResetSignalEvent(id, kind, kind == "watch" ? "strong" : state == "confirmed" ? "confirmed" : "announced", at.Value, SafeSummary(Text(alert, "summary")));
                events[id] = item;
                active = watchActive;
            }
        }
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Flag(item, "preview") || Text(item, "scope") != "global") continue;
            var id = Text(item, "id"); var at = Date(item, "announced_at");
            if (id.Length == 0 || at is null || events.ContainsKey(id) || blocked.Contains(id)) continue;
            var kind = Text(item, "type"); var state = Text(item, "announcement_state");
            var verification = Text(item, "reset_verification_status");
            if (verification is "rejected" or "retracted" || state is "cancelled" or "canceled" or "retracted") continue;
            if (kind == "reset" && (state is "announced" or "confirmed" || verification == "confirmed"))
                events[id] = new(id, "reset", verification == "confirmed" || state == "confirmed" ? "confirmed" : "announced", at.Value, SafeSummary(Text(item, "summary")));
            else if (Text(item, "reset_kind") == "banked" && Text(item, "banked_state") is "arriving" or "available" or "granted" or "confirmed")
                events[id] = new(id, "banked", Text(item, "banked_state"), at.Value, SafeSummary(Text(item, "summary")));
        }
        return new(now, updated, Date(forecast, "last_reset_at"), stale, active, events.Values.OrderByDescending(e => e.At).Take(100).ToList());
    }
    private static string Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static bool Flag(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    private static double? Number(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) && double.IsFinite(n) ? n : null;
    private static DateTimeOffset? Date(JsonElement e, string name) => DateTimeOffset.TryParse(Text(e, name), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date : null;
    private static string SafeSummary(string text)
    {
        var clean = string.Concat(text.Where(c => !char.IsControl(c) || c == '\n')).Replace("\\n", " ").Trim();
        return clean[..Math.Min(clean.Length, 500)];
    }
    public void Dispose() => _client.Dispose();
}
