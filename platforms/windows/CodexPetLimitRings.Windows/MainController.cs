using System.Drawing;
using System.Text.Json;
using System.Windows.Threading;
using CodexPetLimitRings.Windows.Interop;
using CodexPetLimitRings.Windows.Services;
using CodexPetLimitRings.Windows.Views;
using Forms = System.Windows.Forms;

namespace CodexPetLimitRings.Windows;

public sealed class MainController : IDisposable
{
    private readonly SettingsStore _store = new();
    private readonly CodexPetStateReader _stateReader = new();
    private readonly UsageService _usageService = new();
    private readonly ResetSignalService _resetSignalService = new();
    private readonly CancellationTokenSource _lifetime = new();
    private ResetSignalState _resetSignals;
    private DateTimeOffset _lastSignalAttempt = DateTimeOffset.MinValue;
    private bool _signalsRefreshing;
    private bool _signalsFailed;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly PotionWindow _secondaryPotion = new("周", System.Windows.Media.Color.FromRgb(6, 18, 61), System.Windows.Media.Color.FromRgb(10, 82, 194), System.Windows.Media.Color.FromRgb(20, 199, 235), System.Windows.Media.Color.FromRgb(46, 224, 255));
    private readonly PetInputProxyWindow _petInputProxy = new();
    private readonly UnifiedDragController _unifiedDrag = new();
    private readonly UsageDetailsWindow _details = new();
    private readonly SettingsWindow _settingsWindow = new();
    private readonly Forms.NotifyIcon _tray = new();
    private Icon? _trayIcon;
    private OverlaySettings _settings;
    private UsageSnapshot _usage = UsageSnapshot.Empty;
    private PetAnchor? _anchor;
    private DateTimeOffset _lastUsageAttempt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastUsageSuccess = DateTimeOffset.MinValue;
    private DateTimeOffset _lastAnchorDiagnostic = DateTimeOffset.MinValue;
    private CancellationTokenSource? _usageCancellation;
    private string? _verificationCapturePath;
    private bool _refreshing;
    private long _usageRevision;
    private bool _petVisible;
    private bool _yieldingToShell;
    private long _visibilityGeneration;
    private DragPreview? _dragPreview;

    public MainController()
    {
        _settings = _store.LoadSettings();
        UiText.SetLanguage(_settings.Language);
        _resetSignals = _store.LoadResetSignals();
        var saved = _settings.AutoReadUsage ? _store.LoadLatestUsage() ?? _store.LoadManualUsage() : _store.LoadManualUsage();
        _usage = saved is null ? UsageSnapshot.Empty : _settings.AutoReadUsage ? saved with { Source = "stale" } : saved;
    }

    public void Start(bool showSettings = false, string? verificationCapturePath = null)
    {
        _verificationCapturePath = verificationCapturePath;
        AppLog.Write($"HUD state source: {_stateReader.StatePath}");
        ConfigureTray();
        _details.ApplyLanguage();
        _secondaryPotion.PotionClicked += () => { if (_details.IsVisible) _details.Hide(); else ShowDetails(_secondaryPotion); };
        _petInputProxy.PointerPressed += point => BeginUnifiedInteraction(UnifiedDragSurface.Pet, point);
        _petInputProxy.PointerMoved += ContinueUnifiedInteraction;
        _petInputProxy.PointerReleased += EndUnifiedInteraction;
        _petInputProxy.PointerCancelled += CancelUnifiedInteraction;
        _petInputProxy.HoverMoved += ForwardPetHover;
        BindPotionDrag(_secondaryPotion);
        _unifiedDrag.DragStarted += BeginDragPreview;
        _unifiedDrag.DragMoved += MoveDragPreview;
        _unifiedDrag.DragFinished += EndDragPreview;
        _unifiedDrag.StatusChanged += AppLog.Write;
        _details.RefreshRequested += () => _ = RefreshUsageAsync(force: true);
        _details.SettingsRequested += ShowSettings;
        _details.RadarRefreshRequested += () => _ = RefreshResetSignalsAsync(true);
        _details.UsageImported += usage =>
        {
            _usageRevision++; _usage = usage; _settings.AutoReadUsage = false; _store.SaveSettings(_settings); _store.SaveManualUsage(usage);
            _secondaryPotion.UpdateUsage(usage.SecondaryRemaining, usage.SecondaryReset, usage.Source);
            UpdateTrayText();
        };
        _settingsWindow.SettingsChanged += ApplySettings;
        _settingsWindow.Apply(_settings);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
        if (showSettings) System.Windows.Application.Current.Dispatcher.BeginInvoke(() => ShowDetails(_secondaryPotion));
    }

