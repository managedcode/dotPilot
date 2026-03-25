using DotPilot.Core.ChatSessions;

namespace DotPilot.Core.Providers;

internal static class LocalModelProviderConfigurationReader
{
    public static async ValueTask<LocalModelProviderConfiguration> ReadAsync(
        AgentProviderKind providerKind,
        IReadOnlyList<ProviderLocalModelRecord> configuredModels,
        string? preferredModelPath,
        CancellationToken cancellationToken)
    {
        if (!providerKind.IsLocalModelProvider())
        {
            throw new ArgumentOutOfRangeException(nameof(providerKind), providerKind, null);
        }

        var environmentVariableNames = providerKind.GetModelPathEnvironmentVariableNames();
        var candidatePaths = EnumerateCandidatePaths(configuredModels, preferredModelPath, environmentVariableNames);
        var evaluatedModels = await EvaluateModelsAsync(providerKind, candidatePaths, cancellationToken).ConfigureAwait(false);
        var compatibleModels = BuildCompatibleModels(evaluatedModels);
        var supportedModelNames = compatibleModels
            .Select(static model => model.ModelName)
            .ToArray();
        var configuredModelPaths = ResolveConfiguredModelPaths(evaluatedModels, compatibleModels);
        var detectedRuntimeTypes = ResolveDetectedRuntimeTypes(evaluatedModels, compatibleModels);
        var firstValidationFailure = compatibleModels.Count > 0
            ? null
            : evaluatedModels.FirstOrDefault(static candidate =>
                !candidate.Compatibility.IsCompatible &&
                !string.IsNullOrWhiteSpace(candidate.Compatibility.FailureMessage));

        return new LocalModelProviderConfiguration(
            providerKind.GetPrimaryModelPathEnvironmentVariableName(),
            environmentVariableNames,
            providerKind.GetModelPathSetupCommand(),
            compatibleModels,
            configuredModelPaths,
            compatibleModels.Count > 0,
            compatibleModels.FirstOrDefault()?.ModelName,
            supportedModelNames,
            firstValidationFailure?.Compatibility.FailureCode,
            firstValidationFailure?.Compatibility.FailureMessage,
            detectedRuntimeTypes,
            LocalModelProviderCompatibilityCatalog.GetSupportedRuntimeTypes(providerKind));
    }

