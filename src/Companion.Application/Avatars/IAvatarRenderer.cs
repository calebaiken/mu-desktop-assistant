using Companion.Core.Desktop;

namespace Companion.Application.Avatars;

/// <summary>Semantic presentation contract; the host owns placement, input and lifetime.</summary>
public interface IAvatarRenderer
{
    void Render(AvatarState state, bool animate, bool highContrast);
    void Stop();
}
