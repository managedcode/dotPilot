using System.Collections.Frozen;
using DotPilot.Core.Features.RuntimeFoundation;
using DotPilot.Core.Features.Workbench;

namespace DotPilot.Runtime.Features.Workbench;

internal sealed class WorkbenchWorkspaceSnapshotBuilder
{
    private const int MaxDocumentCount = 12;
    private const int MaxNodeCount = 96;
    private const int MaxPreviewLines = 18;
    private const int MaxTraversalDepth = 4;
    private const string SearchPlaceholder = "Search the workspace tree";
    private const string SessionStage = "Execute";
    private const string MonacoRendererLabel = "Monaco-aligned preview";
    private const string StructuredRendererLabel = "Structured preview";
    private const string ReadOnlyStatusSummary = "Read-only workspace reference";
    private const string DiffReviewNote = "issue #13 runtime-backed review";
    private const string ToolchainCategoryTitle = "Toolchain Center";
    private const string ToolchainCategorySummary = "Install, connect, diagnose, and poll Codex, Claude Code, and GitHub Copilot.";
    private const string ProvidersCategoryTitle = "Providers";
    private const string PoliciesCategoryTitle = "Policies";
    private const string StorageCategoryTitle = "Storage";
    private const string ProvidersCategorySummary = "Provider readiness stays visible from the unified settings shell.";
    private const string PoliciesCategorySummary = "Review and approval defaults for operator sessions.";
    private const string StorageCategorySummary = "Workspace root and artifact handling.";
    private const string ApprovalModeEntryName = "Approval mode";
    private const string ApprovalModeEntryValue = "Operator confirmation";
    private const string ReviewGateEntryName = "Diff review gate";
    private const string ReviewGateEntryValue = "Required";
    private const string WorkspaceRootEntryName = "Workspace root";
    private const string ArtifactRetentionEntryName = "Artifact retention";
    private const string ArtifactRetentionEntryValue = "14 days";
    private const string WorkbenchDocPath = "docs/Features/workbench-foundation.md";
    private const string ArchitecturePath = "docs/Architecture.md";
    private const string PlanPath = "issue-13-workbench-foundation.plan.md";
    private const string ConsolePath = "artifacts/session-output.log";
    private const string ScreenshotPath = "artifacts/workbench-shell.png";
    private const string TimestampOne = "09:10";
    private const string TimestampTwo = "09:13";
    private const string TimestampThree = "09:15";
    private const string TimestampFour = "09:17";
    private const string InfoLevel = "INFO";
    private const string ReviewLevel = "REVIEW";
    private const string RuntimeSource = "runtime";
    private const string AgentSource = "agent";
    private const string SettingsSource = "settings";
    private const string SettingsMessage = "Published unified settings categories for providers, policies, and storage.";
    private const string SessionEntryPlanTitle = "Plan baseline";
    private const string SessionEntryIndexTitle = "Workspace indexed";
    private const string SessionEntryReviewTitle = "Review ready";
    private const string SessionEntrySettingsTitle = "Settings published";
    private const string SessionEntryPlanSummary = "Preserved the issue #13 workbench plan before implementation.";
    private const string SessionEntrySettingsSummary = "Surfaced providers, policies, and storage as first-class settings categories.";

