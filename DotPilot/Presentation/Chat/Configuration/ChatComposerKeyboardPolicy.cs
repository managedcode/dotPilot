
namespace DotPilot.Presentation;

public static class ChatComposerKeyboardPolicy
{
    public static ChatComposerKeyboardAction Resolve(
        ComposerSendBehavior behavior,
        bool isEnterKey,
        bool hasModifier)
    {
        if (!isEnterKey)
        {
            return ChatComposerKeyboardAction.None;
        }

        return behavior switch
        {
            ComposerSendBehavior.EnterSends => hasModifier
                ? ChatComposerKeyboardAction.InsertNewLine
                : ChatComposerKeyboardAction.SendMessage,
            ComposerSendBehavior.EnterInsertsNewLine => hasModifier
                ? ChatComposerKeyboardAction.SendMessage
                : ChatComposerKeyboardAction.InsertNewLine,
            _ => ChatComposerKeyboardAction.SendMessage,
        };
    }
}

public enum ChatComposerKeyboardAction
{
    None,
    SendMessage,
    InsertNewLine,
}
