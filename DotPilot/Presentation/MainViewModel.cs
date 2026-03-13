using System.Collections.Frozen;

namespace DotPilot.Presentation;

public sealed class MainViewModel : ObservableObject
{
    private const double IndentSize = 16d;
    private const string DefaultDocumentTitle = "Select a file";
    private const string DefaultDocumentPath = "Choose a repository item from the left sidebar.";
    private const string DefaultDocumentStatus = "The file surface becomes active after you open a file.";
    private const string DefaultInspectorArtifactsTitle = "Artifacts";
    private const string DefaultInspectorLogsTitle = "Runtime log console";
    private const string DefaultInspectorArtifactsSummary = "Generated files, plans, screenshots, and session outputs stay attached to the current workbench.";
    private const string DefaultInspectorLogsSummary = "Runtime logs remain visible without leaving the main workbench.";
    private const string DefaultLanguageLabel = "No document";
    private const string DefaultRendererLabel = "Select a repository item";
    private const string DefaultPreviewContent = "Open a file from the repository tree to inspect it here.";

    private readonly FrozenDictionary<string, WorkbenchDocumentDescriptor> _documentsByPath;
    private readonly IReadOnlyList<WorkbenchRepositoryNodeItem> _allRepositoryNodes;
    private IReadOnlyList<WorkbenchRepositoryNodeItem> _filteredRepositoryNodes;
    private string _repositorySearchText = string.Empty;
    private WorkbenchRepositoryNodeItem? _selectedRepositoryNode;
    private WorkbenchDocumentDescriptor? _selectedDocument;
    private string _editablePreviewContent = DefaultPreviewContent;
    private bool _isDiffReviewMode;
    private bool _isLogConsoleVisible;

