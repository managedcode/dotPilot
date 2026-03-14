namespace DotPilot.Presentation.Controls;

public sealed partial class ChatConversationView : UserControl
{
    public ChatConversationView()
    {
        InitializeComponent();
    }
}

public sealed class ChatMessageTemplateSelector : DataTemplateSelector
{
    private const string MissingTemplateMessage = "Chat message templates must be configured.";

    public DataTemplate? IncomingTemplate { get; set; }

    public DataTemplate? OutgoingTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item is ChatTimelineItem { IsCurrentUser: true }
            ? OutgoingTemplate ?? IncomingTemplate ?? throw new InvalidOperationException(MissingTemplateMessage)
            : IncomingTemplate ?? OutgoingTemplate ?? throw new InvalidOperationException(MissingTemplateMessage);
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
