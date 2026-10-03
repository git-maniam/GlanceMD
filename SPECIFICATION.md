# GlanceMD — Product and Engineering Specification

**Document status:** Implementation-ready baseline  
**Product:** GlanceMD for Windows  
**Version:** 1.0 specification  
**Last updated:** 2026-10-03  
**Intended audience:** Autonomous/agentic coding systems, developers, testers, and maintainers

---

## 1. Purpose

GlanceMD is a fast, secure, read-only Windows desktop application for viewing Markdown documents as formatted content. It renders common Markdown syntax, local images, links, syntax-highlighted code, and Mermaid diagrams. Users may select and copy displayed information and print the rendered document, but GlanceMD must never edit or overwrite the source Markdown file.

This document is the source of truth for version 1.0. An implementation agent should treat requirements marked **MUST** as mandatory, **SHOULD** as expected unless technically unjustified, and **MAY** as optional.

## 2. Product goals

GlanceMD MUST:

1. Open `.md`, `.markdown`, `.mdown`, and `.mkd` files from within the application or Windows Explorer.
2. Present Markdown as readable, polished, formatted content.
3. Render fenced `mermaid` code blocks as diagrams.
4. Remain strictly read-only with respect to source documents.
5. Allow normal text selection and copying from rendered content.
6. Allow copying code blocks and Mermaid diagrams.
7. Print the complete rendered document, including Mermaid diagrams and local images.
8. Work without an internet connection after installation.
9. Protect users from scripts, unsafe HTML, unsafe links, and unintended file/network access embedded in untrusted Markdown.
10. Integrate naturally with current supported Windows desktop conventions.

## 3. Non-goals for version 1.0

Version 1.0 will not include:

- Markdown editing, source preview, save, save-as, formatting commands, or autosave.
- Folder/project management, tabs, document history synchronization, cloud storage, collaboration, or comments.
- Export to PDF or HTML as a dedicated command. Users can choose a PDF printer from the Windows print dialog.
- Git operations or GitHub authentication.
- Plug-ins, user-authored scripts, or third-party themes.
- Math/LaTeX rendering unless subsequently approved as a separate feature.
- Automatic downloads of remote images or other remote document resources.
- Full browser behavior. GlanceMD is a document viewer, not a web browser.

## 4. Target users and primary scenarios

### 4.1 Target users

- Developers reading repository documentation.
- Business and technical users consuming Markdown reports or instructions.
- Users reviewing documents containing Mermaid flowcharts and architecture diagrams.

### 4.2 Primary scenarios

1. A user double-clicks a Markdown file and GlanceMD displays it.
2. A user launches GlanceMD, selects **Open**, and chooses a Markdown file.
3. A user drags a Markdown file onto the application window.
4. A user reads a document with relative local images and Mermaid diagrams.
5. A user selects formatted text and copies it.
6. A user copies the text of a code block with one action.
7. A user copies a rendered Mermaid diagram as an image.
8. A user prints the rendered document using the Windows print dialog.
9. A file changes on disk and the viewer refreshes safely while preserving reading position when feasible.

## 5. Technology baseline

Use the latest stable patch versions compatible with this baseline at implementation time. Do not use preview or experimental packages.

| Area | Required choice |
|---|---|
| Language/runtime | C# on .NET 10 LTS |
| Desktop UI | WinUI 3, Windows App SDK stable channel |
| Architecture | MVVM with dependency injection where it improves testability |
| Render surface | Microsoft Edge WebView2 |
| Markdown parser | Markdig |
| Mermaid | Mermaid JavaScript bundled as a local, pinned asset |
| Syntax highlighting | A small, locally bundled, pinned library such as highlight.js or Prism.js |
| Tests | xUnit for unit/integration tests; a suitable Windows UI test framework for critical UI flows |
| Packaging | MSIX, with x64 and ARM64 deliverables; x86 is optional |
| CI | GitHub Actions Windows runner |

The implementation MUST pin dependency versions through normal package manifests and commit the lock file where supported. Third-party licenses and notices MUST be included in the distributed application.

## 6. Supported platform

- Windows 11 is the primary supported operating system.
- Windows 10 support MAY be included only where the selected stable Windows App SDK and .NET runtime officially support it.
- Supported CPU architectures: x64 and ARM64.
- The app SHOULD be self-contained so users do not separately install .NET.
- The installer MUST account for the Windows App SDK and WebView2 Runtime requirements using supported deployment mechanisms.
- The app MUST behave correctly at 100%–300% display scaling and across multiple monitors.

