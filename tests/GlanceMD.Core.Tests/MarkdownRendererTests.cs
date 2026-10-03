using GlanceMD.Core;

namespace GlanceMD.Core.Tests;

public sealed class MarkdownRendererTests
{
    [Fact]
    public async Task RenderAsync_DisablesRawHtmlAndCreatesOutline()
    {
        var document = Create("# Hello *world*\n\n<script>alert(1)</script>\n\n## Next");
        var result = await new MarkdownRenderer().RenderAsync(document);

        Assert.DoesNotContain("<script>alert", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Collection(result.Outline,
            item => Assert.Equal(new HeadingItem(1, "Hello world", "hello-world"), item),
            item => Assert.Equal(new HeadingItem(2, "Next", "next"), item));
    }

    [Fact]
    public async Task RenderAsync_RendersMermaidAsEncodedPlaceholder()
    {
        var result = await new MarkdownRenderer().RenderAsync(Create("```mermaid\ngraph TD\nA-->B\n```"));

        Assert.Equal(1, result.MermaidDiagramCount);
        Assert.Contains("data-mermaid-source", result.Html);
        Assert.Contains("A--&gt;B", result.Html);
        Assert.Contains("Mermaid source", result.Html);
    }

    [Fact]
    public async Task RenderAsync_AddsCopyButtonToCode()
    {
        var result = await new MarkdownRenderer().RenderAsync(Create("```csharp\nvar x = 1;\n```"));
        Assert.Contains("data-copy=\"code\"", result.Html);
        Assert.Contains("language-csharp", result.Html);
    }

    private static LoadedDocument Create(string markdown) =>
        new("C:\\docs\\test.md", "test.md", markdown, DateTimeOffset.UtcNow, markdown.Length);
}
