namespace DotPilot.Presentation.Controls;

public sealed partial class ChatConversationView : UserControl
{
    private long _itemsSourceCallbackToken;
    private bool _isItemsSourceCallbackRegistered;
    private bool _pendingAutoScroll = true;

    public ChatConversationView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isItemsSourceCallbackRegistered)
        {
            _itemsSourceCallbackToken = MessagesList.RegisterPropertyChangedCallback(
                ItemsControl.ItemsSourceProperty,
                OnMessagesSourceChanged);
            _isItemsSourceCallbackRegistered = true;
        }

        MessagesList.LayoutUpdated += OnMessagesLayoutUpdated;
        MessagesList.SizeChanged += OnMessagesSizeChanged;
        QueueAutoScroll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        MessagesList.LayoutUpdated -= OnMessagesLayoutUpdated;
        MessagesList.SizeChanged -= OnMessagesSizeChanged;
        if (!_isItemsSourceCallbackRegistered)
        {
            return;
        }

        MessagesList.UnregisterPropertyChangedCallback(
            ItemsControl.ItemsSourceProperty,
            _itemsSourceCallbackToken);
        _isItemsSourceCallbackRegistered = false;
    }

    private void OnMessagesSourceChanged(DependencyObject sender, DependencyProperty dependencyProperty)
    {
        QueueAutoScroll();
    }

    private void OnMessagesLayoutUpdated(object? sender, object e)
    {
        if (!_pendingAutoScroll)
        {
            return;
        }

        _pendingAutoScroll = false;
        ScrollToLatestMessage();
    }

    private void OnMessagesSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Height <= e.PreviousSize.Height)
        {
            return;
        }

        QueueAutoScroll();
    }

    private void QueueAutoScroll()
    {
        _pendingAutoScroll = true;
    }

    private void ScrollToLatestMessage()
    {
        if (!IsLoaded)
        {
            return;
        }

        _ = ConversationScrollViewer.ChangeView(
            horizontalOffset: null,
            verticalOffset: ConversationScrollViewer.ScrollableHeight,
            zoomFactor: null,
            disableAnimation: true);
    }
}

public sealed class ChatMessageTemplateSelector : DataTemplateSelector
{
    private const string MissingTemplateMessage = "Chat message templates must be configured.";
    private static readonly SessionStreamEntryKind[] _activityKinds =
    [
        SessionStreamEntryKind.ToolStarted,
        SessionStreamEntryKind.ToolCompleted,
        SessionStreamEntryKind.Status,
        SessionStreamEntryKind.Error,
    ];

    public DataTemplate? ActivityTemplate { get; set; }

    public DataTemplate? IncomingTemplate { get; set; }

    public DataTemplate? OutgoingTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        if (item is ChatTimelineItem activityItem &&
            _activityKinds.Contains(activityItem.Kind))
        {
            return ActivityTemplate ?? IncomingTemplate ?? OutgoingTemplate ?? throw new InvalidOperationException(MissingTemplateMessage);
        }

        return item is ChatTimelineItem { IsCurrentUser: true }
            ? OutgoingTemplate ?? IncomingTemplate ?? throw new InvalidOperationException(MissingTemplateMessage)
            : IncomingTemplate ?? OutgoingTemplate ?? throw new InvalidOperationException(MissingTemplateMessage);
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
