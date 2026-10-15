using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Companion.Core.Configuration;
using Companion.Core.Desktop;
using Companion.Desktop.Controls;
using Companion.Infrastructure.Configuration;
using Companion.Infrastructure.Lifetime;

namespace Companion.Desktop;

/// <summary>Isolated native rendering/settings checks; no physical-input claim.</summary>
internal static class AvatarCustomizationSmokeChecks
{
    internal static async Task RunAsync(CompanionRuntime parent, string dataDirectory)
    {
        List<string> passed = [];
        string? failure = null;
        var original = parent.Settings;
        var profile = Path.Combine(dataDirectory, "avatar-customization-" + Guid.NewGuid().ToString("N"));
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Avatar customization check failed: " + name);
            passed.Add(name);
        }
        try
        {
            CheckRenderer(Check, dataDirectory);
            var instance = SingleInstanceCoordinator.TryAcquire(profile)
                ?? throw new InvalidOperationException("Isolated avatar profile is already in use.");
            await using var runtime = new CompanionRuntime(profile, instance);
            await runtime.InitializeAsync();
            var baseline = runtime.Settings with { AvatarPreset = AvatarPreset.Classic, AvatarAccent = AvatarAccent.Teal, ReducedMotion = true };
            await runtime.SaveAsync(baseline);
            var companion = new CompanionWindow { ShowActivated = false, ShowInTaskbar = false };
            SettingsWindow? editor = null;
            try
            {
                companion.Apply(baseline);
                companion.Opacity = 0;
                companion.Show();
                var bounds = (companion.Width, companion.Height);
                editor = OpenEditor(runtime, companion);
                Check("selectors-use-saved-values", editor.AvatarPresetInput.SelectedValue is AvatarPreset.Classic && editor.AvatarAccentInput.SelectedValue is AvatarAccent.Teal && editor.AvatarDecorationInput.SelectedValue is AvatarDecoration.HaloAndTrail && editor.AvatarFaceInput.SelectedValue is AvatarFace.Friendly && !editor.AvatarDecorationInput.IsEnabled);
                editor.AvatarPresetInput.SelectedValue = AvatarPreset.Mu;
                editor.AvatarAccentInput.SelectedValue = AvatarAccent.Violet;
                editor.AvatarDecorationInput.SelectedValue = AvatarDecoration.TrailOnly;
                editor.AvatarFaceInput.SelectedValue = AvatarFace.Focused;
                Check("local-preview-updates-immediately", editor.AppearancePreview.Preset == AvatarPreset.Mu && editor.AppearancePreview.Accent == AvatarAccent.Violet && editor.AppearancePreview.MuBody.Visibility == Visibility.Visible && editor.AppearancePreview.Decoration == AvatarDecoration.TrailOnly && editor.AppearancePreview.Face == AvatarFace.Focused && editor.AvatarDecorationInput.IsEnabled);
                Check("preview-owns-no-animation-resources", !editor.AppearancePreview.IsAnimating && !editor.AppearancePreview.IsIdleBlinkTimerEnabled && editor.AppearancePreview.OwnedAnimationClockCount == 0);
                var callbackEditor = editor;
                await Task.Run(() => callbackEditor.AppearanceSystemChanged(null, new System.ComponentModel.PropertyChangedEventArgs(nameof(SystemParameters.HighContrast))));
                await editor.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check("system-palette-callback-marshals-from-worker", editor.AppearancePreview.Preset == AvatarPreset.Mu && editor.AppearancePreview.Accent == AvatarAccent.Violet && !editor.AppearancePreview.IsAnimating);
                Check("editing-does-not-apply-to-companion", companion.AvatarRenderer.Preset == AvatarPreset.Classic && companion.AvatarRenderer.Accent == AvatarAccent.Teal && runtime.Settings == baseline);
                Check("preset-selector-accessible", UIElementAutomationPeer.CreatePeerForElement(editor.AvatarPresetInput)?.GetName() == "Avatar character");
                Check("palette-selector-accessible", UIElementAutomationPeer.CreatePeerForElement(editor.AvatarAccentInput)?.GetName() == "Avatar color or theme");
                Check("decoration-selector-accessible", UIElementAutomationPeer.CreatePeerForElement(editor.AvatarDecorationInput)?.GetName() == "Muon decoration");
                Check("face-selector-accessible", UIElementAutomationPeer.CreatePeerForElement(editor.AvatarFaceInput)?.GetName() == "Avatar face");
                editor.Width = editor.MinWidth;
                editor.Height = editor.MinHeight;
                editor.AvatarAccentInput.BringIntoView();
                await editor.Dispatcher.InvokeAsync(() => editor.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                Check("palette-has-minimum-target", editor.AvatarAccentInput.ActualHeight >= 32 && editor.AvatarAccentInput.ActualWidth > 100);
                foreach (var target in new[] { editor.AvatarPresetInput, editor.AvatarAccentInput, editor.AvatarDecorationInput, editor.AvatarFaceInput })
                {
                    target.BringIntoView();
                    await editor.Dispatcher.InvokeAsync(() => editor.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                    DependencyObject? ancestor = target;
                    while (ancestor is not null && ancestor is not ScrollViewer) ancestor = VisualTreeHelper.GetParent(ancestor);
                    var viewport = ancestor as ScrollViewer ?? throw new InvalidOperationException("Settings selectors require a scrollable viewport.");
                    var visible = target.TransformToAncestor(viewport).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
                    Check(target.Name + "-scrolls-fully-into-view", visible.Left >= -1 && visible.Top >= -1 && visible.Right <= viewport.ActualWidth + 1 && visible.Bottom <= viewport.ActualHeight + 1);
                }
                AppearanceSmokeChecks.CheckSettingsLayout(editor);
                Check("small-settings-retains-save", editor.SaveButton.IsVisible && editor.SaveButton.ActualHeight >= 32);
                await editor.SaveChangesAsync().WaitAsync(TimeSpan.FromSeconds(3));
                Check("save-applies-selected-appearance", companion.AvatarRenderer.Preset == AvatarPreset.Mu && companion.AvatarRenderer.Accent == AvatarAccent.Violet && companion.AvatarRenderer.Decoration == AvatarDecoration.TrailOnly && companion.AvatarRenderer.Face == AvatarFace.Focused);
                Check("customization-does-not-resize-compact-window", bounds == (companion.Width, companion.Height));
                Check("save-persists-selected-appearance", runtime.Settings.AvatarPreset == AvatarPreset.Mu && runtime.Settings.AvatarAccent == AvatarAccent.Violet && runtime.Settings.AvatarDecoration == AvatarDecoration.TrailOnly && runtime.Settings.AvatarFace == AvatarFace.Focused);
                using (var store = new JsonSettingsStore(profile))
                {
                    var loaded = await store.LoadAsync(CancellationToken.None);
                    Check("restart-loads-selected-appearance", loaded.Settings == runtime.Settings);
                }
                editor.Close();
                Check("closed-preview-releases-resources", !editor.AppearancePreview.IsIdleBlinkTimerEnabled && editor.AppearancePreview.OwnedAnimationClockCount == 0);
                await Task.Run(() => callbackEditor.AppearanceSystemChanged(null, new System.ComponentModel.PropertyChangedEventArgs(nameof(SystemParameters.HighContrast))));
                await editor.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check("late-system-callback-does-not-revive-closed-preview", !editor.AppearancePreview.IsIdleBlinkTimerEnabled && editor.AppearancePreview.OwnedAnimationClockCount == 0);
                editor = OpenEditor(runtime, companion);
                editor.AvatarPresetInput.SelectedValue = AvatarPreset.Classic;
                editor.AvatarAccentInput.SelectedValue = AvatarAccent.Amber;
                editor.AvatarDecorationInput.SelectedValue = AvatarDecoration.None;
                editor.AvatarFaceInput.SelectedValue = AvatarFace.Friendly;
                editor.Close();
                Check("close-discards-unsaved-appearance", companion.AvatarRenderer.Preset == AvatarPreset.Mu && companion.AvatarRenderer.Accent == AvatarAccent.Violet && runtime.Settings.AvatarPreset == AvatarPreset.Mu && companion.AvatarRenderer.Decoration == AvatarDecoration.TrailOnly && companion.AvatarRenderer.Face == AvatarFace.Focused);
                foreach (var outcome in new[] { "failed", "cancelled" })
                {
                    editor = OpenEditor(runtime, companion, _ => outcome == "failed"
                        ? Task.FromException<bool>(new IOException("Injected appearance save failure."))
                        : Task.FromCanceled<bool>(new CancellationToken(true)));
                    editor.AvatarPresetInput.SelectedValue = AvatarPreset.Classic;
                    editor.AvatarAccentInput.SelectedValue = AvatarAccent.Blue;
                    editor.AvatarDecorationInput.SelectedValue = AvatarDecoration.None;
                    editor.AvatarFaceInput.SelectedValue = AvatarFace.Friendly;
                    await editor.SaveChangesAsync().WaitAsync(TimeSpan.FromSeconds(3));
                    Check(outcome + "-save-keeps-companion", companion.AvatarRenderer.Preset == AvatarPreset.Mu && companion.AvatarRenderer.Accent == AvatarAccent.Violet && companion.AvatarRenderer.Decoration == AvatarDecoration.TrailOnly && companion.AvatarRenderer.Face == AvatarFace.Focused);
                    Check(outcome + "-save-keeps-persisted-settings", runtime.Settings.AvatarPreset == AvatarPreset.Mu && runtime.Settings.AvatarAccent == AvatarAccent.Violet && runtime.Settings.AvatarDecoration == AvatarDecoration.TrailOnly && runtime.Settings.AvatarFace == AvatarFace.Focused);
                    Check(outcome + "-save-retains-editable-preview", editor.SaveButton.IsEnabled && editor.AppearancePreview.Preset == AvatarPreset.Classic && editor.AppearancePreview.Accent == AvatarAccent.Blue);
                    editor.Close();
                }
                await CheckHoverBridgeAsync(companion, runtime.Settings, Check);
                companion.ApplyControlsVisibility(false, false, false, false);
                Check("customization-preserves-hidden-resting-controls", !companion.ControlsVisible);
            }
            finally { editor?.Close(); companion.Close(); }
            Check("parent-profile-remains-unchanged", parent.Settings == original);
        }
        catch (Exception error) { failure = error.GetType().Name + ": " + error.Message; throw; }
        finally
        {
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "avatar-customization-smoke.json"),
                JsonSerializer.Serialize(new { count = passed.Count, passed, failure, profile,
                    limitations = "Native property/layout/rendering checks; no physical keyboard, screen-reader or multi-monitor certification." }));
        }
    }

    private static SettingsWindow OpenEditor(CompanionRuntime runtime, CompanionWindow companion,
        Func<CompanionSettings, Task<bool>>? save = null)
    {
        var window = new SettingsWindow(runtime, companion, saveOperation: save)
        { Owner = companion, ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        return window;
    }

    private static async Task CheckHoverBridgeAsync(CompanionWindow window, CompanionSettings original, Action<string, bool> check)
    {
        var cases = new List<(AvatarPreset Preset, AvatarAccent Accent, AvatarDecoration Decoration, double Scale)>();
        foreach (var scale in new[] { .75, 1, 1.5 })
        {
            foreach (var decoration in Enum.GetValues<AvatarDecoration>())
                cases.Add((AvatarPreset.Mu, AvatarAccent.Galaxy, decoration, scale));
            cases.Add((AvatarPreset.Mu, AvatarAccent.Teal, AvatarDecoration.HaloAndTrail, scale));
            cases.Add((AvatarPreset.Classic, AvatarAccent.Teal, AvatarDecoration.None, scale));
        }
        try
        {
            foreach (var item in cases)
            {
                window.Apply(original with
                {
                    AvatarPreset = item.Preset, AvatarAccent = item.Accent, AvatarDecoration = item.Decoration,
                    AvatarScale = item.Scale, ReducedMotion = true
                });
                window.Opacity = 0;
                await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                var avatar = window.AvatarVisual.TransformToAncestor(window).TransformBounds(new Rect(window.AvatarVisual.RenderSize));
                var label = $"hover-{item.Preset}-{item.Accent}-{item.Decoration}-{item.Scale}";
                window.ApplyControlsVisibility(true, false, false, false);
                window.UpdateLayout();
                var toolbar = window.HoverControls.TransformToAncestor(window).TransformBounds(new Rect(window.HoverControls.RenderSize));
                var (width, _, shown) = RenderWindowPixels(window);
                var x = (int)Math.Floor(avatar.Left + avatar.Width / 2);
                var top = (int)Math.Ceiling(avatar.Top + avatar.Height / 2);
                var bottom = (int)Math.Floor(toolbar.Top + toolbar.Height / 2);
                check(label + "-painted-path", window.HoverBridge.Visibility == Visibility.Visible &&
                    Enumerable.Range(top, bottom - top + 1).All(row => shown[(row * width + x) * 4 + 3] > 0));
                window.ApplyControlsVisibility(false, false, false, false);
                window.UpdateLayout();
                var (hiddenWidth, hiddenHeight, hidden) = RenderWindowPixels(window);
                var blankStart = Math.Min(hiddenHeight, (int)Math.Ceiling(avatar.Bottom) + 1);
                check(label + "-hidden-alpha-clear", window.HoverBridge.Visibility == Visibility.Hidden && !window.ControlsVisible &&
                    Enumerable.Range(blankStart * hiddenWidth, (hiddenHeight - blankStart) * hiddenWidth).All(pixel => hidden[pixel * 4 + 3] == 0));
            }
            window.Apply(original with { AvatarPreset = AvatarPreset.Mu, AvatarAccent = AvatarAccent.Galaxy, ReducedMotion = true });
            window.Opacity = 0;
            check("galaxy-bridge-shares-core-brush", ReferenceEquals(window.HoverBridge.Background, window.AvatarRenderer.Body.Background) &&
                ReferenceEquals(window.HoverBridge.BorderBrush, window.AvatarRenderer.Body.BorderBrush) &&
                (SystemParameters.HighContrast ? ReferenceEquals(window.HoverBridge.Background, SystemColors.WindowBrush) : window.HoverBridge.Background is LinearGradientBrush { IsFrozen: true }));
            window.RenderAvatarAppearance(AvatarState.Idle, true, true);
            check("high-contrast-bridge-uses-system-brushes", ReferenceEquals(window.HoverBridge.Background, SystemColors.WindowBrush) &&
                ReferenceEquals(window.HoverBridge.BorderBrush, SystemColors.WindowTextBrush) &&
                ReferenceEquals(window.HoverBridge.Background, window.AvatarRenderer.Body.Background) &&
                !window.AvatarRenderer.IsAnimating && !window.AvatarRenderer.IsIdleBlinkTimerEnabled && window.AvatarRenderer.OwnedAnimationClockCount == 0);
        }
        finally { window.Apply(original); window.Opacity = 0; window.ApplyControlsVisibility(false, false, false, false); }
    }

    private static (int Width, int Height, byte[] Pixels) RenderWindowPixels(CompanionWindow window)
    {
        var width = (int)Math.Ceiling(window.ActualWidth);
        var height = (int)Math.Ceiling(window.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render((Visual)window.Content);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return (width, height, pixels);
    }

    private static void CheckRenderer(Action<string, bool> check, string dataDirectory)
    {
        using var renderer = new DefaultAvatarRenderer();
        var sheet = new DrawingVisual();
        using (var canvas = sheet.RenderOpen())
        {
            canvas.DrawRectangle(Brushes.White, null, new Rect(0, 0, 800, 300));
            var row = 0;
            foreach (var preset in Enum.GetValues<AvatarPreset>())
            {
                var column = 0;
                var colors = new HashSet<Color>();
                foreach (var accent in Enum.GetValues<AvatarAccent>())
                {
                    renderer.ApplyAppearance(preset, accent);
                    renderer.Render(AvatarState.Idle, false, false);
                    renderer.Measure(new Size(140, 110));
                    renderer.Arrange(new Rect(0, 0, 140, 110));
                    renderer.UpdateLayout();
                    colors.Add(renderer.Body.Background is LinearGradientBrush gradient ? gradient.GradientStops[0].Color : ((SolidColorBrush)renderer.Body.Background).Color);
                    check($"{preset}-{accent}-native-shape", renderer.MuBody.Visibility == (preset == AvatarPreset.Mu ? Visibility.Visible : Visibility.Collapsed) && renderer.Body.Visibility == (preset == AvatarPreset.Classic ? Visibility.Visible : Visibility.Collapsed));
                    check($"{preset}-{accent}-static-motion", renderer.OwnedAnimationClockCount == 0 && !renderer.IsIdleBlinkTimerEnabled);
                    var image = new RenderTargetBitmap(140, 110, 96, 96, PixelFormats.Pbgra32);
                    image.Render(renderer);
                    canvas.DrawImage(image, new Rect(column * 160 + 10, row * 150 + 25, 140, 110));
                    canvas.DrawText(new FormattedText($"{(preset == AvatarPreset.Mu ? "Muon" : "Classic")} · {accent}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, 1), new Point(column * 160 + 10, row * 150 + 6));
                    renderer.Render(AvatarState.Idle, true, true);
                    check($"{preset}-{accent}-system-contrast", ReferenceEquals(renderer.MuBody.Fill, SystemColors.WindowBrush) && ReferenceEquals(renderer.LeftEye.Fill, SystemColors.WindowTextBrush) && !renderer.IsAnimating && renderer.OwnedAnimationClockCount == 0);
                    column++;
                }
                check(preset + "-five-distinct-colors-or-themes", colors.Count == 5);
                foreach (var state in Enum.GetValues<AvatarState>())
                {
                    renderer.Render(state, false, false);
                    check($"{preset}-{state}-semantic-static", renderer.Mouth.Data is not null && !renderer.IsAnimating && renderer.OwnedAnimationClockCount == 0);
                    renderer.Render(state, true, true);
                    check($"{preset}-{state}-high-contrast-no-motion", ReferenceEquals(renderer.LeftEye.Fill, SystemColors.WindowTextBrush) && !renderer.IsAnimating && !renderer.IsIdleBlinkTimerEnabled && renderer.OwnedAnimationClockCount == 0);
                }
                renderer.Render(AvatarState.Sleeping, true, false);
                check(preset + "-sleep-forces-no-motion", !renderer.IsAnimating && !renderer.IsIdleBlinkTimerEnabled && renderer.LeftEye.Height == 3);
                row++;
            }
            renderer.ApplyAppearance(AvatarPreset.Classic, AvatarAccent.Teal);
            renderer.Render(AvatarState.Idle, false, false);
            var originalColor = ((SolidColorBrush)renderer.Body.Background).Color;
            renderer.ApplyAppearance(AvatarPreset.Classic, AvatarAccent.Blue);
            check("same-state-palette-change-repaints", ((SolidColorBrush)renderer.Body.Background).Color != originalColor);
            renderer.ApplyAppearance(AvatarPreset.Mu, AvatarAccent.Blue);
            check("same-state-preset-change-repaints", renderer.MuBody.Visibility == Visibility.Visible && renderer.Body.Visibility == Visibility.Collapsed);
            var core = renderer.MuBody.Data.Bounds;
            check("muon-body-is-round-particle-core", Math.Abs(core.X - 47) < .01 && Math.Abs(core.Y - 24) < .01 && Math.Abs(core.Width - 62) < .01 && Math.Abs(core.Height - 62) < .01);
            renderer.ApplyAppearance(AvatarPreset.Mu, AvatarAccent.Blue, AvatarDecoration.None);
            check("same-state-decoration-change-repaints", renderer.DetectionHalo.Visibility == Visibility.Collapsed && renderer.ParticleTrail.Visibility == Visibility.Collapsed);
            var friendlyHeight = renderer.LeftEye.Height;
            renderer.ApplyAppearance(AvatarPreset.Mu, AvatarAccent.Blue, AvatarDecoration.None, AvatarFace.Focused);
            check("same-state-face-change-repaints", renderer.LeftEye.Height == 4 && renderer.LeftEye.Height != friendlyHeight);
            renderer.Render(AvatarState.Speaking, true, false);
            renderer.ApplyAppearance(AvatarPreset.Mu, AvatarAccent.Teal);
            check("appearance-change-retains-bounded-speaking-policy", renderer.MuBody.Visibility == Visibility.Visible && renderer.OwnedAnimationClockCount == 2);
            renderer.Stop();
            check("stop-releases-customized-animation", renderer.OwnedAnimationClockCount == 0 && !renderer.IsIdleBlinkTimerEnabled && !renderer.IsBlinkClosed);
        }
        CheckAppearanceCombinations(renderer, check);
        var bitmap = new RenderTargetBitmap(800, 300, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(sheet);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(dataDirectory);
        using var file = File.Create(Path.Combine(dataDirectory, "avatar-presets.png"));
        encoder.Save(file);
        WriteMuonOptions(renderer, dataDirectory);
    }

    private static void CheckAppearanceCombinations(DefaultAvatarRenderer renderer, Action<string, bool> check)
    {
        foreach (var preset in Enum.GetValues<AvatarPreset>())
        foreach (var accent in Enum.GetValues<AvatarAccent>())
        foreach (var decoration in Enum.GetValues<AvatarDecoration>())
        foreach (var face in Enum.GetValues<AvatarFace>())
        {
            var label = $"{preset}-{accent}-{decoration}-{face}";
            renderer.ApplyAppearance(preset, accent, decoration, face);
            renderer.Render(AvatarState.Idle, false, false);
            check(label + "-decoration-and-face", renderer.Decoration == decoration && renderer.Face == face &&
                renderer.DetectionHalo.Visibility == (preset == AvatarPreset.Mu && decoration == AvatarDecoration.HaloAndTrail ? Visibility.Visible : Visibility.Collapsed) &&
                renderer.ParticleTrail.Visibility == (preset == AvatarPreset.Mu && decoration != AvatarDecoration.None ? Visibility.Visible : Visibility.Collapsed) &&
                renderer.LeftEye.Height == (preset == AvatarPreset.Mu ? face == AvatarFace.Focused ? 4 : 10 : face == AvatarFace.Focused ? 5 : 16) &&
                renderer.GalaxyStars.Visibility == (accent == AvatarAccent.Galaxy ? Visibility.Visible : Visibility.Collapsed) &&
                (accent != AvatarAccent.Galaxy || renderer.MuBody.Fill is LinearGradientBrush { IsFrozen: true, GradientStops.Count: 3 }));
            var reducedMotionSafe = true;
            var highContrastSafe = true;
            foreach (var state in Enum.GetValues<AvatarState>())
            {
                renderer.Render(state, false, false);
                reducedMotionSafe &= !renderer.IsAnimating && !renderer.IsIdleBlinkTimerEnabled && renderer.OwnedAnimationClockCount == 0 && renderer.Mouth.Data is not null;
                renderer.Render(state, true, true);
                highContrastSafe &= !renderer.IsAnimating && !renderer.IsIdleBlinkTimerEnabled && renderer.OwnedAnimationClockCount == 0 &&
                    ReferenceEquals(renderer.LeftEye.Fill, SystemColors.WindowTextBrush) && ReferenceEquals(renderer.DetectionHalo.Stroke, SystemColors.WindowTextBrush) &&
                    ReferenceEquals(renderer.ParticleTrail.Fill, SystemColors.WindowTextBrush) && ReferenceEquals(renderer.MuBody.Fill, SystemColors.WindowBrush) &&
                    renderer.GalaxyStars.Visibility == Visibility.Collapsed;
            }
            renderer.Render(AvatarState.Sleeping, true, false);
            reducedMotionSafe &= !renderer.IsAnimating && !renderer.IsIdleBlinkTimerEnabled && renderer.OwnedAnimationClockCount == 0 && renderer.LeftEye.Height == 3;
            check(label + "-all-states-reduced-motion", reducedMotionSafe);
            check(label + "-all-states-high-contrast", highContrastSafe);
        }
    }

    private static void WriteMuonOptions(DefaultAvatarRenderer renderer, string dataDirectory)
    {
        var sheet = new DrawingVisual();
        using (var canvas = sheet.RenderOpen())
        {
            canvas.DrawRectangle(Brushes.White, null, new Rect(0, 0, 480, 300));
            foreach (var face in Enum.GetValues<AvatarFace>())
            foreach (var decoration in Enum.GetValues<AvatarDecoration>())
            {
                renderer.ApplyAppearance(AvatarPreset.Mu, AvatarAccent.Teal, decoration, face);
                renderer.Render(AvatarState.Idle, false, false);
                renderer.UpdateLayout();
                var image = new RenderTargetBitmap(140, 110, 96, 96, PixelFormats.Pbgra32);
                image.Render(renderer);
                var x = (int)decoration * 160 + 10;
                var y = (int)face * 150;
                canvas.DrawImage(image, new Rect(x, y + 25, 140, 110));
                var label = decoration switch { AvatarDecoration.HaloAndTrail => "Halo + trail", AvatarDecoration.TrailOnly => "Trail", _ => "None" };
                canvas.DrawText(new FormattedText($"{face} · {label}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 11, Brushes.Black, 1), new Point(x, y + 6));
            }
        }
        var bitmap = new RenderTargetBitmap(480, 300, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(sheet);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(dataDirectory, "muon-options.png"));
        encoder.Save(file);
    }
}
