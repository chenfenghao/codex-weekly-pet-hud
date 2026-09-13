using System.Threading;
using System.Windows;

namespace CodexPetLimitRings.Windows;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private MainController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var overlayTest = Array.IndexOf(e.Args, "--overlay-order-self-test");
        if (overlayTest >= 0 && overlayTest + 1 < e.Args.Length)
        {
            var output = Path.GetFullPath(e.Args[overlayTest + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            Dispatcher.BeginInvoke(async () => Shutdown(await OverlayOrderSelfTest.RunAsync(output)));
            return;
        }
        var radarTest = Array.IndexOf(e.Args, "--reset-self-test");
        if (radarTest >= 0 && radarTest + 1 < e.Args.Length)
        {
            var dir = Path.GetFullPath(e.Args[radarTest + 1]);
            Dispatcher.BeginInvoke(() =>
            {
                try { Shutdown(ResetSignalSelfTest.Run(dir)); }
                catch (Exception error) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir,"result.txt"),error.ToString()); Shutdown(1); }
            });
            return;
        }
        var radarProbe = Array.IndexOf(e.Args, "--reset-probe");
        if (radarProbe >= 0 && radarProbe + 1 < e.Args.Length)
        {
            var output = Path.GetFullPath(e.Args[radarProbe + 1]);
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    using var service = new Services.ResetSignalService();
                    var value = await service.ReadAsync();
                    File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(value));
                    Shutdown(value.Stale ? 2 : 0);
                }
                catch (Exception error) { File.WriteAllText(output,error.GetType().Name + ": " + error.Message); Shutdown(1); }
            });
            return;
        }
        var probeIndex = Array.IndexOf(e.Args, "--usage-probe");
        if (probeIndex >= 0 && probeIndex + 1 < e.Args.Length)
        {
            var output = Path.GetFullPath(e.Args[probeIndex + 1]);
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    using var service = new Services.UsageService();
                    var usage = await service.RefreshAsync();
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(usage));
                    Shutdown(usage is null ? 2 : 0);
                }
                catch (Exception error) { File.WriteAllText(output, error.GetType().Name); Shutdown(1); }
            });
            return;
        }
        var paceTestIndex = Array.IndexOf(e.Args, "--pace-self-test");
        if (paceTestIndex >= 0 && paceTestIndex + 1 < e.Args.Length)
        {
            var directory = Path.GetFullPath(e.Args[paceTestIndex + 1]);
            Dispatcher.BeginInvoke(() =>
            {
                try { Shutdown(PacingSelfTest.Run(directory)); }
                catch (Exception error) { Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "result.txt"), error.ToString()); Shutdown(1); }
            });
            return;
        }
        var inputSelfTestIndex = Array.FindIndex(
            e.Args,
            argument => string.Equals(
                argument,
                "--input-relay-self-test",
                StringComparison.OrdinalIgnoreCase));
        if (inputSelfTestIndex >= 0)
        {
            var outputPath = inputSelfTestIndex + 1 < e.Args.Length
                ? Path.GetFullPath(e.Args[inputSelfTestIndex + 1])
                : Path.Combine(Path.GetTempPath(), "codex-pet-input-relay-self-test.json");
            Dispatcher.BeginInvoke(async () =>
            {
                var exitCode = await InputRelaySelfTest.RunAsync(outputPath);
                Shutdown(exitCode);
            });
            return;
        }

        _singleInstance = new Mutex(true, "CodexPetLimitRings.Windows.SingleInstance", out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        try
        {
            _controller = new MainController();
            var captureIndex = Array.FindIndex(
                e.Args,
                argument => string.Equals(argument, "--verify-capture", StringComparison.OrdinalIgnoreCase));
            var verificationCapturePath = captureIndex >= 0 && captureIndex + 1 < e.Args.Length
                ? Path.GetFullPath(e.Args[captureIndex + 1])
                : null;
            _controller.Start(
                e.Args.Any(argument => string.Equals(argument, "--settings", StringComparison.OrdinalIgnoreCase)),
                verificationCapturePath);
            Services.AppLog.Write("Codex Pet HUD started.");
        }
        catch (Exception error)
        {
            Services.AppLog.Write($"Startup failed: {error.GetType().Name}: {error.Message}");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
