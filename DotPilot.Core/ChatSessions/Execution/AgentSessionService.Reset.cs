using ManagedCode.Communication;
using Microsoft.EntityFrameworkCore;

namespace DotPilot.Core.ChatSessions;

internal sealed partial class AgentSessionService
{
    private const string WorkspaceResetBlockedCode = "WorkspaceResetBlocked";
    private const string WorkspaceResetBlockedMessage = "Finish active session output before deleting local data.";

    public async ValueTask<Result<AgentWorkspaceSnapshot>> ResetWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (sessionActivityMonitor.Current.HasActiveSessions)
        {
            AgentSessionServiceLog.WorkspaceResetBlocked(
                logger,
                sessionActivityMonitor.Current.ActiveSessionCount);
            return Result<AgentWorkspaceSnapshot>.Fail(WorkspaceResetBlockedCode, WorkspaceResetBlockedMessage);
        }

        try
        {
            await _initializationGate.WaitAsync(cancellationToken);
            try
            {
                AgentSessionServiceLog.WorkspaceResetStarted(logger);
                await ResetWorkspaceCoreAsync(cancellationToken);
                AgentSessionServiceLog.WorkspaceResetCompleted(logger);
            }
            finally
            {
                _initializationGate.Release();
            }

            return await LoadWorkspaceAsync(forceRefreshProviders: true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.WorkspaceResetFailed(logger, exception);
            return Result<AgentWorkspaceSnapshot>.Fail(exception);
        }
    }

    private async Task ResetWorkspaceCoreAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await AgentProfileSchemaCompatibilityEnsurer.EnsureAsync(dbContext, cancellationToken);
        await ClearWorkspaceTablesAsync(dbContext, cancellationToken);
        await ClearRuntimeArtifactsAsync(cancellationToken);
        InvalidateProviderStatusSnapshot();
        _initialized = false;
        await EnsureDefaultProviderAndAgentAsync(dbContext, cancellationToken);
        _initialized = true;
    }

    private static async Task ClearWorkspaceTablesAsync(
        LocalAgentSessionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        dbContext.SessionEntries.RemoveRange(await dbContext.SessionEntries.ToListAsync(cancellationToken));
        dbContext.Sessions.RemoveRange(await dbContext.Sessions.ToListAsync(cancellationToken));
        dbContext.AgentProfiles.RemoveRange(await dbContext.AgentProfiles.ToListAsync(cancellationToken));
        dbContext.ProviderPreferences.RemoveRange(await dbContext.ProviderPreferences.ToListAsync(cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task ClearRuntimeArtifactsAsync(CancellationToken cancellationToken)
    {
        await sessionStateStore.ClearAsync(cancellationToken);
        await chatHistoryStore.ClearAsync(cancellationToken);
        if (storageOptions.UseInMemoryDatabase || OperatingSystem.IsBrowser())
        {
            return;
        }

        await LocalStorageDeletion.DeleteDirectoryIfExistsAsync(
            AgentSessionStoragePaths.ResolvePlaygroundRootDirectory(storageOptions),
            cancellationToken);
    }
}
