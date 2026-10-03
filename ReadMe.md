# GlanceMD

GlanceMD is a read-only Windows application for viewing Markdown files with formatted text, local raster images, code blocks, and offline Mermaid diagrams. It supports selection and copying, in-document search, outline navigation, file-change reload, themes, zoom, and printing.

The detailed product and engineering requirements are in [SPECIFICATION.md](SPECIFICATION.md).

## Requirements

- Windows 11
- .NET 10 SDK `10.0.401` or a compatible patch release
- Visual Studio 2026 with the Windows App SDK workload, or the .NET CLI
- WebView2 Runtime (normally installed with Microsoft Edge)

## Build and test

PowerShell:

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path (Get-Location) '.packages'

dotnet restore GlanceMD.slnx -r win-x64
dotnet test tests/GlanceMD.Core.Tests/GlanceMD.Core.Tests.csproj
dotnet build src/GlanceMD.App/GlanceMD.App.csproj -c Debug -p:Platform=x64
```

Run the packaged application:

```powershell
dotnet run --project src/GlanceMD.App/GlanceMD.App.csproj -p:Platform=x64
```

## Security model

Opened documents are untrusted and are never modified. Raw HTML is disabled, remote content is blocked by Content Security Policy, WebView navigation and permissions are intercepted, local images pass through a same-folder path/extension/size policy, and Mermaid output is sanitized before insertion. See [docs/security.md](docs/security.md) for details and known limitations.

## Third-party software

- Markdig 1.4.0, BSD-2-Clause
- Mermaid 12.1.0, MIT; bundled for offline rendering
- Windows App SDK, WebView2, and CommunityToolkit.Mvvm, Microsoft licenses

Notices are distributed in `WebAssets/THIRD-PARTY-NOTICES.txt`.