    private static List<LocalModelProviderEntry> BuildCompatibleModels(IReadOnlyList<EvaluatedLocalModelCandidate> evaluatedModels)
    {
        var compatibleCandidates = evaluatedModels
            .Where(static candidate =>
                candidate.Compatibility.IsCompatible &&
                !string.IsNullOrWhiteSpace(candidate.Compatibility.NormalizedModelPath) &&
                !string.IsNullOrWhiteSpace(candidate.Compatibility.SuggestedModelName))
            .ToArray();
        if (compatibleCandidates.Length == 0)
        {
            return [];
        }

        var nameGroups = compatibleCandidates
            .GroupBy(candidate => candidate.Compatibility.SuggestedModelName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
        List<LocalModelProviderEntry> compatibleModels = [];
        foreach (var candidate in compatibleCandidates)
        {
            var baseModelName = candidate.Compatibility.SuggestedModelName!;
            var modelPath = candidate.Compatibility.NormalizedModelPath!;
            var modelName = ResolveUniqueModelName(baseModelName, modelPath, nameGroups, seenNames);
            compatibleModels.Add(new LocalModelProviderEntry(
                modelName,
                baseModelName,
                modelPath,
                candidate.AddedAt,
                candidate.Compatibility.DetectedRuntimeType));
        }

        return compatibleModels;
    }

    private static string ResolveUniqueModelName(
        string baseModelName,
        string modelPath,
        Dictionary<string, int> nameGroups,
        HashSet<string> seenNames)
    {
        var modelName = baseModelName;
        if (nameGroups.TryGetValue(baseModelName, out var count) && count > 1)
        {
            var parentDirectoryName = ResolveParentDirectoryName(modelPath);
            if (!string.IsNullOrWhiteSpace(parentDirectoryName))
            {
                modelName = $"{baseModelName} ({parentDirectoryName})";
            }

            if (seenNames.Contains(modelName))
            {
                modelName = $"{baseModelName} ({modelPath})";
            }
        }

        if (seenNames.Add(modelName))
        {
            return modelName;
        }

        var suffix = 2;
        while (!seenNames.Add($"{modelName} #{suffix}"))
        {
            suffix++;
        }

        return $"{modelName} #{suffix}";
    }

    private static string ResolveParentDirectoryName(string modelPath)
    {
        var normalizedPath = modelPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directoryPath = Path.GetDirectoryName(normalizedPath);
        return string.IsNullOrWhiteSpace(directoryPath)
            ? string.Empty
            : Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static string[] ResolveConfiguredModelPaths(
        IReadOnlyList<EvaluatedLocalModelCandidate> evaluatedModels,
        List<LocalModelProviderEntry> compatibleModels)
    {
        if (compatibleModels.Count > 0)
        {
            return compatibleModels
                .Select(static model => model.ModelPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return evaluatedModels
            .Select(candidate => candidate.Compatibility.NormalizedModelPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ResolveDetectedRuntimeTypes(
        IReadOnlyList<EvaluatedLocalModelCandidate> evaluatedModels,
        List<LocalModelProviderEntry> compatibleModels)
    {
        var detectedRuntimeTypes = compatibleModels.Count > 0
            ? compatibleModels.Select(static model => model.DetectedRuntimeType)
            : evaluatedModels.Select(static candidate => candidate.Compatibility.DetectedRuntimeType);
        return detectedRuntimeTypes
            .Where(static runtimeType => !string.IsNullOrWhiteSpace(runtimeType))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async ValueTask<IReadOnlyList<EvaluatedLocalModelCandidate>> EvaluateModelsAsync(
        AgentProviderKind providerKind,
        IReadOnlyList<LocalModelPathCandidate> candidatePaths,
        CancellationToken cancellationToken)
    {
        HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);
        List<EvaluatedLocalModelCandidate> evaluatedModels = [];
        foreach (var candidate in candidatePaths)
        {
            var compatibility = await LocalModelProviderCompatibilityReader.ReadAsync(
                providerKind,
                candidate.ModelPath,
                cancellationToken).ConfigureAwait(false);
            var normalizedPath = compatibility.NormalizedModelPath;
            if (string.IsNullOrWhiteSpace(normalizedPath) || !seenPaths.Add(normalizedPath))
            {
                continue;
            }

            evaluatedModels.Add(new EvaluatedLocalModelCandidate(candidate.AddedAt, compatibility));
        }

        return evaluatedModels;
    }

    private static List<LocalModelPathCandidate> EnumerateCandidatePaths(
        IReadOnlyList<ProviderLocalModelRecord> configuredModels,
        string? preferredModelPath,
        IReadOnlyList<string> environmentVariableNames)
    {
        List<LocalModelPathCandidate> candidates = [];
        if (!string.IsNullOrWhiteSpace(preferredModelPath))
        {
            candidates.Add(new LocalModelPathCandidate(preferredModelPath.Trim(), DateTimeOffset.MaxValue));
        }

        candidates.AddRange(
            configuredModels
                .Where(static record => !string.IsNullOrWhiteSpace(record.ModelPath))
                .OrderByDescending(static record => record.AddedAt)
                .ThenBy(static record => record.ModelPath, StringComparer.OrdinalIgnoreCase)
                .Select(static record => new LocalModelPathCandidate(record.ModelPath.Trim(), record.AddedAt)));

        for (var index = 0; index < environmentVariableNames.Count; index++)
        {
            var environmentValue = Environment.GetEnvironmentVariable(environmentVariableNames[index]);
            if (string.IsNullOrWhiteSpace(environmentValue))
            {
                continue;
            }

            candidates.Add(new LocalModelPathCandidate(
                environmentValue.Trim(),
                DateTimeOffset.MinValue.AddTicks(index)));
        }

        return candidates;
    }

    private sealed record LocalModelPathCandidate(string ModelPath, DateTimeOffset AddedAt);

    private sealed record EvaluatedLocalModelCandidate(
        DateTimeOffset AddedAt,
        LocalModelCompatibilityInfo Compatibility);
}