## 7. Functional requirements

### FR-01: Launch states

When launched without a document, the app MUST show a lightweight welcome state containing:

- Product name and short description.
- A prominent **Open Markdown file** action.
- Drag-and-drop guidance.
- Up to 10 recent documents, if available and enabled.

The empty state MUST NOT create a new Markdown document.

### FR-02: Open document

The application MUST support:

- `Ctrl+O` and an **Open** command.
- A file picker filtered to supported Markdown extensions plus an **All files** option.
- Command-line file paths.
- Windows file activation/file association.
- Drag-and-drop of exactly one supported file onto the window.

Opening a second document in the existing single-document window replaces the current document. If the operating system launches another app instance through file activation, the implementation SHOULD redirect activation to the existing instance and foreground it.

Unsupported, missing, inaccessible, or oversized files MUST result in a clear non-destructive error message. The prior document MUST remain visible when a new open operation fails.

### FR-03: Read-only guarantee

GlanceMD MUST NOT expose an editor or write to an opened source document. It MUST open source files with read sharing that permits other applications to modify them. It MUST NOT:

- Change source contents, metadata, timestamps, permissions, or encoding.
- Create sidecar files in the source directory.
- Offer save or auto-format operations.
- Acquire a persistent exclusive file lock.

Application settings, logs, crash data, and a recent-file list may be stored only in application-controlled storage. The application MUST NOT store document contents in telemetry or logs.

### FR-04: Markdown rendering

The renderer MUST support at least:

- Headings levels 1–6.
- Paragraphs, line breaks, emphasis, strong emphasis, and strikethrough.
- Ordered, unordered, nested, and task lists.
- Block quotes and horizontal rules.
- Inline code and fenced code blocks with an optional language identifier.
- Tables compatible with GitHub-flavored Markdown.
- Links, autolinks, and reference links.
- Local images and image alt text.
- Escaped Markdown characters.
- Footnotes.
- YAML front matter, hidden from the rendered body.
- Heading anchors with deterministic, unique identifiers.

Raw HTML embedded in Markdown MUST be disabled or sanitized using a strict allowlist. Scripts, event-handler attributes, forms, iframes, objects, embeds, stylesheets, and active content MUST never be admitted from a document.

### FR-05: Mermaid rendering

A fenced code block whose language is `mermaid` (case-insensitive) MUST be rendered as a diagram.

Requirements:

- Mermaid MUST be loaded from a packaged local asset, never from a CDN.
- Mermaid MUST be initialized with a strict security setting and without arbitrary HTML labels.
- Each diagram MUST be rendered independently so one invalid diagram does not block the document.
- Until rendering completes, show a small accessible progress placeholder.
- On failure, show an inline error panel, a concise error message, and the original Mermaid source in a collapsible or selectable code block.
- The failure UI MUST offer **Copy Mermaid source**.
- A successful diagram MUST offer **Copy diagram** and MUST remain selectable/focusable through keyboard navigation.
- Diagram colors MUST be legible in light, dark, high-contrast, screen, and print modes.
- Diagram SVG MUST be sanitized before insertion into the document.
- Rendering MUST have a timeout to prevent malformed definitions from freezing the app.

Support the Mermaid diagram types provided by the pinned Mermaid version. Do not maintain a separate hand-written list in application logic.

### FR-06: Images and resource resolution

- Relative local image paths MUST resolve relative to the opened Markdown file's directory.
- Absolute `file:` paths and paths escaping the document directory MUST be blocked by default.
- Data-URI images MUST be blocked unless a strict size and MIME allowlist is implemented.
- Remote `http` and `https` images MUST be blocked by default to preserve privacy and offline behavior.
- A blocked or missing image MUST show its alt text and a visible placeholder without crashing rendering.
- SVG files loaded from a document MUST be blocked or rigorously sanitized. The preferred v1 behavior is to block external SVG files.

### FR-07: Links

- Heading/fragment links MUST navigate inside the rendered document.
- Relative links to another supported local Markdown file inside the current document directory MUST open in GlanceMD.
- Relative links to non-Markdown local files MUST require explicit user confirmation before Windows opens them.
- `https` and `http` links MUST require an explicit click and open in the user's default browser after confirmation or according to a persisted user preference.
- `mailto` links MAY use the system handler after confirmation.
- All other URI schemes, including `javascript`, `data`, `vbscript`, `file`, and custom schemes, MUST be blocked unless specifically allowed by a later requirement.
- Hovering or focusing a link SHOULD display its destination.
- WebView navigation away from the trusted local app content MUST always be cancelled; approved external links are launched through the OS instead.

