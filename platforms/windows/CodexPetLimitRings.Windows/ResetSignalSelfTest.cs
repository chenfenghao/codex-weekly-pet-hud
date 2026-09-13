using System.Text.Json;
using CodexPetLimitRings.Windows.Services;

namespace CodexPetLimitRings.Windows;

internal static class ResetSignalSelfTest
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = 0;
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks++; }
        var now = new DateTimeOffset(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);
        ResetSignalSnapshot Parse(string kind = "reset", string state = "confirmed", int? score = null, string mode = "model", bool stale = false, string scope = "global", string type = "reset", string banked = "unknown", bool preview = false)
        {
            var forecast = new { mode, updated_at = now, last_reset_at = now.AddDays(-1), alert_event_id = mode == "model" ? null : "watch-1", latest_alert = new { id = "latest", kind, state, score, source_at = now.AddMinutes(-1), summary = "Public announcement" } };
            var feed = new { fetched_at = now, stale, events = new[] { new { id = "feed-1", type, scope, announced_at = now.AddMinutes(-2), announcement_state = "announced", reset_verification_status = "pending", preview, reset_kind = banked == "unknown" ? null : "banked", banked_state = banked, summary = "Public feed item" } } };
            using var f = JsonDocument.Parse(JsonSerializer.Serialize(forecast));
            using var e = JsonDocument.Parse(JsonSerializer.Serialize(feed));
            return ResetSignalService.Parse(f.RootElement, e.RootElement, now);
        }
        var initial = Parse();
        Check(initial.Events.Count == 2 && initial.Events[0].State == "confirmed", "Confirmed forecast and feed parsed");
        Check(!initial.ActiveSignal, "Historical confirmation is not active watch");
        Check(Parse(kind:"watch", state:"strong", score:83, mode:"signal").ActiveSignal, "Strong signal active");
        Check(!Parse(kind:"watch", state:"strong", score:24, mode:"signal").ActiveSignal, "Model probability not a signal");
        Check(Parse(kind:"watch", state:"strong", score:93).Events.All(e => e.Kind != "watch"), "Inactive watch suppressed");
        Check(!Parse(kind:"watch", state:"cancelled", score:93, mode:"signal").ActiveSignal, "Cancelled watch suppressed");
        Check(Parse(scope:"targeted").Events.Count == 1, "Targeted feed not global");
        Check(Parse(preview:true).Events.Count == 1, "Preview excluded");
        Check(Parse(type:"credits",banked:"arriving").Events.Any(e=>e.Kind=="banked"), "Banked announcement separate");
        Check(Parse(type:"credits").Events.Count == 1, "Unknown credits not alert");
        var store = new ResetSignalState();
        Check(ResetSignalPolicy.Accept(store, initial, true).Count == 0, "First run no history notifications");
        Check(ResetSignalPolicy.Accept(store, initial with { CheckedAt=now.AddMinutes(5) }, true).Count == 0, "Repeat polling silent");
        var newEvent = new ResetSignalEvent("new-1", "watch", "strong", now.AddMinutes(2), "New watch");
        var next = initial with { CheckedAt=now.AddMinutes(5), Events=[newEvent, ..initial.Events], ActiveSignal=true };
        Check(ResetSignalPolicy.Accept(store, next, true).Count == 1, "New signal notifies once");
        var restored = JsonSerializer.Deserialize<ResetSignalState>(JsonSerializer.Serialize(store))!;
        Check(ResetSignalPolicy.Accept(restored, next, true).Count == 0, "Restart deduplicates");
        var confirmed = next with { Events=[newEvent with {Kind="reset",State="confirmed"}, ..initial.Events] };
        Check(ResetSignalPolicy.Accept(store, confirmed, true).Count == 1, "Watch to confirmation notifies");
        Check(ResetSignalPolicy.Accept(store, confirmed with { Events=[newEvent with {Kind="reset",State="announced"}] }, true).Count == 0, "Downgraded state does not notify again");
        var stale = confirmed with { Stale=true, Events=[newEvent with {Id="stale-event"}] };
        Check(ResetSignalPolicy.Accept(store, stale, true).Count == 0 && !store.SeenKeys.Any(k=>k.StartsWith("stale-event|")), "Stale data does not consume event");
        var muted = next with { Events=[newEvent with {Id="muted-event"}] };
        Check(ResetSignalPolicy.Accept(store, muted, false).Count == 0, "Muted remains silent");
        Check(ResetSignalPolicy.Accept(store, muted, true).Count == 0, "Unmute does not replay");
        ResetSignalPolicy.Accept(store, initial, true);
        Check(store.Highlight is null, "Removed signal loses highlight");
        Check(ResetSignalPolicy.Accept(store, initial with { Events=[newEvent with {Id="old-backfill",At=now.AddDays(-3)}] }, true).Count == 0, "Historical backfill silent");
        Check(ResetSignalPolicy.Accept(store, initial with { Events=[newEvent with {Id="future",At=now.AddDays(1)}] }, true).Count == 0, "Invalid future timestamp silent");
        File.WriteAllText(Path.Combine(directory,"result.txt"), $"PASS: {checks} reset radar parsing, freshness, first-run, deduplication, mute and transition assertions.");
        return 0;
    }
}
