using DotPilot.Core.Features.ToolchainCenter;

namespace DotPilot.Presentation;

public sealed class SettingsViewModel : ObservableObject
{
    private const string PageTitleValue = "Unified settings shell";
    private const string PageSubtitleValue =
        "Toolchains, provider readiness, policies, and storage stay visible from one operator-oriented surface.";
    private const string DefaultCategoryTitle = "Select a settings category";
    private const string DefaultCategorySummary = "Choose a category to inspect its current entries.";
    private const string ToolchainProviderSummaryFormat = "{0} ready • {1} need attention";
    private static readonly System.Text.CompositeFormat ToolchainProviderSummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(ToolchainProviderSummaryFormat);

    private WorkbenchSettingsCategoryItem? _selectedCategory;
    private ToolchainProviderItem? _selectedToolchainProvider;

    public SettingsViewModel(
        IWorkbenchCatalog workbenchCatalog,
        IRuntimeFoundationCatalog runtimeFoundationCatalog,
        IToolchainCenterCatalog toolchainCenterCatalog)
    {
        ArgumentNullException.ThrowIfNull(workbenchCatalog);
        ArgumentNullException.ThrowIfNull(runtimeFoundationCatalog);
        ArgumentNullException.ThrowIfNull(toolchainCenterCatalog);

        Snapshot = workbenchCatalog.GetSnapshot();
        RuntimeFoundation = runtimeFoundationCatalog.GetSnapshot();
        ToolchainCenter = toolchainCenterCatalog.GetSnapshot();
        Categories = Snapshot.SettingsCategories
            .Select(category => new WorkbenchSettingsCategoryItem(
                category.Key,
                category.Title,
                category.Summary,
                PresentationAutomationIds.SettingsCategory(category.Key),
                category.Entries))
            .ToArray();
        ToolchainProviders = ToolchainCenter.Providers
            .Select(provider => new ToolchainProviderItem(
                provider,
                PresentationAutomationIds.ToolchainProvider(provider.Provider.CommandName)))
            .ToArray();
        ToolchainWorkstreams = ToolchainCenter.Workstreams
            .Select(workstream => new ToolchainWorkstreamItem(
                workstream,
                PresentationAutomationIds.ToolchainWorkstream(workstream.IssueNumber)))
            .ToArray();
        _selectedCategory = Categories.FirstOrDefault(category => category.Key == WorkbenchSettingsCategoryKeys.Toolchains) ??
            (Categories.Count > 0 ? Categories[0] : null);
        _selectedToolchainProvider = ToolchainProviders.Count > 0 ? ToolchainProviders[0] : null;
        SettingsIssueLabel = WorkbenchIssues.FormatIssueLabel(WorkbenchIssues.SettingsShell);
    }

    public WorkbenchSnapshot Snapshot { get; }

    public RuntimeFoundationSnapshot RuntimeFoundation { get; }

    public ToolchainCenterSnapshot ToolchainCenter { get; }

    public string SettingsIssueLabel { get; }

    public string PageTitle => PageTitleValue;

    public string PageSubtitle => PageSubtitleValue;

    public IReadOnlyList<WorkbenchSettingsCategoryItem> Categories { get; }

    public IReadOnlyList<ToolchainProviderItem> ToolchainProviders { get; }

    public IReadOnlyList<ToolchainWorkstreamItem> ToolchainWorkstreams { get; }

    public WorkbenchSettingsCategoryItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(SelectedCategoryTitle));
            RaisePropertyChanged(nameof(SelectedCategorySummary));
            RaisePropertyChanged(nameof(VisibleEntries));
            RaisePropertyChanged(nameof(IsToolchainCenterVisible));
            RaisePropertyChanged(nameof(AreGenericSettingsVisible));
        }
    }

    public ToolchainProviderItem? SelectedToolchainProvider
    {
        get => _selectedToolchainProvider;
        set
        {
            if (!SetProperty(ref _selectedToolchainProvider, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(SelectedToolchainProviderSnapshot));
        }
    }

    public string SelectedCategoryTitle => SelectedCategory?.Title ?? DefaultCategoryTitle;

    public string SelectedCategorySummary => SelectedCategory?.Summary ?? DefaultCategorySummary;

    public bool IsToolchainCenterVisible => SelectedCategory?.Key == WorkbenchSettingsCategoryKeys.Toolchains;

    public bool AreGenericSettingsVisible => !IsToolchainCenterVisible;

    public IReadOnlyList<WorkbenchSettingEntry> VisibleEntries => SelectedCategory?.Entries ?? [];

    public ToolchainProviderSnapshot? SelectedToolchainProviderSnapshot => SelectedToolchainProvider?.Snapshot;

    public string ProviderSummary => string.Format(
        System.Globalization.CultureInfo.InvariantCulture,
        ToolchainProviderSummaryCompositeFormat,
        ToolchainCenter.ReadyProviderCount,
        ToolchainCenter.AttentionRequiredProviderCount);
}