### FR-08: Selection and copying

- Users MUST be able to select rendered text with mouse and keyboard.
- `Ctrl+C` MUST copy the current selection as plain text and MAY include standard rich HTML clipboard data.
- Every fenced code block MUST have a keyboard-accessible **Copy code** button.
- A successful Mermaid diagram's **Copy diagram** action MUST place a raster image on the clipboard; including SVG clipboard data is optional.
- Copy actions MUST show a brief, non-modal success indication.
- The app MUST never place content on the clipboard without a user action.

### FR-09: Printing

`Ctrl+P` and the **Print** command MUST open the standard WebView2/system print UI for the complete rendered document.

Print output MUST:

- Include formatted Markdown, local raster images, and successfully rendered Mermaid diagrams.
- Exclude app chrome, toolbars, copy buttons, loading indicators, and error-detail expanders unless their source fallback is the only representation.
- Use a white background and dark text regardless of the selected app theme.
- Avoid splitting headings from their immediate content where practical.
- Repeat table headers across pages where browser print support permits.
- Wrap long code lines or otherwise prevent silent clipping.
- Expand all document content needed for print.
- Contain readable link destinations where appropriate.

The Print command MUST wait for Mermaid rendering to complete or time out before displaying the dialog. If any diagrams failed, inform the user that their source fallback will be printed.

### FR-10: Find

- `Ctrl+F` MUST open an in-document find bar.
- The find bar MUST support next, previous, match count, case-insensitive matching by default, and `Esc` to close.
- Matches inside visible Mermaid text are optional; matches in Mermaid source fallbacks are expected when those fallbacks are visible.

### FR-11: Zoom

- `Ctrl++`, `Ctrl+-`, and `Ctrl+0` MUST adjust/reset document zoom.
- Supported range: 50%–300%.
- The current zoom value MUST be accessible and persist across sessions.
- Browser-style `Ctrl` + mouse wheel zoom SHOULD work.

### FR-12: File changes

The application MUST watch the open file for changes without locking it.

- If the source changes and no read is in progress, debounce events and reload automatically.
- Preserve approximate scroll position or nearest visible heading when practical.
- If the file is deleted or becomes unavailable, retain the last successfully rendered view and show a persistent warning.
- If rapid changes occur, coalesce them and avoid flicker.
- A manual **Reload** command and `Ctrl+R` MUST be provided.

### FR-13: Recent documents

- Store at most 10 recent file paths in application settings.
- Show filename and parent folder to disambiguate entries.
- Remove inaccessible entries when selected or offer to remove them.
- Provide **Clear recent files**.
- Never store document contents or thumbnails in the recent list.
- A setting MUST allow users to disable recent-document history.

### FR-14: Window and theme behavior

- The window title MUST be `<filename> — GlanceMD`, or `GlanceMD` with no open file.
- The application MUST support light, dark, and system-default themes.
- Changing theme MUST update app chrome, rendered Markdown, code highlighting, and Mermaid diagrams.
- Window size, position, state, theme, zoom, and sidebar visibility MAY persist locally.
- Restored window bounds MUST be clamped to currently available displays.

### FR-15: Outline navigation

- The app SHOULD offer a collapsible outline derived from document headings.
- Selecting an outline item scrolls to its heading.
- The current section SHOULD be reflected in the outline as the user scrolls.
- Documents without headings MUST not show an empty distracting panel.

### FR-16: Diagnostics and errors

- Expected errors MUST be presented in plain language with a recovery action.
- Technical details MAY be expandable and copyable.
- Logs MUST avoid document body content, Mermaid source, query strings, and secrets.
- A diagnostics/about page MUST show application version, runtime versions, open-source notices, and a **Copy diagnostics** action.
- The app MUST remain usable when an individual resource, diagram, or renderer operation fails.

## 8. User interface specification

### 8.1 Main window layout

Use a native WinUI title bar and command surface. The recommended layout is:

