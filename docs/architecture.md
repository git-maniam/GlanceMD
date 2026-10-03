# Architecture

GlanceMD is split into a framework-independent Core library and a WinUI 3 application.

The Core library owns source loading, limits, Markdown parsing, heading extraction, Mermaid placeholders, safe resource policy, and HTML shell creation. It has no WinUI or WebView dependency and is covered by xUnit tests.

The App owns Windows activation, file picking and drag/drop, WebView2 lifecycle, clipboard and printing, file watching, settings, and recent documents. Markdown is rendered before navigation. The generated document loads only packaged scripts and styles from `app.glancemd.local`. Local raster images are rewritten to `resource.glancemd.local` and served only after Core policy approval.

```text
File activation / picker / drop
    -> DocumentLoader
    -> MarkdownRenderer (raw HTML disabled)
    -> HtmlResourceRewriter
    -> HtmlShellBuilder (strict CSP)
    -> WebView2
    -> bundled Mermaid -> sanitized SVG
```
