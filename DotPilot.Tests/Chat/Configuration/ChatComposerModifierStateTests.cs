using Windows.System;

namespace DotPilot.Tests.Chat;

public sealed class ChatComposerModifierStateTests
{
    [TestCase(VirtualKey.Shift)]
    [TestCase(VirtualKey.LeftShift)]
    [TestCase(VirtualKey.RightShift)]
    [TestCase(VirtualKey.Control)]
    [TestCase(VirtualKey.LeftControl)]
    [TestCase(VirtualKey.RightControl)]
    [TestCase(VirtualKey.Menu)]
    [TestCase(VirtualKey.LeftMenu)]
    [TestCase(VirtualKey.RightMenu)]
    [TestCase(VirtualKey.LeftWindows)]
    [TestCase(VirtualKey.RightWindows)]
    public void RegisterKeyDownMarksSupportedModifierKeysAsPressed(VirtualKey key)
    {
        var state = new ChatComposerModifierState();

        state.RegisterKeyDown(key);

        state.HasPressedModifier.Should().BeTrue();
    }

    [Test]
    public void RegisterKeyDownIgnoresNonModifierKeys()
    {
        var state = new ChatComposerModifierState();

        state.RegisterKeyDown(VirtualKey.A);

        state.HasPressedModifier.Should().BeFalse();
    }

    [Test]
    public void RegisterKeyUpClearsTrackedModifier()
    {
        var state = new ChatComposerModifierState();
        state.RegisterKeyDown(VirtualKey.LeftShift);

        state.RegisterKeyUp(VirtualKey.LeftShift);

        state.HasPressedModifier.Should().BeFalse();
    }

    [Test]
    public void RegisterKeyUpKeepsModifierPressedWhileSameModifierFamilyIsStillHeld()
    {
        var state = new ChatComposerModifierState();
        state.RegisterKeyDown(VirtualKey.LeftShift);
        state.RegisterKeyDown(VirtualKey.RightShift);

        state.RegisterKeyUp(VirtualKey.LeftShift);

        state.HasPressedModifier.Should().BeTrue();
    }

    [Test]
    public void ResetClearsTrackedModifiers()
    {
        var state = new ChatComposerModifierState();
        state.RegisterKeyDown(VirtualKey.LeftShift);
        state.RegisterKeyDown(VirtualKey.LeftControl);

        state.Reset();

        state.HasPressedModifier.Should().BeFalse();
    }
}