```text
┌─────────────────────────────────────────────────────────────┐
│ GlanceMD       Open  Reload  Print  Find  Zoom  Theme  More │
├───────────────┬─────────────────────────────────────────────┤
│ Outline       │ Rendered Markdown document                  │
│ (collapsible) │                                             │
│               │                                             │
├───────────────┴─────────────────────────────────────────────┤
│ Status / warnings / file-change state                       │
└─────────────────────────────────────────────────────────────┘
```

The document is the dominant surface. Keep controls visually quiet. Do not show editor affordances, a source pane, or a caret that implies editability.

### 8.2 Commands and shortcuts

| Command | Shortcut |
|---|---|
| Open | `Ctrl+O` |
| Print | `Ctrl+P` |
| Find | `Ctrl+F` |
| Copy selected content | `Ctrl+C` |
| Reload | `Ctrl+R` or `F5` |
| Zoom in | `Ctrl++` |
| Zoom out | `Ctrl+-` |
| Reset zoom | `Ctrl+0` |
| Toggle outline | `Ctrl+Shift+O` |
| Close find/error flyout | `Esc` |
| Close app | `Alt+F4` |

### 8.3 Loading and transitions

- For small documents, avoid flashing a loading surface.
- For operations exceeding approximately 200 ms, show a subtle progress indicator.
- Retain the last successful view until the replacement render is ready.
- Do not animate large document content on reload.

## 9. Accessibility requirements

The application MUST meet WCAG 2.2 AA principles where applicable to desktop software and follow Windows accessibility guidance.

- All commands and document actions MUST be keyboard accessible.
- Focus indicators MUST be visible.
- Icon-only buttons MUST have accessible names and tooltips.
- Logical focus order MUST follow visual order.
- Text contrast MUST meet at least 4.5:1 for normal text and 3:1 for large text/UI graphics.
- UI MUST remain usable at 200% text scaling and 300% document zoom.
- High Contrast mode MUST be respected.
- Images MUST expose Markdown alt text to assistive technology.
- Mermaid diagrams MUST expose an accessible label. If no explicit description exists, expose a label such as `Mermaid diagram` and make source available.
- Error notifications MUST be announced without unexpectedly stealing focus.
- The rendered document MUST use semantic HTML elements so WebView2 accessibility maps headings, lists, tables, links, and code correctly.

## 10. Security and privacy model

Treat every opened Markdown file and every referenced resource as untrusted input.

### 10.1 Required controls

1. Disable Markdig raw HTML output or sanitize it with an audited strict allowlist.
2. Use a restrictive Content Security Policy. At minimum, default sources are `none`; scripts/styles may load only from packaged application assets; network connections and frames are prohibited.
3. Do not enable general WebView host-object exposure.
4. If native/web messaging is needed, use a narrow versioned message schema, validate origin and payload size/type, and allowlist commands.
5. Intercept all navigation, new-window, download, permission, and external-scheme events.
6. Disable WebView context-menu features that imply editing or unsafe browser operations. Retain safe copy/select actions.
7. Disable browser devtools in release builds.
8. Do not use `eval` or inject Markdown text into executable JavaScript strings.
9. Insert untrusted Mermaid source using data transfer/DOM text APIs, never HTML concatenation.
10. Sanitize Mermaid-generated SVG and prohibit foreign objects, scripts, event handlers, external references, and unsafe URLs.
11. Apply file-size, resource-size, diagram-count, diagram-source-size, recursion, and render-time limits.
12. Cancel pending rendering when a new document is opened or the window closes.
13. No analytics or telemetry by default. Any future telemetry requires explicit specification and user control.

### 10.2 Initial defensive limits

Implement these as named configuration values, with tests:

| Limit | Initial value |
|---|---:|
| Markdown file size | 10 MiB |
| Local raster image size | 20 MiB per image |
| Mermaid diagrams | 100 per document |
| Mermaid source | 256 KiB per diagram |
| Mermaid render timeout | 10 seconds per diagram, 30 seconds total |
| Rendered HTML size | 50 MiB |

If a limit is exceeded, degrade gracefully and explain the affected item. Do not crash or hang.

### 10.3 Content Security Policy baseline

The implementation agent must adapt this to the exact local hosting mechanism while preserving its intent:

```text
default-src 'none';
script-src 'self';
style-src 'self';
img-src 'self' data:;
font-src 'self';
connect-src 'none';
media-src 'none';
object-src 'none';
frame-src 'none';
base-uri 'none';
form-action 'none';
```

Avoid `'unsafe-inline'`. If unavoidable for generated styles, use nonces or hashes and document the decision.

