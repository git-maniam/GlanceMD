# Security

GlanceMD treats Markdown, Mermaid, paths, links, and referenced files as untrusted.

Implemented controls:

- Markdig raw HTML is disabled.
- The HTML shell has a deny-by-default Content Security Policy.
- Mermaid 12.1.0 is pinned and bundled; no runtime CDN access is needed.
- Mermaid uses strict mode with HTML labels disabled, and generated SVG is stripped of scripts, foreign objects, active attributes, and external references.
- WebView devtools, downloads, permissions, browser accelerators, password saving, and autofill are disabled.
- New windows and navigation outside the trusted origin are intercepted.
- Remote images do not load.
- Local image access is restricted to raster files inside the Markdown file's directory and to 20 MiB per file.
- Markdown input, Mermaid count/source, output, and image sizes are bounded.
- Source files are opened read-only with shared access and are never saved.
- Logs and settings do not contain document bodies.

Known limitations for the current implementation:

- The JavaScript sanitizer is deliberately narrow but is not a substitute for staying current with Mermaid security releases.
- Junction/reparse-point resolution is constrained lexically. Deployments processing hostile local files should add final-handle path validation before relaxing any directory policy.
- Single-instance redirection is not yet implemented; each Windows activation may create its own viewer window.
