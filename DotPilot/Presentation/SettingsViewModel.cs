namespace DotPilot.Presentation;

public sealed class SettingsViewModel : ObservableObject
{
    private const string PageTitleValue = "Unified settings shell";
    private const string PageSubtitleValue =
        "Providers, policies, and storage stay visible from one operator-oriented surface.";
    private const string DefaultCategoryTitle = "Select a settings category";
    private const string DefaultCategorySummary = "Choose a category to inspect its current entries.";

    private WorkbenchSettingsCategoryItem? _selectedCategory;

    public SettingsViewModel(
        IWorkbenchCatalog workbenchCatalog,
        IRuntimeFoundationCatalog runtimeFoundationCatalog)
    {
        ArgumentNullException.ThrowIfNull(workbenchCatalog);
        ArgumentNullException.ThrowIfNull(runtimeFoundationCatalog);

        Snapshot = workbenchCatalog.GetSnapshot();
        RuntimeFoundation = runtimeFoundationCatalog.GetSnapshot();
        Categories = Snapshot.SettingsCategories
            .Select(category => new WorkbenchSettingsCategoryItem(
                category.Key,
                category.Title,
                category.Summary,
                PresentationAutomationIds.SettingsCategory(category.Key),
                category.Entries))
            .ToArray();
        _selectedCategory = Categories.Count > 0 ? Categories[0] : null;
        SettingsIssueLabel = WorkbenchIssues.FormatIssueLabel(WorkbenchIssues.SettingsShell);
    }

    public WorkbenchSnapshot Snapshot { get; }

    public RuntimeFoundationSnapshot RuntimeFoundation { get; }

    public string SettingsIssueLabel { get; }

    public string PageTitle => PageTitleValue;

    public string PageSubtitle => PageSubtitleValue;

    public IReadOnlyList<WorkbenchSettingsCategoryItem> Categories { get; }

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
        }
    }

    public string SelectedCategoryTitle => SelectedCategory?.Title ?? DefaultCategoryTitle;

    public string SelectedCategorySummary => SelectedCategory?.Summary ?? DefaultCategorySummary;

    public IReadOnlyList<WorkbenchSettingEntry> VisibleEntries => SelectedCategory?.Entries ?? [];

    public string ProviderSummary => $"{RuntimeFoundation.Providers.Count} provider checks available";
}