## 11. Rendering architecture

### 11.1 Processing pipeline

```text
File activation/open
  → validate extension/path/size
  → read bytes asynchronously
  → detect supported encoding
  → normalize text for parsing (never write it back)
  → parse Markdown with configured Markdig pipeline
  → extract headings and Mermaid blocks
  → render sanitized semantic HTML
  → wrap in trusted local HTML shell + CSP + packaged CSS/JS
  → navigate WebView2 to trusted local content
  → render/sanitize Mermaid diagrams
  → signal document-ready state
  → enable Print
```

### 11.2 Encoding

- UTF-8 with or without BOM MUST be supported.
- UTF-16 LE/BE with BOM SHOULD be supported.
- Invalid byte sequences MUST produce a clear error or safe replacement characters; they must not crash the app.
- GlanceMD MUST never transcode and save the source.

### 11.3 Trusted content hosting

Prefer WebView2 virtual-host mapping or another supported local mechanism that gives packaged assets a fixed trusted HTTPS-like origin. Do not expose a broad source directory. Resource requests MUST be intercepted and mapped through an allowlisted resolver so the web content cannot freely read local files.

### 11.4 Separation of concerns

The solution SHOULD isolate:

- File activation and document loading.
- Markdown parsing/render transformation.
- Safe local-resource resolution.
- HTML shell generation.
- WebView lifecycle and navigation policy.
- Mermaid rendering coordination.
- Printing.
- Settings and recent files.
- File watching/reload.

Core parsing, policy, path resolution, and view-model logic MUST be testable without launching the full UI.

## 12. Suggested solution structure

```text
GlanceMD.sln
src/
  GlanceMD.App/                 # WinUI 3 application and composition root
    Assets/
    Controls/
    Services/
    ViewModels/
    Views/
    WebAssets/                  # pinned Mermaid/highlighter/CSS/JS shell
  GlanceMD.Core/                # parsing, models, policies, resource resolution
tests/
  GlanceMD.Core.Tests/
  GlanceMD.App.Tests/
  GlanceMD.UiTests/
test-data/
  documents/                    # non-sensitive rendering/security fixtures
docs/
  architecture.md
  security.md
  testing.md
  third-party-notices.md
.github/workflows/
  ci.yml
Directory.Build.props
Directory.Packages.props
README.md
LICENSE
```

Naming may change if required by templates, but dependency direction MUST remain: `App → Core`; `Core` must not depend on WinUI or WebView2.

## 13. Core data contracts

The exact types may evolve, but preserve these concepts:

```csharp
public sealed record DocumentRequest(string FullPath);

public sealed record LoadedDocument(
    string FullPath,
    string DisplayName,
    string Markdown,
    DateTimeOffset LastModified,
    long SizeInBytes);

public sealed record RenderedDocument(
    string Html,
    IReadOnlyList<HeadingItem> Outline,
    IReadOnlyList<RenderWarning> Warnings,
    int MermaidDiagramCount);

public sealed record HeadingItem(int Level, string Text, string Anchor);

public interface IDocumentLoader
{
    Task<LoadedDocument> LoadAsync(DocumentRequest request, CancellationToken ct);
}

public interface IMarkdownRenderer
{
    Task<RenderedDocument> RenderAsync(LoadedDocument document, CancellationToken ct);
}

public interface IResourcePolicy
{
    ResourceDecision Evaluate(string documentPath, string requestedReference);
}
```

Use result/error types or documented exceptions consistently. UI-facing messages must not be constructed deep inside parsing logic.

## 14. Performance and reliability

Target hardware: a supported Windows device with 4 logical cores and 8 GiB RAM.

- Cold launch to usable empty window: p95 under 2 seconds.
- Open and display a 1 MiB Markdown file with no diagrams: p95 under 1 second.
- UI thread must remain responsive while loading, parsing, highlighting, diagram rendering, and file watching.
- Initial content SHOULD appear before all diagrams finish where this does not compromise print readiness.
- Repeated reloads MUST cancel obsolete work and must not leak WebView handlers or DOM nodes.
- Memory after closing/replacing a large document SHOULD substantially return after garbage collection; establish a repeatable soak test.
- A malformed document MUST not terminate the process.
- Unhandled exceptions MUST be logged safely and shown as a recoverable fatal-error view when possible.

## 15. Testing requirements

### 15.1 Unit tests

At minimum, test:

