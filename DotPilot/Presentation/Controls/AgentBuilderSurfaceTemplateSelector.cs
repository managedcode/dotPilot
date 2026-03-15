namespace DotPilot.Presentation.Controls;

public sealed class AgentBuilderSurfaceTemplateSelector : DataTemplateSelector
{
    private const string MissingTemplateMessage = "Agent builder surface templates must be configured.";

    public DataTemplate? CatalogTemplate { get; set; }

    public DataTemplate? PromptTemplate { get; set; }

    public DataTemplate? EditorTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item switch
        {
            AgentBuilderSurfaceKind.Catalog =>
                CatalogTemplate ?? throw new InvalidOperationException(MissingTemplateMessage),
            AgentBuilderSurfaceKind.PromptComposer =>
                PromptTemplate ?? throw new InvalidOperationException(MissingTemplateMessage),
            AgentBuilderSurfaceKind.Editor =>
                EditorTemplate ?? throw new InvalidOperationException(MissingTemplateMessage),
            AgentBuilderSurface { Kind: AgentBuilderSurfaceKind.Catalog } =>
                CatalogTemplate ?? throw new InvalidOperationException(MissingTemplateMessage),
            AgentBuilderSurface { Kind: AgentBuilderSurfaceKind.PromptComposer } =>
                PromptTemplate ?? throw new InvalidOperationException(MissingTemplateMessage),
            AgentBuilderSurface { Kind: AgentBuilderSurfaceKind.Editor } =>
                EditorTemplate ?? throw new InvalidOperationException(MissingTemplateMessage),
            _ => CatalogTemplate ?? PromptTemplate ?? EditorTemplate ?? throw new InvalidOperationException(MissingTemplateMessage),
        };
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
