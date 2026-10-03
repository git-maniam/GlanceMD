namespace GlanceMD.Core;

public sealed record DocumentRequest(string FullPath);

public sealed record LoadedDocument(
    string FullPath,
    string DisplayName,
    string Markdown,
    DateTimeOffset LastModified,
    long SizeInBytes);

public sealed record HeadingItem(int Level, string Text, string Anchor);

public sealed record RenderWarning(string Code, string Message);

public sealed record RenderedDocument(
    string Html,
    IReadOnlyList<HeadingItem> Outline,
    IReadOnlyList<RenderWarning> Warnings,
    int MermaidDiagramCount);

public enum ResourceDecisionKind
{
    AllowedLocalImage,
    AllowedLocalMarkdown,
    RequiresConfirmation,
    Blocked
}

public sealed record ResourceDecision(ResourceDecisionKind Kind, string? ResolvedPath, string Reason);

public static class GlanceMdLimits
{
    public const long MarkdownFileBytes = 10 * 1024 * 1024;
    public const long LocalImageBytes = 20 * 1024 * 1024;
    public const int MermaidDiagrams = 100;
    public const int MermaidSourceCharacters = 256 * 1024;
    public const int RenderedHtmlCharacters = 50 * 1024 * 1024;
}