- Supported extension and file-size validation.
- UTF encoding detection.
- Markdown features and stable heading-anchor generation.
- Raw HTML/script stripping.
- Mermaid block extraction and source preservation.
- URI scheme policy.
- Relative path normalization and traversal rejection.
- Local image allow/block decisions.
- Recent-list ordering, deduplication, cap, and disable behavior.
- Settings defaults and migration.
- File-watcher debounce logic.
- Cancellation and stale-render rejection.

### 15.2 Security fixtures

Include regression documents covering:

- `<script>`, inline event handlers, malicious SVG, iframe/object/embed/form tags.
- `javascript:`, `data:`, `file:`, UNC, device paths, encoded traversal, mixed slash traversal, and symlink/junction escape attempts.
- Mermaid labels containing HTML/script payloads.
- Oversized Mermaid source, excessive diagrams, deeply nested Markdown, and huge tables.
- Remote images, tracking pixels, redirects, and download attempts.
- WebView new-window and permission requests.

Tests MUST assert that active content does not execute and blocked resources are not fetched.

### 15.3 Rendering tests

Maintain representative golden/sample documents for:

- CommonMark/GFM constructs.
- Light, dark, high contrast, and print styling.
- Wide tables, long code lines, Unicode, RTL text, emoji, and CJK content.
- Valid and invalid Mermaid diagrams.
- Missing and blocked images.

Avoid brittle full-page pixel tests as the only validation. Combine semantic DOM assertions, focused screenshots, and manual release checks.

### 15.4 UI/end-to-end tests

Automate the critical path where practical:

1. Launch empty state.
2. Open a fixture through the picker abstraction or activation.
3. Confirm heading, table, code, local image, and Mermaid output.
4. Select/copy text and use a code copy button.
5. Open find and navigate matches.
6. Reload after an external file change.
7. Exercise print initiation with a test seam; do not require a physical printer in CI.
8. Navigate entirely by keyboard.

### 15.5 CI quality gates

Every pull request MUST:

- Restore from locked dependencies.
- Build Release configuration with warnings treated as errors for first-party code.
- Run all non-interactive unit and integration tests.
- Run formatting/static analysis.
- Produce an MSIX build artifact on the protected release workflow.
- Run dependency and secret scanning.
- Fail if bundled third-party asset notices are missing.

## 16. Acceptance criteria for version 1.0

Release is acceptable only when all statements below are demonstrably true:

1. A supported Markdown file can be opened via picker, drag/drop, command line, and file activation.
2. The supplied reference fixture renders all features listed in FR-04 correctly.
3. Valid Mermaid fixtures render offline; invalid ones show isolated, copyable fallbacks.
4. Text, code, and diagram copy actions work through keyboard-accessible controls.
5. Print preview includes the entire document and diagrams without app controls.
6. No application workflow modifies an opened Markdown file; an automated hash/timestamp test confirms this.
7. Embedded script/HTML/SVG/navigation security fixtures cannot execute active content, read arbitrary local files, or issue network requests.
8. The app remains responsive for a 10 MiB limit-case document and fails gracefully above the limit.
9. Light, dark, system, high-contrast, and print presentations remain legible.
10. A screen-reader smoke test exposes document headings, links, lists, tables, code actions, and diagram labels.
11. x64 and ARM64 packaged builds install, launch, open a document, print, and uninstall cleanly.
12. CI passes and the repository contains build, test, packaging, security, and dependency documentation.

## 17. Delivery plan

### Milestone 1 — Foundation

- Create solution, WinUI app, Core library, test projects, CI, analyzers, and packaging skeleton.
- Implement empty state, settings infrastructure, file picker, activation, drag/drop, and safe asynchronous loader.
- Demonstrate the read-only invariant with tests.

### Milestone 2 — Secure Markdown viewer

- Configure Markdig and semantic HTML shell.
- Integrate WebView2 with trusted local asset hosting, CSP, navigation interception, and theme styles.
- Implement headings/outline, local raster resources, links, selection/copy, find, zoom, and errors.
- Add security regression suite before adding Mermaid.

### Milestone 3 — Mermaid and printing

- Bundle pinned Mermaid assets and notices.
- Add independent diagram rendering, sanitization, timeout/fallback, accessibility, and copy-image behavior.
- Add print coordination and print stylesheet.

### Milestone 4 — Windows integration and hardening

