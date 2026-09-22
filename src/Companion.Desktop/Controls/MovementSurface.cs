using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Companion.Desktop.Controls;

/// <summary>A keyboard movement target exposed as a named pane to accessibility clients.</summary>
public sealed class MovementSurface : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new MovementSurfacePeer(this);

    private sealed class MovementSurfacePeer(MovementSurface owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(MovementSurface);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override bool IsKeyboardFocusableCore() => Owner.Focusable && Owner.IsEnabled;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }
}