    private static readonly FrozenSet<string> SupportedDocumentExtensions = new[]
    {
        ".cs",
        ".csproj",
        ".json",
        ".md",
        ".props",
        ".slnx",
        ".targets",
        ".xaml",
        ".xml",
        ".yml",
        ".yaml",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> MonacoPreviewExtensions = new[]
    {
        ".cs",
        ".json",
        ".md",
        ".xaml",
        ".xml",
        ".yml",
        ".yaml",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly ResolvedWorkspace _workspace;
    private readonly RuntimeFoundationSnapshot _runtimeFoundationSnapshot;
    private readonly GitIgnoreRuleSet _ignoreRules;

    public WorkbenchWorkspaceSnapshotBuilder(
        ResolvedWorkspace workspace,
        RuntimeFoundationSnapshot runtimeFoundationSnapshot)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(runtimeFoundationSnapshot);

        _workspace = workspace;
        _runtimeFoundationSnapshot = runtimeFoundationSnapshot;
        _ignoreRules = GitIgnoreRuleSet.Load(workspace.Root);
    }

    public WorkbenchSnapshot Build()
    {
        var repositoryNodes = BuildRepositoryNodes();
        var documents = BuildDocuments(repositoryNodes);
        if (repositoryNodes.Count == 0 || documents.Count == 0)
        {
            return WorkbenchSeedData.Create(_runtimeFoundationSnapshot);
        }

        return new(
            _workspace.Name,
            _workspace.Root,
            SearchPlaceholder,
            $"{_workspace.Name} operator workbench",
            SessionStage,
            $"Indexed {repositoryNodes.Count} workspace nodes and prepared {documents.Count} reviewable documents.",
            CreateSessionEntries(documents[0].Title),
            repositoryNodes,
            documents,
            CreateArtifacts(documents),
            CreateLogs(documents.Count, documents[0].Title),
            CreateSettingsCategories());
    }

    private List<WorkbenchRepositoryNode> BuildRepositoryNodes()
    {
        List<WorkbenchRepositoryNode> nodes = [];
        TraverseDirectory(_workspace.Root, relativePath: string.Empty, depth: 0, nodes);
        return nodes;
    }

    private void TraverseDirectory(string absoluteDirectory, string relativePath, int depth, List<WorkbenchRepositoryNode> nodes)
    {
        if (depth > MaxTraversalDepth || nodes.Count >= MaxNodeCount)
        {
            return;
        }

        foreach (var directoryPath in EnumerateEntries(absoluteDirectory, searchDirectories: true))
        {
            if (nodes.Count >= MaxNodeCount)
            {
                return;
            }

            var directoryName = Path.GetFileName(directoryPath);
            var directoryRelativePath = CombineRelative(relativePath, directoryName);
            if (_ignoreRules.IsIgnored(directoryRelativePath, isDirectory: true))
            {
                continue;
            }

            nodes.Add(new(directoryRelativePath, directoryRelativePath, directoryName, depth, IsDirectory: true, CanOpen: false));
            TraverseDirectory(directoryPath, directoryRelativePath, depth + 1, nodes);
        }

        foreach (var filePath in EnumerateEntries(absoluteDirectory, searchDirectories: false))
        {
            if (nodes.Count >= MaxNodeCount)
            {
                return;
            }

            var fileName = Path.GetFileName(filePath);
            var fileRelativePath = CombineRelative(relativePath, fileName);
            if (_ignoreRules.IsIgnored(fileRelativePath, isDirectory: false) ||
                !SupportedDocumentExtensions.Contains(Path.GetExtension(filePath)))
            {
                continue;
            }

            nodes.Add(new(fileRelativePath, fileRelativePath, fileName, depth, IsDirectory: false, CanOpen: true));
        }
    }

    private List<WorkbenchDocumentDescriptor> BuildDocuments(IReadOnlyList<WorkbenchRepositoryNode> repositoryNodes)
    {
        List<WorkbenchDocumentDescriptor> documents = [];
        foreach (var node in repositoryNodes.Where(static node => node.CanOpen).Take(MaxDocumentCount))
        {
            var absolutePath = Path.Combine(_workspace.Root, node.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var previewContent = ReadPreview(absolutePath);
            if (string.IsNullOrWhiteSpace(previewContent))
            {
                continue;
            }

            var extension = Path.GetExtension(absolutePath);
            documents.Add(new(
                node.RelativePath,
                node.Name,
                ResolveLanguageLabel(extension),
                ResolveRendererLabel(extension),
                ReadOnlyStatusSummary,
                IsReadOnly: true,
                previewContent,
                CreateDiffLines(node.Name, node.RelativePath)));
        }

        return documents;
    }

    private IReadOnlyList<WorkbenchArtifactDescriptor> CreateArtifacts(List<WorkbenchDocumentDescriptor> documents)
    {
        var primaryDocument = documents[0];
        return
        [
            new("Workbench feature doc", "Documentation", File.Exists(Path.Combine(_workspace.Root, WorkbenchDocPath)) ? "Ready" : "Pending", WorkbenchDocPath, "Tracks epic #13 scope and workbench flow."),
            new("Architecture overview", "Documentation", File.Exists(Path.Combine(_workspace.Root, ArchitecturePath)) ? "Ready" : "Pending", ArchitecturePath, "Routes agents through the active solution boundaries."),
            new("Issue #13 plan", "Plan", File.Exists(Path.Combine(_workspace.Root, PlanPath)) ? "Ready" : "Pending", PlanPath, "Captures ordered implementation and validation work."),
            new(primaryDocument.Title, "Review target", "Open", primaryDocument.RelativePath, "The current file surface mirrors the selected workspace document."),
            new("Session output", "Console", "Streaming", ConsolePath, "The runtime log console stays bound to the current workbench session."),
            new("Workbench shell proof", "Screenshot", "Queued", ScreenshotPath, "Reserved for browser UI test screenshot attachments."),
        ];
    }

    private IReadOnlyList<WorkbenchLogEntry> CreateLogs(int documentCount, string primaryDocumentTitle)
    {
        return
        [
            new(TimestampOne, InfoLevel, RuntimeSource, $"Indexed the workspace rooted at {_workspace.Root}."),
            new(TimestampTwo, InfoLevel, RuntimeSource, $"Prepared Monaco-aligned previews for {documentCount} documents."),
            new(TimestampThree, ReviewLevel, AgentSource, $"Queued an explicit diff review for {primaryDocumentTitle}."),
            new(TimestampFour, InfoLevel, SettingsSource, SettingsMessage),
        ];
    }

    private IReadOnlyList<WorkbenchSessionEntry> CreateSessionEntries(string primaryDocumentTitle)
    {
        return
        [
            new(SessionEntryPlanTitle, TimestampOne, SessionEntryPlanSummary, WorkbenchSessionEntryKind.Operator),
            new(SessionEntryIndexTitle, TimestampTwo, $"Indexed the live workspace rooted at {_workspace.Root}.", WorkbenchSessionEntryKind.System),
            new(SessionEntryReviewTitle, TimestampThree, $"Prepared a diff review and preview surface for {primaryDocumentTitle}.", WorkbenchSessionEntryKind.Agent),
            new(SessionEntrySettingsTitle, TimestampFour, SessionEntrySettingsSummary, WorkbenchSessionEntryKind.System),
        ];
    }

    private IReadOnlyList<WorkbenchSettingsCategory> CreateSettingsCategories()
    {
        return
        [
            new(
                WorkbenchSettingsCategoryKeys.Toolchains,
                ToolchainCategoryTitle,
                ToolchainCategorySummary,
                []),
            new(
                WorkbenchSettingsCategoryKeys.Providers,
                ProvidersCategoryTitle,
                ProvidersCategorySummary,
                _runtimeFoundationSnapshot.Providers
                    .Select(provider => new WorkbenchSettingEntry(
                        provider.DisplayName,
                        provider.Status.ToString(),
                        provider.StatusSummary,
                        IsSensitive: false,
                        IsActionable: provider.RequiresExternalToolchain))
                    .ToArray()),
            new(
                WorkbenchSettingsCategoryKeys.Policies,
                PoliciesCategoryTitle,
                PoliciesCategorySummary,
                [
                    new(ApprovalModeEntryName, ApprovalModeEntryValue, "All file and tool changes stay operator-approved.", IsSensitive: false, IsActionable: true),
                    new(ReviewGateEntryName, ReviewGateEntryValue, "Agent proposals remain reviewable before acceptance.", IsSensitive: false, IsActionable: true),
                ]),
            new(
                WorkbenchSettingsCategoryKeys.Storage,
                StorageCategoryTitle,
                StorageCategorySummary,
                [
                    new(WorkspaceRootEntryName, _workspace.Root, "The workbench binds to the live workspace when available.", IsSensitive: false, IsActionable: false),
                    new(ArtifactRetentionEntryName, ArtifactRetentionEntryValue, "Artifacts remain visible from the dock and console.", IsSensitive: false, IsActionable: true),
                ]),
        ];
    }

    private static string[] EnumerateEntries(string absoluteDirectory, bool searchDirectories)
    {
        try
        {
            var entries = searchDirectories
                ? Directory.EnumerateDirectories(absoluteDirectory)
                : Directory.EnumerateFiles(absoluteDirectory);

            return entries.OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string ReadPreview(string absolutePath)
    {
        try
        {
            return string.Join(
                Environment.NewLine,
                File.ReadLines(absolutePath)
                    .Take(MaxPreviewLines));
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<WorkbenchDiffLine> CreateDiffLines(string title, string relativePath)
    {
        return
        [
            new(WorkbenchDiffLineKind.Context, $"@@ {relativePath} @@"),
            new(WorkbenchDiffLineKind.Removed, $"- prototype-only state for {title}"),
            new(WorkbenchDiffLineKind.Added, $"+ runtime-backed workbench state for {DiffReviewNote}"),
        ];
    }

    private static string ResolveLanguageLabel(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "C#",
            ".csproj" => "MSBuild",
            ".json" => "JSON",
            ".md" => "Markdown",
            ".props" or ".targets" or ".xml" => "XML",
            ".slnx" => "Solution",
            ".xaml" => "XAML",
            ".yml" or ".yaml" => "YAML",
            _ => "Text",
        };
    }

    private static string ResolveRendererLabel(string extension)
    {
        return MonacoPreviewExtensions.Contains(extension)
            ? MonacoRendererLabel
            : StructuredRendererLabel;
    }

    private static string CombineRelative(string relativePath, string name)
    {
        return string.IsNullOrEmpty(relativePath) ? name : string.Concat(relativePath, "/", name);
    }
}