- Add single-instance file activation, recent documents, file watcher/reload, persisted window state, and diagnostics.
- Complete accessibility, performance, soak, installer, x64, and ARM64 verification.
- Finish documentation and release checklist.

Each milestone MUST end with a runnable application and passing tests. Do not postpone security enforcement until the last milestone.

## 18. Implementation instructions for an agentic coding system

An autonomous coding agent receiving this specification MUST follow this operating contract:

1. Read this entire specification and inspect the repository before changing files.
2. Treat this file as the requirements baseline. Do not silently remove or reinterpret requirements.
3. Before implementation, create a short traceable plan mapping work items to requirement IDs and milestones.
4. Prefer the technology choices in section 5. If a required package is incompatible, document evidence, propose the smallest substitute, and preserve behavior/security requirements.
5. Use only stable package versions. Pin versions and record licenses. Never retrieve runtime scripts from CDNs.
6. Implement vertical slices in milestone order. Keep the repository buildable after each slice.
7. Add or update tests with every behavior change. Security controls require negative tests.
8. Never weaken CSP, sanitization, navigation policy, path validation, or limits merely to make a fixture pass.
9. Do not add Markdown editing, source writes, cloud services, analytics, or other non-goals.
10. Keep UI-thread work minimal and pass cancellation tokens through load/render operations.
11. Run formatter, build, tests, and relevant security fixtures before declaring a task complete.
12. Report: files changed, requirements satisfied, commands/tests run, results, remaining risks, and next milestone.
13. If blocked by an ambiguous product choice, implement the safest reversible default when it does not materially change scope; otherwise stop and ask one focused question.
14. Never claim completion based only on compilation. Validate behavior against section 16.

### Definition of done for every work item

A work item is done only when:

- Behavior is implemented and linked to one or more requirement IDs.
- Automated tests cover success, failure, and security-relevant cases.
- Relevant documentation is updated.
- Release build completes without new warnings.
- No secrets, generated build output, or unlicensed vendor files are committed.
- Accessibility names and keyboard behavior are verified for affected UI.

## 19. Product decisions requiring explicit approval after v1

Do not implement these implicitly:

- Whether to allow remote images per document.
- Whether to support trusted raw HTML.
- Whether to add PDF/HTML export.
- Whether to add tabs or multiple windows.
- Whether to add KaTeX/MathJax rendering.
- Whether to distribute through Microsoft Store, GitHub releases, enterprise MSIX, or all three.
- Whether to add update checking or telemetry.

## 20. Reference fixture definition

The repository SHOULD include a `test-data/documents/reference.md` fixture containing:

- All heading levels and a generated outline.
- Emphasis, strikeout, links, block quote, lists, task list, footnote, and horizontal rule.
- A wide GFM table.
- Inline code and fenced code in at least two languages.
- A relative PNG/JPEG with alt text and a deliberately missing image.
- At least three Mermaid diagrams and one invalid Mermaid block.
- Unicode, emoji, CJK, RTL text, and long unbroken content.
- Page-break pressure for print validation.

Expected behavior for that fixture MUST be documented without placing environment-specific absolute paths in snapshots.

---

## Appendix A — Requirement priority

- **P0:** Safe opening, read-only behavior, core Markdown, Mermaid, copy, print, offline operation, security controls, error containment, x64 packaging.
- **P1:** ARM64, file watching, recent files, outline, theme, zoom, find, accessibility completeness, robust activation.
- **P2:** Optional rich clipboard formats, SVG clipboard data, x86, refinements that do not gate the core workflow.

P0 and P1 are required for version 1.0 unless this specification is explicitly revised.

## Appendix B — Release checklist

- [ ] Version and changelog finalized.
- [ ] Clean Release build from a fresh clone.
- [ ] Unit, integration, security, and UI smoke tests pass.
- [ ] Reference fixture reviewed in light, dark, and high-contrast modes.
- [ ] Reference fixture print/PDF output reviewed.
- [ ] Source-file hash and timestamp remain unchanged after every workflow.
- [ ] Offline rendering verified with networking disabled.
- [ ] External navigation and local traversal attacks verified blocked.
- [ ] x64 and ARM64 MSIX install/upgrade/uninstall verified.
- [ ] App icon, identity, file associations, and signing configured.
- [ ] Third-party notices and licenses included.
- [ ] Accessibility keyboard and screen-reader smoke tests completed.
- [ ] No secrets, debug flags, devtools, or verbose content logging in release build.
- [ ] Known issues documented.
