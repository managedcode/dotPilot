namespace DotPilot.Presentation.Controls;

public sealed partial class WorkbenchSidebar : UserControl
{
    public WorkbenchSidebar()
    {
        InitializeComponent();
    }

    private void OnRepositoryNodeTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            sender is not FrameworkElement element ||
            element.DataContext is not WorkbenchRepositoryNodeItem repositoryNode)
        {
            return;
        }

        viewModel.SelectedRepositoryNode = repositoryNode;
    }
}
