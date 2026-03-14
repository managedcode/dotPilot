using DotPilot.Presentation;

namespace DotPilot.Tests.Features.AgentSessions;

public sealed class ChatComposerKeyboardPolicyTests
{
    [Test]
    public void ResolveReturnsSendMessageForPlainEnter()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            isEnterKey: true,
            isShiftPressed: false,
            isAltPressed: false);

        action.Should().Be(ChatComposerKeyboardAction.SendMessage);
    }

    [Test]
    public void ResolveReturnsInsertNewLineForShiftEnter()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            isEnterKey: true,
            isShiftPressed: true,
            isAltPressed: false);

        action.Should().Be(ChatComposerKeyboardAction.InsertNewLine);
    }

    [Test]
    public void ResolveReturnsInsertNewLineForAltEnter()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            isEnterKey: true,
            isShiftPressed: false,
            isAltPressed: true);

        action.Should().Be(ChatComposerKeyboardAction.InsertNewLine);
    }

    [Test]
    public void ResolveReturnsNoneWhenKeyIsNotEnter()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            isEnterKey: false,
            isShiftPressed: true,
            isAltPressed: true);

        action.Should().Be(ChatComposerKeyboardAction.None);
    }
}
