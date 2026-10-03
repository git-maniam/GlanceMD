using System.Net;
using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace GlanceMD.Core;

public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers()
            .DisableHtml()
            .Use(new SafeCodeBlockExtension())
            .Build();
    }

    public Task<RenderedDocument> RenderAsync(
        LoadedDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var syntaxTree = Markdown.Parse(document.Markdown, _pipeline);
        var outline = CreateOutline(syntaxTree);
        var mermaidBlocks = syntaxTree.Descendants<FencedCodeBlock>()
            .Count(block => IsMermaid(block));

        var warnings = new List<RenderWarning>();
        if (mermaidBlocks > GlanceMdLimits.MermaidDiagrams)
        {
            warnings.Add(new RenderWarning(
                "mermaid-limit",
                $"Only the first {GlanceMdLimits.MermaidDiagrams} Mermaid diagrams will be rendered."));
        }

        var body = Markdown.ToHtml(syntaxTree, _pipeline);
        if (body.Length > GlanceMdLimits.RenderedHtmlCharacters)
        {
            throw new InvalidOperationException("The rendered document exceeds the 50 MiB safety limit.");
        }

        return Task.FromResult(new RenderedDocument(
            body,
            outline,
            warnings,
            Math.Min(mermaidBlocks, GlanceMdLimits.MermaidDiagrams)));
    }

    private static IReadOnlyList<HeadingItem> CreateOutline(MarkdownDocument document)
    {
        var result = new List<HeadingItem>();
        var usedAnchors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var text = ExtractInlineText(heading.Inline).Trim();
            var anchor = Slugify(text);
            if (usedAnchors.TryGetValue(anchor, out var count))
            {
                count++;
                usedAnchors[anchor] = count;
                anchor = $"{anchor}-{count}";
            }
            else
            {
                usedAnchors[anchor] = 0;
            }

            heading.GetAttributes().Id = anchor;
            result.Add(new HeadingItem(heading.Level, text, anchor));
        }

        return result;
    }

    private static string ExtractInlineText(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var inline in container.Descendants())
        {
            if (inline is LiteralInline literal)
            {
                builder.Append(literal.Content.ToString());
            }
            else if (inline is CodeInline code)
            {
                builder.Append(code.Content);
            }
        }

        return builder.ToString();
    }

    private static string Slugify(string text)
    {
        var builder = new StringBuilder();
        var previousDash = false;
        foreach (var character in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousDash = false;
            }
            else if (!previousDash && builder.Length > 0)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "section" : slug;
    }

    private static bool IsMermaid(FencedCodeBlock block) =>
        block.Info is not null &&
        block.Info.ToString().Trim().Equals("mermaid", StringComparison.OrdinalIgnoreCase);

    private sealed class SafeCodeBlockExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline) { }

        public void Setup(MarkdownPipeline pipeline, Markdig.Renderers.IMarkdownRenderer renderer)
        {
            if (renderer is not HtmlRenderer htmlRenderer)
            {
                return;
            }

            var defaultRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
            if (defaultRenderer is not null)
            {
                htmlRenderer.ObjectRenderers.Remove(defaultRenderer);
            }

            htmlRenderer.ObjectRenderers.AddIfNotAlready(new SafeCodeBlockRenderer());
        }
    }

    private sealed class SafeCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
    {
        private int _mermaidIndex;

        protected override void Write(HtmlRenderer renderer, CodeBlock block)
        {
            var source = ReadLines(block);
            if (block is FencedCodeBlock fenced && IsMermaid(fenced))
            {
                WriteMermaid(renderer, source);
                return;
            }

            var language = block is FencedCodeBlock code
                ? code.Info?.ToString().Trim() ?? string.Empty
                : string.Empty;
            var languageClass = string.IsNullOrWhiteSpace(language)
                ? string.Empty
                : $" class=\"language-{WebUtility.HtmlEncode(language)}\"";

            renderer.Write("<div class=\"code-container\"><button class=\"copy-button no-print\" type=\"button\" data-copy=\"code\" aria-label=\"Copy code\">Copy</button><pre><code");
            renderer.Write(languageClass);
            renderer.Write(">");
            renderer.WriteEscape(source);
            renderer.WriteLine("</code></pre></div>");
        }

        private void WriteMermaid(HtmlRenderer renderer, string source)
        {
            _mermaidIndex++;
            if (_mermaidIndex > GlanceMdLimits.MermaidDiagrams)
            {
                WriteMermaidFallback(renderer, source, "Diagram limit exceeded.");
                return;
            }

            if (source.Length > GlanceMdLimits.MermaidSourceCharacters)
            {
                WriteMermaidFallback(renderer, source, "Diagram source exceeds the 256 KiB limit.");
                return;
            }

            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(source));
            renderer.Write("<figure class=\"mermaid-container\" data-mermaid-source=\"");
            renderer.WriteEscape(encoded);
            renderer.Write("\"><figcaption class=\"sr-only\">Mermaid diagram</figcaption>");
            renderer.Write("<div class=\"mermaid-output\" role=\"img\" aria-label=\"Mermaid diagram\"><span class=\"diagram-loading\">Rendering diagram…</span></div>");
            renderer.Write("<div class=\"diagram-actions no-print\"><button type=\"button\" data-copy=\"diagram\">Copy diagram</button><button type=\"button\" data-copy=\"mermaid\">Copy source</button></div>");
            renderer.Write("<details class=\"mermaid-fallback\"><summary>Mermaid source</summary><pre><code>");
            renderer.WriteEscape(source);
            renderer.WriteLine("</code></pre></details></figure>");
        }

        private static void WriteMermaidFallback(HtmlRenderer renderer, string source, string message)
        {
            renderer.Write("<aside class=\"render-error\"><strong>Mermaid diagram not rendered.</strong> ");
            renderer.WriteEscape(message);
            renderer.Write("<pre><code>");
            renderer.WriteEscape(source);
            renderer.WriteLine("</code></pre></aside>");
        }

        private static string ReadLines(LeafBlock block)
        {
            var builder = new StringBuilder();
            var lines = block.Lines.Lines;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                builder.Append(line.Slice.ToString());
                if (index < lines.Length - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }
    }
}