    public MainViewModel(
        IWorkbenchCatalog workbenchCatalog,
        IRuntimeFoundationCatalog runtimeFoundationCatalog)
    {
        try
        {
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] MainViewModel constructor started.");
            ArgumentNullException.ThrowIfNull(workbenchCatalog);
            ArgumentNullException.ThrowIfNull(runtimeFoundationCatalog);

            Snapshot = workbenchCatalog.GetSnapshot();
            BrowserConsoleDiagnostics.Info(
                $"[DotPilot.Startup] MainViewModel workbench snapshot loaded. Nodes={Snapshot.RepositoryNodes.Count}, Documents={Snapshot.Documents.Count}.");
            RuntimeFoundation = runtimeFoundationCatalog.GetSnapshot();
            BrowserConsoleDiagnostics.Info(
                $"[DotPilot.Startup] MainViewModel runtime foundation snapshot loaded. Providers={RuntimeFoundation.Providers.Count}.");
            EpicLabel = WorkbenchIssues.FormatIssueLabel(WorkbenchIssues.DesktopWorkbenchEpic);
            _documentsByPath = Snapshot.Documents.ToFrozenDictionary(document => document.RelativePath, StringComparer.OrdinalIgnoreCase);
            _allRepositoryNodes = Snapshot.RepositoryNodes
                .Select(MapRepositoryNode)
                .ToArray();
            _filteredRepositoryNodes = _allRepositoryNodes;

            _selectedDocument = Snapshot.Documents.Count > 0 ? Snapshot.Documents[0] : null;
            _editablePreviewContent = _selectedDocument?.PreviewContent ?? DefaultPreviewContent;

            var initialNode = _selectedDocument is null
                ? FindFirstOpenableNode(_allRepositoryNodes)
                : FindNodeByRelativePath(_allRepositoryNodes, _selectedDocument.RelativePath);

            if (initialNode is not null)
            {
                SetSelectedRepositoryNode(initialNode);
            }

            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] MainViewModel constructor completed.");
        }
        catch (Exception exception)
        {
            BrowserConsoleDiagnostics.Error($"[DotPilot.Startup] MainViewModel constructor failed: {exception}");
            throw;
        }
    }

    public WorkbenchSnapshot Snapshot { get; }

    public RuntimeFoundationSnapshot RuntimeFoundation { get; }

    public string EpicLabel { get; }

    public string PageTitle => Snapshot.SessionTitle;

    public string WorkspaceName => Snapshot.WorkspaceName;

    public string WorkspaceRoot => Snapshot.WorkspaceRoot;

    public string SearchPlaceholder => Snapshot.SearchPlaceholder;

    public string SessionStage => Snapshot.SessionStage;

    public string SessionSummary => Snapshot.SessionSummary;

    public IReadOnlyList<WorkbenchSessionEntry> SessionEntries => Snapshot.SessionEntries;

    public IReadOnlyList<WorkbenchArtifactDescriptor> Artifacts => Snapshot.Artifacts;

    public IReadOnlyList<WorkbenchLogEntry> Logs => Snapshot.Logs;

    public IReadOnlyList<WorkbenchRepositoryNodeItem> FilteredRepositoryNodes
    {
        get => _filteredRepositoryNodes;
        private set
        {
            if (ReferenceEquals(_filteredRepositoryNodes, value))
            {
                return;
            }

            _filteredRepositoryNodes = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(RepositoryResultSummary));
        }
    }

    public string RepositoryResultSummary => $"{FilteredRepositoryNodes.Count} items";

    public string RepositorySearchText
    {
        get => _repositorySearchText;
        set
        {
            if (!SetProperty(ref _repositorySearchText, value))
            {
                return;
            }

            UpdateFilteredRepositoryNodes();
        }
    }

    public WorkbenchRepositoryNodeItem? SelectedRepositoryNode
    {
        get => _selectedRepositoryNode;
        set => SetSelectedRepositoryNode(value);
    }

    public string SelectedDocumentTitle => _selectedDocument?.Title ?? DefaultDocumentTitle;

    public string SelectedDocumentPath => _selectedDocument?.RelativePath ?? DefaultDocumentPath;

    public string SelectedDocumentStatus => _selectedDocument?.StatusSummary ?? DefaultDocumentStatus;

    public string SelectedDocumentLanguage => _selectedDocument?.LanguageLabel ?? DefaultLanguageLabel;

    public string SelectedDocumentRenderer => _selectedDocument?.RendererLabel ?? DefaultRendererLabel;

    public bool SelectedDocumentIsReadOnly => _selectedDocument?.IsReadOnly ?? true;

    public IReadOnlyList<WorkbenchDiffLine> SelectedDocumentDiffLines => _selectedDocument?.DiffLines ?? [];

    public string EditablePreviewContent
    {
        get => _editablePreviewContent;
        set => SetProperty(ref _editablePreviewContent, value);
    }

    public bool IsDiffReviewMode
    {
        get => _isDiffReviewMode;
        set
        {
            if (!SetProperty(ref _isDiffReviewMode, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsPreviewMode));
        }
    }

    public bool IsPreviewMode => !IsDiffReviewMode;

    public bool IsLogConsoleVisible
    {
        get => _isLogConsoleVisible;
        set
        {
            if (!SetProperty(ref _isLogConsoleVisible, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsArtifactsVisible));
            RaisePropertyChanged(nameof(InspectorTitle));
            RaisePropertyChanged(nameof(InspectorSummary));
        }
    }

    public bool IsArtifactsVisible => !IsLogConsoleVisible;

    public string InspectorTitle => IsLogConsoleVisible ? DefaultInspectorLogsTitle : DefaultInspectorArtifactsTitle;

    public string InspectorSummary => IsLogConsoleVisible ? DefaultInspectorLogsSummary : DefaultInspectorArtifactsSummary;

    private static WorkbenchRepositoryNodeItem MapRepositoryNode(WorkbenchRepositoryNode node)
    {
        var kindGlyph = node.IsDirectory ? "▾" : "•";
        var indentMargin = new Thickness(node.Depth * IndentSize, 0d, 0d, 0d);
        var automationId = PresentationAutomationIds.RepositoryNode(node.RelativePath);
        var tapAutomationId = PresentationAutomationIds.RepositoryNodeTap(node.RelativePath);

        return new(
            node.RelativePath,
            node.Name,
            node.DisplayLabel,
            node.IsDirectory,
            node.CanOpen,
            kindGlyph,
            indentMargin,
            automationId,
            tapAutomationId);
    }

    private void UpdateFilteredRepositoryNodes()
    {
        var searchTerms = RepositorySearchText.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        FilteredRepositoryNodes = searchTerms.Length is 0
            ? _allRepositoryNodes
            : _allRepositoryNodes
                .Where(node => searchTerms.All(term =>
                    node.DisplayLabel.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    node.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        if (_selectedRepositoryNode is null ||
            !FilteredRepositoryNodes.Contains(_selectedRepositoryNode))
        {
            SetSelectedRepositoryNode(FindFirstOpenableNode(FilteredRepositoryNodes));
        }
    }

    private static WorkbenchRepositoryNodeItem? FindFirstOpenableNode(IReadOnlyList<WorkbenchRepositoryNodeItem> nodes)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (nodes[index].CanOpen)
            {
                return nodes[index];
            }
        }

        return nodes.Count > 0 ? nodes[0] : null;
    }

    private static WorkbenchRepositoryNodeItem? FindNodeByRelativePath(
        IReadOnlyList<WorkbenchRepositoryNodeItem> nodes,
        string relativePath)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (nodes[index].RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
            {
                return nodes[index];
            }
        }

        return null;
    }

    private void SetSelectedRepositoryNode(WorkbenchRepositoryNodeItem? value)
    {
        if (!SetProperty(ref _selectedRepositoryNode, value, nameof(SelectedRepositoryNode)))
        {
            return;
        }

        if (value?.CanOpen != true ||
            !_documentsByPath.TryGetValue(value.RelativePath, out var selectedDocument))
        {
            return;
        }

        _selectedDocument = selectedDocument;
        EditablePreviewContent = selectedDocument.PreviewContent;
        RaisePropertyChanged(nameof(SelectedDocumentTitle));
        RaisePropertyChanged(nameof(SelectedDocumentPath));
        RaisePropertyChanged(nameof(SelectedDocumentStatus));
        RaisePropertyChanged(nameof(SelectedDocumentLanguage));
        RaisePropertyChanged(nameof(SelectedDocumentRenderer));
        RaisePropertyChanged(nameof(SelectedDocumentIsReadOnly));
        RaisePropertyChanged(nameof(SelectedDocumentDiffLines));
    }
}
