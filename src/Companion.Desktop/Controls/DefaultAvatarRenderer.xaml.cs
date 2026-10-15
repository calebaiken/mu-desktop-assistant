using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Companion.Application.Avatars;
using Companion.Core.Configuration;
using Companion.Core.Desktop;

namespace Companion.Desktop.Controls;

public partial class DefaultAvatarRenderer : UserControl, IAvatarRenderer, IDisposable
{
    private static readonly TimeSpan IdleBlinkWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan IdleBlinkDuration = TimeSpan.FromMilliseconds(120);
    private readonly DispatcherTimer idleBlinkTimer;
    private readonly List<AnimationClock> animationClocks = [];
    private (AvatarState State, bool Animate, bool Contrast)? presentation;
    private bool disposed;
    private AvatarPreset preset = AvatarPreset.Mu;
    private AvatarAccent accent = AvatarAccent.Teal;
    private AvatarDecoration decoration = AvatarDecoration.HaloAndTrail;
    private AvatarFace face = AvatarFace.Friendly;
    internal AvatarPreset Preset => preset;
    internal AvatarAccent Accent => accent;
    internal AvatarDecoration Decoration => decoration;
    internal AvatarFace Face => face;

    // This is the motion policy, including time spent waiting for the next idle blink.
    public bool IsAnimating { get; private set; }
    internal long CompletedBlinkCount { get; private set; }
    internal bool IsBlinkClosed { get; private set; }
    internal bool IsIdleBlinkTimerEnabled => idleBlinkTimer.IsEnabled;
    internal int OwnedAnimationClockCount => animationClocks.Count;

    public DefaultAvatarRenderer()
    {
        InitializeComponent();
        idleBlinkTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        idleBlinkTimer.Tick += IdleBlinkTick;
        Unloaded += RendererUnloaded;
    }

