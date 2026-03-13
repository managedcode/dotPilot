namespace DotPilot.Core.Features.ToolchainCenter;

public interface IToolchainCenterCatalog
{
    ToolchainCenterSnapshot GetSnapshot();
}
