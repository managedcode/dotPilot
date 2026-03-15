namespace DotPilot.Tests.Chat;

public sealed class ChatComposerKeyboardPolicyTests
{
    [Test]
    public void ResolveReturnsSendMessageForPlainEnter()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            behavior: ComposerSendBehavior.EnterSends,
            isEnterKey: true,
            hasModifier: false);

        action.Should().Be(ChatComposerKeyboardAction.SendMessage);
    }

    [Test]
    public void ResolveReturnsInsertNewLineForModifiedEnterWhenEnterSends()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            behavior: ComposerSendBehavior.EnterSends,
            isEnterKey: true,
            hasModifier: true);

        action.Should().Be(ChatComposerKeyboardAction.InsertNewLine);
    }

    [Test]
    public void ResolveReturnsInsertNewLineForPlainEnterWhenEnterAddsNewLine()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            behavior: ComposerSendBehavior.EnterInsertsNewLine,
            isEnterKey: true,
            hasModifier: false);

        action.Should().Be(ChatComposerKeyboardAction.InsertNewLine);
    }

    [Test]
    public void ResolveReturnsSendMessageForModifiedEnterWhenEnterAddsNewLine()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            behavior: ComposerSendBehavior.EnterInsertsNewLine,
            isEnterKey: true,
            hasModifier: true);

        action.Should().Be(ChatComposerKeyboardAction.SendMessage);
    }

    [Test]
    public void ResolveReturnsNoneWhenKeyIsNotEnter()
    {
        var action = ChatComposerKeyboardPolicy.Resolve(
            behavior: ComposerSendBehavior.EnterSends,
            isEnterKey: false,
            hasModifier: true);

        action.Should().Be(ChatComposerKeyboardAction.None);
    }
}
