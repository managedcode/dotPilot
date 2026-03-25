using System.Diagnostics;
using DotPilot.Core.ChatSessions;
using DotPilot.Core.Providers.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Providers;

[NonParallelizable]
public sealed class AgentProviderStatusReaderTests
{
    [Test]
    public async Task RefreshWorkspaceAsyncReadsProviderStatusFromCurrentSourceOfTruth()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5", "gpt-5-mini");

        await using var fixture = CreateFixture();
        _ = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        var initialWorkspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();

        initialWorkspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");

        commandScope.WriteVersionCommand("codex", "codex version 2.0.0");

        var workspace = (await fixture.WorkspaceState.RefreshWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("2.0.0");
    }

    [Test]
    public async Task ReadAsyncReusesTheCachedSnapshotUntilItIsInvalidated()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 0);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");

        await using var fixture = CreateFixture();
        var reader = fixture.Provider.GetRequiredService<IAgentProviderStatusReader>();

        var initialSnapshot = await reader.ReadAsync(CancellationToken.None);
        initialSnapshot
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");
        commandScope.ReadInvocationCount("codex").Should().Be(1);

        commandScope.WriteCountingVersionCommand("codex", "codex version 2.0.0", delayMilliseconds: 0);
        commandScope.WriteCodexMetadata("gpt-5.1", "gpt-5.1");

        var cachedSnapshot = await reader.ReadAsync(CancellationToken.None);
        cachedSnapshot
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");
        commandScope.ReadInvocationCount("codex").Should().Be(1);

        reader.Invalidate();

        var refreshedSnapshot = await reader.ReadAsync(CancellationToken.None);
        refreshedSnapshot
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("2.0.0");
    }

    [Test]
    public async Task InvalidateDuringAnActiveProbeForcesTheNextReadToStartANewProbe()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 300);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");

        await using var fixture = CreateFixture();
        var reader = fixture.Provider.GetRequiredService<IAgentProviderStatusReader>();

        var initialReadTask = reader.ReadAsync(CancellationToken.None).AsTask();
        await Task.Delay(75);

        reader.Invalidate();
        commandScope.WriteCountingVersionCommand("codex", "codex version 2.0.0", delayMilliseconds: 0);
        commandScope.WriteCodexMetadata("gpt-5.1", "gpt-5.1");

        var refreshedSnapshot = await reader.ReadAsync(CancellationToken.None);
        refreshedSnapshot
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("2.0.0");

        var initialSnapshot = await initialReadTask;
        initialSnapshot.Should().NotBeEmpty();
    }

    [Test]
    public async Task EnabledCodexProviderReportsReadyRuntimeAndCliMetadata()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5", "gpt-5-mini");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeTrue();
        provider.Status.Should().Be(AgentProviderStatus.Ready);
        provider.StatusSummary.Should().Contain("ready for local desktop execution");
        provider.SuggestedModelName.Should().Be("gpt-5.4");
        provider.SupportedModelNames.Should().Contain("gpt-5-mini");
        provider.InstalledVersion.Should().Be("1.0.0");
        provider.Details.Should().Contain(detail => detail.Label == "Suggested model" && detail.Value == "gpt-5.4");
        provider.Details.Should().Contain(detail => detail.Label == "Supported models" && detail.Value.Contains("gpt-5-mini", StringComparison.Ordinal));
    }

    [Test]
    public async Task EnabledCopilotProviderReportsReadyRuntimeAndSuggestedModels()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteVersionCommand("copilot", "copilot version 0.0.421");
        commandScope.WriteCopilotConfig("claude-opus-4.6");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.GitHubCopilot, true),
            CancellationToken.None)).ShouldSucceed();

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeTrue();
        provider.Status.Should().Be(AgentProviderStatus.Ready);
        provider.StatusSummary.Should().Contain("ready for local desktop execution");
        provider.InstalledVersion.Should().Be("0.0.421");
        provider.SuggestedModelName.Should().Be("claude-opus-4.6");
        provider.SupportedModelNames.Should().ContainSingle().Which.Should().Be("claude-opus-4.6");
        provider.Details.Should().Contain(detail => detail.Label == "Suggested model" && detail.Value == "claude-opus-4.6");
        provider.Details.Should().Contain(detail => detail.Label == "Supported models" && detail.Value == "claude-opus-4.6");
    }

    [Test]
    public async Task EnabledClaudeProviderReportsReadyRuntimeAndSuggestedModels()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteVersionCommand("claude", "claude version 2.0.75");
        commandScope.WriteClaudeSettings("claude-opus-4-6");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.ClaudeCode, true),
            CancellationToken.None)).ShouldSucceed();

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeTrue();
        provider.Status.Should().Be(AgentProviderStatus.Ready);
        provider.StatusSummary.Should().Contain("ready for local desktop execution");
        provider.InstalledVersion.Should().Be("2.0.75");
        provider.SuggestedModelName.Should().Be("claude-opus-4-6");
        provider.SupportedModelNames.Should().Contain("claude-opus-4-6");
        provider.Details.Should().Contain(detail => detail.Label == "Suggested model" && detail.Value == "claude-opus-4-6");
        provider.Details.Should().Contain(detail =>
            detail.Label == "Supported models" &&
            detail.Value.Contains("claude-opus-4-6", StringComparison.Ordinal));
    }

    [Test]
    public async Task EnabledGeminiProviderReportsReadyRuntimeAndSuggestedModels()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteVersionCommand("gemini", "gemini-cli 0.34.0");
        commandScope.WriteGeminiMetadata("gemini-2.5-pro", "gemini-2.5-pro", "gemini-2.5-flash");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Gemini, true),
            CancellationToken.None)).ShouldSucceed();

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeTrue();
        provider.Status.Should().Be(AgentProviderStatus.Ready);
        provider.StatusSummary.Should().Contain("ready for local desktop execution");
        provider.InstalledVersion.Should().Be("0.34.0");
        provider.SuggestedModelName.Should().Be("gemini-2.5-pro");
        provider.SupportedModelNames.Should().Contain("gemini-2.5-flash");
        provider.Details.Should().Contain(detail => detail.Label == "Suggested model" && detail.Value == "gemini-2.5-pro");
        provider.Details.Should().Contain(detail =>
            detail.Label == "Supported models" &&
            detail.Value.Contains("gemini-2.5-flash", StringComparison.Ordinal));
    }

    [Test]
    public async Task EnabledOnnxProviderReportsReadyRuntimeWhenModelDirectoryIsConfigured()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        var modelPath = commandScope.WriteOnnxModelDirectory("granite-vision-onnx");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Onnx, true),
            CancellationToken.None)).ShouldSucceed();

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeTrue();
        provider.Status.Should().Be(AgentProviderStatus.Ready);
        provider.StatusSummary.Should().Contain("ready for desktop execution");
        provider.SuggestedModelName.Should().Be("granite-vision-onnx");
        provider.SupportedModelNames.Should().ContainSingle().Which.Should().Be("granite-vision-onnx");
        provider.Details.Should().Contain(detail => detail.Label == "Configured model path" && detail.Value == modelPath);
        provider.Details.Should().Contain(detail => detail.Label == "Detected model type" && detail.Value == "phi3");
        provider.Details.Should().Contain(detail =>
            detail.Label == "Supported model types" &&
            detail.Value.Contains("decoder", StringComparison.Ordinal));
        provider.Details.Should().Contain(detail =>
            detail.Label == "Model path variables" &&
            detail.Value.Contains("DOTPILOT_ONNX_MODEL_PATH", StringComparison.Ordinal));
    }

    [Test]
    public async Task EnabledLlamaSharpProviderReportsReadyRuntimeWhenGgufFileIsConfigured()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        var modelPath = commandScope.WriteLlamaSharpModelFile("mistral-nemo-instruct.gguf");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.LlamaSharp, true),
            CancellationToken.None)).ShouldSucceed();

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeTrue();
        provider.Status.Should().Be(AgentProviderStatus.Ready);
        provider.StatusSummary.Should().Contain("ready for desktop execution");
        provider.SuggestedModelName.Should().Be("mistral-nemo-instruct");
        provider.SupportedModelNames.Should().ContainSingle().Which.Should().Be("mistral-nemo-instruct");
        provider.Details.Should().Contain(detail => detail.Label == "Configured model path" && detail.Value == modelPath);
        provider.Details.Should().Contain(detail => detail.Label == "Detected architecture" && detail.Value == "llama");
        provider.Details.Should().Contain(detail =>
            detail.Label == "Supported architectures" &&
            detail.Value.Contains("llama", StringComparison.Ordinal));
        provider.Details.Should().Contain(detail =>
            detail.Label == "Model path variables" &&
            detail.Value.Contains("DOTPILOT_LLAMASHARP_MODEL_PATH", StringComparison.Ordinal));
    }

    [Test]
    public async Task EnabledLlamaSharpProviderSurfacesEveryAddedModel()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        var firstModelPath = commandScope.WriteLlamaSharpModelFile("mistral-nemo-instruct.gguf", architecture: "mistral");
        var secondModelPath = commandScope.WriteLlamaSharpModelFile("qwen-3-4b-instruct.gguf", architecture: "qwen3");

        await using var fixture = CreateFixture();
        _ = (await fixture.Service.SetLocalModelPathAsync(
            new SetLocalModelPathCommand(AgentProviderKind.LlamaSharp, firstModelPath),
            CancellationToken.None)).ShouldSucceed();
        _ = (await fixture.Service.SetLocalModelPathAsync(
            new SetLocalModelPathCommand(AgentProviderKind.LlamaSharp, secondModelPath),
            CancellationToken.None)).ShouldSucceed();

        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.LlamaSharp, true),
            CancellationToken.None)).ShouldSucceed();

        provider.SuggestedModelName.Should().Be("qwen-3-4b-instruct");
        provider.SupportedModelNames.Should().ContainInOrder("qwen-3-4b-instruct", "mistral-nemo-instruct");
        provider.Details.Should().Contain(detail =>
            detail.Label == "Configured model paths" &&
            detail.Value.Contains(firstModelPath, StringComparison.Ordinal) &&
            detail.Value.Contains(secondModelPath, StringComparison.Ordinal));
        provider.Details.Should().Contain(detail =>
            detail.Label == "Supported models" &&
            detail.Value.Contains("qwen-3-4b-instruct", StringComparison.Ordinal) &&
            detail.Value.Contains("mistral-nemo-instruct", StringComparison.Ordinal));
    }

    [Test]
    public async Task EnabledOnnxProviderReportsErrorForUnsupportedGenAiModelType()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        _ = commandScope.WriteOnnxModelDirectory("unsupported-onnx-model", modelType: "qwen35");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Onnx, true),
            CancellationToken.None)).ShouldSucceed();

        provider.Status.Should().Be(AgentProviderStatus.Error);
        provider.CanCreateAgents.Should().BeFalse();
        provider.StatusSummary.Should().Contain("qwen35");
        provider.Details.Should().Contain(detail => detail.Label == "Detected model type" && detail.Value == "qwen35");
    }

    [Test]
    public async Task EnabledLlamaSharpProviderReportsErrorForUnsupportedArchitecture()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        _ = commandScope.WriteLlamaSharpModelFile("Qwen3.5-4B.Q2_K.gguf", architecture: "qwen35");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.LlamaSharp, true),
            CancellationToken.None)).ShouldSucceed();

        provider.Status.Should().Be(AgentProviderStatus.Error);
        provider.CanCreateAgents.Should().BeFalse();
        provider.StatusSummary.Should().Contain("qwen35");
        provider.Details.Should().Contain(detail => detail.Label == "Detected architecture" && detail.Value == "qwen35");
    }

    [Test]
    public async Task ReadAsyncFansOutSlowCliProbesInParallel()
    {
        const int probeDelayMilliseconds = 450;

        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: probeDelayMilliseconds);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");
        commandScope.WriteCountingVersionCommand("claude", "claude version 2.0.75", delayMilliseconds: probeDelayMilliseconds);
        commandScope.WriteClaudeSettings("claude-opus-4-6");
        commandScope.WriteCountingVersionCommand("gemini", "gemini-cli 0.34.0", delayMilliseconds: probeDelayMilliseconds);
        commandScope.WriteGeminiMetadata("gemini-2.5-pro", "gemini-2.5-pro");

        await using var fixture = CreateFixture();
        _ = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        _ = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.ClaudeCode, true),
            CancellationToken.None)).ShouldSucceed();
        _ = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Gemini, true),
            CancellationToken.None)).ShouldSucceed();

        var reader = fixture.Provider.GetRequiredService<IAgentProviderStatusReader>();
        reader.Invalidate();

        var startedAt = Stopwatch.GetTimestamp();
        var providers = await reader.ReadAsync(CancellationToken.None);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);

        providers.Should().Contain(provider => provider.Kind == AgentProviderKind.Codex);
        providers.Should().Contain(provider => provider.Kind == AgentProviderKind.ClaudeCode);
        providers.Should().Contain(provider => provider.Kind == AgentProviderKind.Gemini);
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(1200));
    }

    [Test]
    public async Task ConcurrentReadsShareOneInFlightProviderProbe()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 300);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");

        await using var fixture = CreateFixture();
        var reader = fixture.Provider.GetRequiredService<IAgentProviderStatusReader>();

        await Task.WhenAll(
            reader.ReadAsync(CancellationToken.None).AsTask(),
            reader.ReadAsync(CancellationToken.None).AsTask(),
            reader.ReadAsync(CancellationToken.None).AsTask());

        commandScope.ReadInvocationCount("codex").Should().Be(1);
    }

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        return new TestFixture(
            provider,
            provider.GetRequiredService<IAgentSessionService>());
    }

    private sealed class TestFixture(ServiceProvider provider, IAgentSessionService service) : IAsyncDisposable
    {
        private readonly ServiceProvider provider = provider;

        public ServiceProvider Provider { get; } = provider;

        public IAgentSessionService Service { get; } = service;

        public IAgentWorkspaceState WorkspaceState { get; } = provider.GetRequiredService<IAgentWorkspaceState>();

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }
}
