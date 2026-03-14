namespace DotPilot.Presentation;

public static class ChatComposerKeyboardPolicy
{
    public static ChatComposerKeyboardAction Resolve(bool isEnterKey, bool isShiftPressed, bool isAltPressed)
    {
        if (!isEnterKey)
        {
            return ChatComposerKeyboardAction.None;
        }

        return isShiftPressed || isAltPressed
            ? ChatComposerKeyboardAction.InsertNewLine
            : ChatComposerKeyboardAction.SendMessage;
    }
}

public enum ChatComposerKeyboardAction
{
    None,
    SendMessage,
    InsertNewLine,
}
