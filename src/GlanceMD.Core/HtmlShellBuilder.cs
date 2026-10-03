using System.Net;
using System.Text.Json;

namespace GlanceMD.Core;

public static class HtmlShellBuilder
{
    public static string Build(RenderedDocument document, string title, string theme)
    {
        ArgumentNullException.ThrowIfNull(document);
        var safeTheme = theme.Equals("dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
        var titleJson = JsonSerializer.Serialize(title);

        return $$"""
            <!doctype html>
            <html lang="en" data-theme="{{safeTheme}}">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src https://app.glancemd.local; style-src https://app.glancemd.local; img-src https://resource.glancemd.local data:; font-src https://app.glancemd.local; connect-src 'none'; media-src 'none'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'">
              <title>{{WebUtility.HtmlEncode(title)}}</title>
              <link rel="stylesheet" href="https://app.glancemd.local/styles.css">
            </head>
            <body>
              <main id="document" tabindex="-1">{{document.Html}}</main>
              <div id="copy-status" role="status" aria-live="polite"></div>
              <script src="https://app.glancemd.local/mermaid.min.js"></script>
              <script src="https://app.glancemd.local/app.js"></script>
              <script type="application/json" id="document-metadata">{"title":{{titleJson}},"diagrams":{{document.MermaidDiagramCount}}}</script>
            </body>
            </html>
            """;
    }
}