    private void Tick()
    {
        try
        {
            if (_settings.ResetRadarEnabled && DateTimeOffset.Now - _lastSignalAttempt >= _settings.RefreshInterval)
                _ = RefreshResetSignalsAsync(false);
            UpdateResetSignalDisplay();
            if (_details.IsVisible) _details.Update(_usage, _refreshing);
            // Never let the invisible drag proxy cover the notification area or system menus.
            // Keep the pet/usage state intact so closing the flyout resumes without a new fetch.
            if (NativeMethods.IsShellFlyoutOpen())
            {
                if (!_yieldingToShell)
                {
                    _yieldingToShell = true;
                    _unifiedDrag.Cancel();
                    _dragPreview = null;
                    _petInputProxy.Hide();
                    _secondaryPotion.Hide();
                    AppLog.Write("HUD yielded to system flyout; drag proxy hidden.");
                }
                return;
            }
            if (_yieldingToShell)
            {
                _yieldingToShell = false;
                AppLog.Write("System flyout closed; HUD may resume.");
            }
            if (_unifiedDrag.IsDragging) return;
            var candidate = _stateReader.ReadVisibleCandidate();
            var anchor = NativeMethods.FindVisiblePetAnchor(candidate, out var anchorDiagnostic);
            if (_dragPreview is not null)
            {
                var settled =
                    anchor is not null &&
                    (Math.Abs(anchor.X - _dragPreview.Anchor.X) > 0.5 ||
                     Math.Abs(anchor.Y - _dragPreview.Anchor.Y) > 0.5);
                if (settled)
                {
                    AppLog.Write(
                        $"Unified drag settled: anchor={anchor!.X:0.##},{anchor.Y:0.##}, " +
                        $"expected={_dragPreview.ExpectedAnchorX:0.##},{_dragPreview.ExpectedAnchorY:0.##}.");
                    _dragPreview = null;
                }
                else if (DateTimeOffset.Now < _dragPreview.HoldUntil)
                {
                    return;
                }
                else
                {
                    AppLog.Write(
                        $"Unified drag settle timed out: expected=" +
                        $"{_dragPreview.ExpectedAnchorX:0.##},{_dragPreview.ExpectedAnchorY:0.##}; " +
                        $"actual={(anchor is null ? "unavailable" : $"{anchor.X:0.##},{anchor.Y:0.##}")}.");
                    _dragPreview = null;
                }
            }
            if (anchor is null)
            {
                if (DateTimeOffset.Now - _lastAnchorDiagnostic >= TimeSpan.FromSeconds(30))
                {
                    var state = candidate is null
                        ? "no visible pet candidate"
                        : $"candidate x={candidate.WindowX:0.##}, y={candidate.WindowY:0.##}, direct={candidate.DirectCoordinates}";
                    AppLog.Write($"Pet anchor unavailable: {state}; {anchorDiagnostic}.");
                    _lastAnchorDiagnostic = DateTimeOffset.Now;
                }
                HideHud();
                return;
            }

            var justShown = !_petVisible;
            if (justShown)
            {
                AppLog.Write(
                    $"Pet anchor acquired: x={anchor.X:0.##}, y={anchor.Y:0.##}, " +
                    $"size={anchor.Width:0.##}x{anchor.Height:0.##}.");
            }
            _petVisible = true;
            _anchor = anchor;
            PlacePotions(anchor);
            PlacePetInputProxy(anchor);
            if (!_secondaryPotion.IsVisible) _secondaryPotion.Show();
            MarkUsageStaleIfNeeded();
            _secondaryPotion.UpdateUsage(_usage.SecondaryRemaining, _usage.SecondaryReset, _usage.Source);
            if (_details.IsVisible) _details.Update(_usage, _refreshing);
            UpdatePotionInputRouting(anchor);
            if (!_petInputProxy.IsVisible)
            {
                _petInputProxy.Show();
                NativeMethods.ConfigurePetInputProxy(_petInputProxy);
            }
            if (_petInputProxy.IsVisible) NativeMethods.PlacePetInputProxy(_petInputProxy);
            if (_usage.Source != "manual" && (justShown || DateTimeOffset.Now - _lastUsageAttempt >= _settings.RefreshInterval))
            {
                _ = RefreshUsageAsync(force: justShown);
            }
        }
        catch (Exception error)
        {
            AppLog.Write($"Tick failed: {error.GetType().Name}: {error.Message}");
            HideHud();
        }
    }

    private void HideHud()
    {
        if (_petVisible)
        {
            AppLog.Write("Pet anchor lost; potion HUD hidden.");
            _visibilityGeneration++;
            _usageCancellation?.Cancel();
        }
        _petVisible = false;
        _unifiedDrag.Cancel();
        _dragPreview = null;
        _anchor = null;
        _petInputProxy.Hide();
        _secondaryPotion.Hide();
        UpdateTrayText();
    }

    private void PlacePotions(PetAnchor anchor)
    {
        var placement = HudLayout.CalculateCapsule(anchor, _settings);
        _secondaryPotion.ApplyScale(placement.Scale);
        _secondaryPotion.Left = placement.SecondaryX;
        _secondaryPotion.Top = placement.Y;
    }

    private void PlacePetInputProxy(PetAnchor anchor)
    {
        _petInputProxy.Apply(PetInputProxyLayout.Calculate(anchor));
    }

    private void BindPotionDrag(PotionWindow potion)
    {
        potion.PointerPressed += point => BeginUnifiedInteraction(UnifiedDragSurface.Potion, point);
        potion.PointerMoved += ContinueUnifiedInteraction;
        potion.PointerReleased += EndUnifiedInteraction;
        potion.PointerCancelled += CancelUnifiedInteraction;
    }

    private void BeginUnifiedInteraction(UnifiedDragSurface surface, ScreenPointer pointer)
    {
        if (_yieldingToShell || !_petVisible ||
            _anchor is null ||
            !NativeMethods.TryGetPhysicalWindowRect(_petInputProxy, out var petBounds))
        {
            return;
        }
        _unifiedDrag.Press(surface, pointer, _anchor, petBounds);
    }

    private void ContinueUnifiedInteraction(ScreenPointer pointer) =>
        _unifiedDrag.Move(pointer);

    private void EndUnifiedInteraction(ScreenPointer pointer) =>
        _unifiedDrag.Release(pointer);

    private void CancelUnifiedInteraction() =>
        _unifiedDrag.Cancel();

    private void BeginDragPreview(PetAnchor anchor)
    {
        _details.Hide();
        _dragPreview = new DragPreview(
            anchor,
            _secondaryPotion.Left,
            _secondaryPotion.Top,
            _petInputProxy.Left,
            _petInputProxy.Top,
            anchor.X,
            anchor.Y,
            DateTimeOffset.MaxValue);
    }

    private void MoveDragPreview(double deltaX, double deltaY)
    {
        var preview = _dragPreview;
        if (preview is null) return;
        _secondaryPotion.Left = preview.SecondaryLeft + deltaX;
        _secondaryPotion.Top = preview.SecondaryTop + deltaY;
        _petInputProxy.Left = preview.ProxyLeft + deltaX;
        _petInputProxy.Top = preview.ProxyTop + deltaY;
    }

    private void EndDragPreview(
        PetAnchor anchor,
        double deltaX,
        double deltaY,
        bool delivered)
    {
        var preview = _dragPreview;
        if (preview is null) return;
        if (!delivered)
        {
            _dragPreview = null;
            Tick();
            return;
        }
        MoveDragPreview(deltaX, deltaY);
        _dragPreview = preview with
        {
            ExpectedAnchorX = anchor.X + deltaX,
            ExpectedAnchorY = anchor.Y + deltaY,
            HoldUntil = DateTimeOffset.Now + TimeSpan.FromSeconds(2)
        };
    }

    private void ForwardPetHover()
    {
        if (!_petVisible || _anchor is null || _unifiedDrag.IsPressed) return;
        NativeMethods.ForwardPetHover(_anchor.NativeWindowHandle);
    }

    private void UpdatePotionInputRouting(PetAnchor anchor)
    {
        NativeMethods.PlacePotionWindow(
            _secondaryPotion,
            anchor.NativeWindowHandle,
            directPotionClicksEnabled: true);
    }

    private async Task RefreshUsageAsync(bool force)
    {
        if (_refreshing || (!force && (!_petVisible || _usage.Source == "manual"))) return;
        if (!force && DateTimeOffset.Now - _lastUsageAttempt < _settings.RefreshInterval) return;
        var generation = _visibilityGeneration;
        var revision = _usageRevision;
        using var cancellation = new CancellationTokenSource();
        _usageCancellation = cancellation;
        _refreshing = true;
        _lastUsageAttempt = DateTimeOffset.Now;
        _details.Update(_usage, true);
        try
        {
            var refreshed = await _usageService.RefreshAsync(cancellation.Token);
            if (refreshed is not null && revision == _usageRevision && (force || (_petVisible && generation == _visibilityGeneration)))
            {
                _usage = refreshed;
                _settings.AutoReadUsage = true; _store.SaveSettings(_settings); _store.SaveLatestUsage(refreshed);
                _lastUsageSuccess = DateTimeOffset.Now;
                _secondaryPotion.UpdateUsage(_usage.SecondaryRemaining, _usage.SecondaryReset, _usage.Source);
                AppLog.Write($"Weekly usage refreshed: {_usage.Source}; remaining={_usage.SecondaryRemaining:0.#}%; reset={_usage.SecondaryReset}.");
                if (_verificationCapturePath is { } capturePath)
                {
                    _verificationCapturePath = null;
                    _ = CaptureVerificationAsync(capturePath);
                }
            }
            else if (refreshed is null)
            {
                MarkUsageStaleIfNeeded();
            }
        }
        catch (HttpRequestException error) { AppLog.Write($"Usage request failed: {error.Message}"); MarkUsageStaleIfNeeded(); }
        catch (TaskCanceledException) { }
        catch (JsonException error) { AppLog.Write($"Usage JSON failed: {error.Message}"); MarkUsageStaleIfNeeded(); }
        catch (InvalidOperationException error) { AppLog.Write($"Usage payload failed: {error.Message}"); MarkUsageStaleIfNeeded(); }
        finally
        {
            _refreshing = false;
            if (generation != _visibilityGeneration) _lastUsageAttempt = DateTimeOffset.MinValue;
            if (ReferenceEquals(_usageCancellation, cancellation)) _usageCancellation = null;
            _details.Update(_usage, false);
            UpdateTrayText();
        }
    }

    private void MarkUsageStaleIfNeeded()
    {
        if (_usage.Source is "none" or "manual" ||
            _lastUsageSuccess == DateTimeOffset.MinValue ||
            DateTimeOffset.Now - _lastUsageSuccess < _settings.RefreshInterval * 2 ||
            _usage.Source == "stale") return;
        _usage = _usage with { Source = "stale" };
        _secondaryPotion.UpdateUsage(_usage.SecondaryRemaining, _usage.SecondaryReset, _usage.Source);
        AppLog.Write("Usage data marked stale after repeated refresh failures.");
    }

    private async Task RefreshResetSignalsAsync(bool force)
    {
        if (_signalsRefreshing || !_settings.ResetRadarEnabled || _lifetime.IsCancellationRequested) return;
        if (!force && DateTimeOffset.Now - _lastSignalAttempt < _settings.RefreshInterval) return;
        _signalsRefreshing = true;
        _lastSignalAttempt = DateTimeOffset.Now;
        try
        {
            var snapshot = await _resetSignalService.ReadAsync(_lifetime.Token);
            if (_lifetime.IsCancellationRequested || !_settings.ResetRadarEnabled) return;
            _signalsFailed = snapshot.Stale;
            var alerts = ResetSignalPolicy.Accept(_resetSignals, snapshot, _settings.ResetNotificationsEnabled);
            _store.SaveResetSignals(_resetSignals);
            AppLog.Write($"Reset radar checked: events={snapshot.Events.Count}; stale={snapshot.Stale}; new={alerts.Count}.");
            if (alerts.Count > 0)
            {
                var latest = alerts[^1];
                var title = alerts.Count > 1 ? UiText.F("发现 {0} 条新的重置动态", alerts.Count) : latest.Title;
                _tray.ShowBalloonTip(10000, title, UiText.F("codex-reset.com · {0:M/d HH:mm}\n点击查看。社区公告不代表你的账户已到账。", latest.At.LocalDateTime), Forms.ToolTipIcon.Info);
            }
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or IOException)
        {
            if (!_lifetime.IsCancellationRequested) { _signalsFailed = true; AppLog.Write($"Reset radar unavailable: {error.GetType().Name}."); }
        }
        finally
        {
            _signalsRefreshing = false;
            if (!_lifetime.IsCancellationRequested) UpdateResetSignalDisplay();
        }
    }

    private void UpdateResetSignalDisplay()
    {
        var snapshot = _resetSignals.Snapshot;
        var stale = _signalsFailed || snapshot is not null && DateTimeOffset.UtcNow - snapshot.CheckedAt > _settings.RefreshInterval * 2 + TimeSpan.FromMinutes(1);
        var highlight = _resetSignals.Highlight is { } h && h.At > DateTimeOffset.UtcNow.AddDays(-1) ? h : null;
        var attention = _settings.ResetRadarEnabled && !stale && (highlight is not null || snapshot?.ActiveSignal == true);
        var headline = !_settings.ResetRadarEnabled ? "重置雷达 · 已关闭"
            : stale ? "重置雷达 · 连接延迟"
            : snapshot is null ? "重置雷达 · 连接中"
            : highlight is not null ? highlight.Kind == "watch" ? "雷达 · 新重置信号" : highlight.Kind == "banked" ? "雷达 · 新储备重置公告" : "雷达 · 新重置公告"
            : snapshot.ActiveSignal ? "雷达 · 有重置信号" : "重置雷达 · 暂无新信号";
        headline = UiText.T(headline);
        var details = snapshot is null ? UiText.T("等待 codex-reset.com 公开 API 数据。") :
            UiText.F("最近检查 {0:M/d HH:mm} · 每 {1} 分钟\n上次全局重置公告：{2}", snapshot.CheckedAt.LocalDateTime, _settings.RefreshMinutes, snapshot.LastResetAt is { } reset ? UiText.Date(reset.LocalDateTime) : UiText.T("暂无记录"));
        var latest = highlight ?? snapshot?.Events.FirstOrDefault();
        if (latest is not null) details += $"\n{latest.Title} · {latest.At.LocalDateTime:M/d HH:mm}\n{latest.Summary}";
        if (stale) details += UiText.T("\n连接或数据延迟，保留上次记录，暂停新信号提醒。");
        _secondaryPotion.UpdateResetSignal(headline, details, attention);
        _details.UpdateResetSignal(headline, details, attention, _signalsRefreshing);
    }

    private void ShowDetails(PotionWindow source)
    {
        if (_resetSignals.Highlight is not null) { _resetSignals.Highlight = null; _store.SaveResetSignals(_resetSignals); }
        _details.Update(_usage, _refreshing);
        if (_anchor is null)
        {
            _details.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            if (!_details.IsVisible) _details.Show();
            _details.Activate(); return;
        }
        PositionDetails(source, _details.MinHeight);
        if (!_details.IsVisible) _details.Show();
        _details.UpdateLayout();
        PositionDetails(source, _details.ActualHeight);
        _details.Activate();
    }

    private void PositionDetails(PotionWindow source, double detailsHeight)
    {
        if (_anchor is null) return;
        var detailsWidth = double.IsNaN(_details.Width) ? _details.ActualWidth : _details.Width;
        var preferredLeft = source.Left + source.Width + 12;
        if (preferredLeft + detailsWidth > _anchor.WorkRight)
            preferredLeft = source.Left - detailsWidth - 12;
        _details.Left = Math.Clamp(preferredLeft, _anchor.WorkX, Math.Max(_anchor.WorkX, _anchor.WorkRight - detailsWidth));
        _details.Top = Math.Clamp(source.Top + (source.Height - detailsHeight) / 2, _anchor.WorkY, Math.Max(_anchor.WorkY, _anchor.WorkBottom - detailsHeight));
    }

    private void ShowSettings()
    {
        _settingsWindow.Apply(_settings);
        if (!_settingsWindow.IsVisible) _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ApplySettings(OverlaySettings settings)
    {
        settings.Normalize();
        _settings = settings;
        _store.SaveSettings(_settings);
        UiText.SetLanguage(_settings.Language);
        _details.ApplyLanguage();
        _details.Update(_usage, _refreshing);
        _secondaryPotion.UpdateUsage(_usage.SecondaryRemaining, _usage.SecondaryReset, _usage.Source);
        if (_tray.ContextMenuStrip is { } menu)
            foreach (Forms.ToolStripItem item in menu.Items)
                if (item.Tag is string key) item.Text = UiText.T(key);
        UpdateTrayText();
        UpdateResetSignalDisplay();
        if (_anchor is not null)
        {
            PlacePotions(_anchor);
            if (_details.IsVisible) PositionDetails(_secondaryPotion, _details.ActualHeight);
        }
    }

    private void ConfigureTray()
    {
        _trayIcon = string.IsNullOrWhiteSpace(Environment.ProcessPath)
            ? null : Icon.ExtractAssociatedIcon(Environment.ProcessPath);
        _tray.Icon = _trayIcon ?? SystemIcons.Application;
        _tray.Text = UiText.T("Codex 周额度");
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowDetails(_secondaryPotion);
        _tray.BalloonTipClicked += (_, _) => ShowDetails(_secondaryPotion);
        var menu = new Forms.ContextMenuStrip();
        void AddItem(string key, EventHandler handler) { var item = menu.Items.Add(UiText.T(key), null, handler); item.Tag = key; }
        AddItem("查看周额度详情", (_, _) => ShowDetails(_secondaryPotion));
        AddItem("立即刷新", (_, _) => _ = RefreshUsageAsync(force: true));
        AddItem("挂件设置（更新间隔 / 外观）…", (_, _) => ShowSettings());
        AddItem("重置挂件位置", (_, _) => {
            _settings.HorizontalOffset = 0; _settings.VerticalOffset = 0;
            ApplySettings(_settings);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        AddItem("退出", (_, _) => System.Windows.Application.Current.Shutdown());
        _tray.ContextMenuStrip = menu;
    }

    private void UpdateTrayText()
    {
        if (!_petVisible) { _tray.Text = UiText.T("Codex 周额度 · 请打开 Codex 宠物"); return; }
        var weekly = _usage.SecondaryRemaining is null ? "--" : $"{_usage.SecondaryRemaining:0}%";
        var state = _usage.Source == "stale" ? UiText.T(" · 更新延迟") : string.Empty;
        _tray.Text = UiText.F("Codex 周额度 · 剩余 {0}{1}", weekly, state);
    }

    private static string FormatUsageForLog(double? remaining) =>
        remaining is null
            ? "--"
            : $"{Math.Round(remaining.Value, 2):0.##}%";

    private async Task CaptureVerificationAsync(string path)
    {
        await Task.Delay(750);
        try
        {
            if (!_petVisible || _anchor is null)
            {
                AppLog.Write("Verification capture skipped: pet anchor is no longer visible.");
                return;
            }
            _secondaryPotion.UpdateLayout();
            if (!NativeMethods.TryGetPhysicalWindowRect(_secondaryPotion, out var primaryBounds) ||
                _secondaryPotion.ActualWidth <= 0 ||
                _secondaryPotion.ActualHeight <= 0)
            {
                AppLog.Write("Verification capture skipped: potion window bounds unavailable.");
                return;
            }

            var scaleX = primaryBounds.Width / _secondaryPotion.ActualWidth;
            var scaleY = primaryBounds.Height / _secondaryPotion.ActualHeight;
            var anchorBounds = new Rectangle(
                primaryBounds.Left + (int)Math.Round((_anchor.X - _secondaryPotion.Left) * scaleX),
                primaryBounds.Top + (int)Math.Round((_anchor.Y - _secondaryPotion.Top) * scaleY),
                Math.Max(1, (int)Math.Round(_anchor.Width * scaleX)),
                Math.Max(1, (int)Math.Round(_anchor.Height * scaleY)));
            var captureBounds = Rectangle.Union(primaryBounds, anchorBounds);
            captureBounds.Inflate(72, 72);
            captureBounds = Rectangle.Intersect(captureBounds, Forms.SystemInformation.VirtualScreen);
            if (captureBounds.Width <= 0 || captureBounds.Height <= 0)
            {
                AppLog.Write("Verification capture skipped: calculated capture area is empty.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var bitmap = new Bitmap(captureBounds.Width, captureBounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(captureBounds.Left, captureBounds.Top, 0, 0, captureBounds.Size);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            AppLog.Write(
                $"Verification capture saved: {path}; area={captureBounds.Left},{captureBounds.Top}," +
                $"{captureBounds.Width}x{captureBounds.Height}.");
        }
        catch (Exception error)
        {
            AppLog.Write($"Verification capture failed: {error.GetType().Name}: {error.Message}");
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _resetSignalService.Dispose();
        _timer.Stop();
        _petVisible = false;
        _usageCancellation?.Cancel();
        _unifiedDrag.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _trayIcon?.Dispose();
        _usageService.Dispose();
        _petInputProxy.Close();
        _secondaryPotion.Close();
        _details.ClosePermanently();
        _settingsWindow.ClosePermanently();
        _lifetime.Dispose();
    }

    private sealed record DragPreview(
        PetAnchor Anchor,
        double SecondaryLeft,
        double SecondaryTop,
        double ProxyLeft,
        double ProxyTop,
        double ExpectedAnchorX,
        double ExpectedAnchorY,
        DateTimeOffset HoldUntil);
}