    internal void ApplyAppearance(AvatarPreset selectedPreset, AvatarAccent selectedAccent,
        AvatarDecoration selectedDecoration = AvatarDecoration.HaloAndTrail, AvatarFace selectedFace = AvatarFace.Friendly)
    {
        Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Enum.IsDefined(selectedPreset) || !Enum.IsDefined(selectedAccent) ||
            !Enum.IsDefined(selectedDecoration) || !Enum.IsDefined(selectedFace)) throw new ArgumentException("Unsupported avatar appearance.");
        if (preset == selectedPreset && accent == selectedAccent && decoration == selectedDecoration && face == selectedFace) return;
        var previous = presentation;
        Stop();
        preset = selectedPreset;
        accent = selectedAccent;
        decoration = selectedDecoration;
        face = selectedFace;
        if (previous is { } current) Render(current.State, current.Animate, current.Contrast);
    }

    public void Render(AvatarState state, bool animate, bool highContrast)
    {
        Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        animate &= state != AvatarState.Sleeping && !highContrast;
        if (presentation == (state, animate, highContrast)) return;
        Stop();
        presentation = (state, animate, highContrast);
        var (fillColor, inkColor) = accent switch
        {
            AvatarAccent.Blue => (Color.FromRgb(220, 234, 249), Color.FromRgb(32, 64, 106)),
            AvatarAccent.Violet => (Color.FromRgb(235, 224, 248), Color.FromRgb(76, 44, 108)),
            AvatarAccent.Amber => (Color.FromRgb(251, 234, 198), Color.FromRgb(103, 65, 20)),
            AvatarAccent.Galaxy => (Color.FromRgb(16, 25, 56), Color.FromRgb(206, 245, 255)),
            _ => (Color.FromRgb(221, 234, 230), Color.FromRgb(36, 72, 61))
        };
        var ink = highContrast ? SystemColors.WindowTextBrush : FrozenBrush(inkColor);
        Body.Background = highContrast ? SystemColors.WindowBrush : accent == AvatarAccent.Galaxy ? GalaxyBrush() : FrozenBrush(fillColor);
        Body.BorderBrush = ink;
        MuBody.Fill = Body.Background;
        MuBody.Stroke = ink;
        DetectionHalo.Stroke = ParticleTrail.Fill = ink;
        GalaxyStars.Fill = ink;
        GalaxyStars.Visibility = accent == AvatarAccent.Galaxy && !highContrast ? Visibility.Visible : Visibility.Collapsed;
        var mu = preset == AvatarPreset.Mu;
        var focused = face == AvatarFace.Focused;
        Body.Visibility = mu ? Visibility.Collapsed : Visibility.Visible;
        MuBody.Visibility = mu ? Visibility.Visible : Visibility.Collapsed;
        // This decorative detection halo and trail evoke particle tracks, not an atomic orbit.
        DetectionHalo.Visibility = mu && decoration == AvatarDecoration.HaloAndTrail ? Visibility.Visible : Visibility.Collapsed;
        ParticleTrail.Visibility = mu && decoration != AvatarDecoration.None ? Visibility.Visible : Visibility.Collapsed;
        LeftEye.Margin = mu ? new Thickness(-4, -12, 0, 0) : new Thickness(-45, -4, 0, 0);
        RightEye.Margin = mu ? new Thickness(36, -12, 0, 0) : new Thickness(45, -4, 0, 0);
        LeftEye.Width = RightEye.Width = mu ? focused ? 12 : 6 : focused ? 14 : 10;
        Mouth.StrokeThickness = mu ? 2.5 : 3;
        LeftEye.Fill = RightEye.Fill = Mouth.Stroke = Badge.Foreground = ink;
        LeftEye.Height = RightEye.Height = state == AvatarState.Sleeping ? 3 : mu ? focused ? 4 : 10 : focused ? 5 : 16;
        Mouth.Data = Geometry.Parse(mu ? state switch
        {
            AvatarState.Sleeping => "M69,67 L87,67",
            AvatarState.Speaking => "M70,61 Q78,81 86,61 Z",
            AvatarState.Error => "M66,69 Q78,56 90,69",
            AvatarState.Thinking => "M72,66 L89,63",
            _ => focused ? "M67,65 L89,65" : "M65,62 Q78,75 91,62"
        } : state switch
        {
            AvatarState.Sleeping => "M 60,74 L 80,74",
            AvatarState.Speaking => "M 60,69 Q 70,92 80,69 Z",
            AvatarState.Error => "M 55,80 Q 70,65 85,80",
            AvatarState.Thinking => "M 64,77 L 82,74",
            _ => focused ? "M 55,74 L 85,74" : "M 53,69 Q 70,84 87,69"
        });
        Badge.Text = state switch
        {
            AvatarState.Listening => "♪", AvatarState.Thinking => "…", AvatarState.Working => "↻",
            AvatarState.Speaking => "·", AvatarState.Notifying => "!", AvatarState.Sleeping => "z", AvatarState.Error => "!", _ => ""
        };
        IsAnimating = animate;
        if (!animate) return;
        if (state == AvatarState.Idle)
        {
            // Idle has no animation clocks: two visual updates per blink replace continuous bobbing.
            ScheduleIdleBlink(IdleBlinkWait);
            return;
        }
        var (distance, seconds) = state switch
        {
            AvatarState.Listening => (-2d, 0.8), AvatarState.Thinking => (-2d, 1.5),
            AvatarState.Working => (-3d, 0.7), AvatarState.Notifying => (-5d, 0.65),
            AvatarState.Error => (-1d, 1.8), _ => (-3d, 2.4)
        };
        Motion.ApplyAnimationClock(TranslateTransform.YProperty, OwnClock(Animation(0, distance, seconds)),
            HandoffBehavior.SnapshotAndReplace);
        if (state == AvatarState.Speaking)
            Mouth.ApplyAnimationClock(OpacityProperty, OwnClock(Animation(0.35, 1, 0.18)), HandoffBehavior.SnapshotAndReplace);
        if (state == AvatarState.Listening)
            Badge.ApplyAnimationClock(OpacityProperty, OwnClock(Animation(0.35, 1, 0.8)), HandoffBehavior.SnapshotAndReplace);
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush GalaxyBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops =
            {
                new(Color.FromRgb(16, 25, 56), 0),
                new(Color.FromRgb(73, 53, 131), .52),
                new(Color.FromRgb(27, 106, 117), 1)
            }
        };
        brush.Freeze();
        return brush;
    }

    private void ScheduleIdleBlink(TimeSpan interval)
    {
        idleBlinkTimer.Interval = interval;
        idleBlinkTimer.Start();
    }

    private void IdleBlinkTick(object? sender, EventArgs e)
    {
        // Treat each phase as one-shot. A busy dispatcher delays a blink instead of catching up.
        idleBlinkTimer.Stop();
        if (disposed || !IsAnimating || presentation != (AvatarState.Idle, true, false))
        {
            OpenBlink();
            return;
        }
        if (IsBlinkClosed)
        {
            OpenBlink();
            CompletedBlinkCount++;
            ScheduleIdleBlink(IdleBlinkWait);
        }
        else
        {
            LeftBlink.ScaleY = RightBlink.ScaleY = 0.18;
            IsBlinkClosed = true;
            ScheduleIdleBlink(IdleBlinkDuration);
        }
    }

    private void OpenBlink()
    {
        LeftBlink.ScaleY = RightBlink.ScaleY = 1;
        IsBlinkClosed = false;
    }

    private AnimationClock OwnClock(DoubleAnimation animation)
    {
        var clock = (AnimationClock)((Timeline)animation).CreateClock(hasControllableRoot: true);
        animationClocks.Add(clock);
        return clock;
    }

    private static DoubleAnimation Animation(double from, double to, double seconds)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Timeline.SetDesiredFrameRate(animation, 20);
        return animation;
    }

    public void Stop()
    {
        Dispatcher.VerifyAccess();
        if (disposed) return;
        idleBlinkTimer.Stop();
        foreach (var clock in animationClocks)
        {
            clock.Controller?.Stop();
            clock.Controller?.Remove();
        }
        animationClocks.Clear();
        Motion.BeginAnimation(TranslateTransform.YProperty, null);
        Mouth.BeginAnimation(OpacityProperty, null);
        Badge.BeginAnimation(OpacityProperty, null);
        Motion.Y = 0;
        Mouth.Opacity = Badge.Opacity = 1;
        // Forced restoration after interruption is not a completed timed blink.
        OpenBlink();
        IsAnimating = false;
        presentation = null;
    }

    private void RendererUnloaded(object sender, RoutedEventArgs e) => Stop();

    public void Dispose()
    {
        Dispatcher.VerifyAccess();
        if (disposed) return;
        Stop();
        idleBlinkTimer.Tick -= IdleBlinkTick;
        Unloaded -= RendererUnloaded;
        disposed = true;
    }
}
