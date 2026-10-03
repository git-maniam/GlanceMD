# Windows installer

GlanceMD uses Inno Setup 6 to create a conventional per-user Windows installer.

## Build

```powershell
winget install --id JRSoftware.InnoSetup --exact
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\BuildInstaller.ps1 -Version 1.0.0
```

Outputs:

- `artifacts/installer/GlanceMD-Setup-x64.exe`
- `artifacts/installer/GlanceMD-Setup-x64.exe.sha256`

The setup executable contains the self-contained x64 application and does not require a separate .NET installation. It installs into the current user's local application directory by default, creates Start Menu and uninstall entries, and offers desktop-shortcut and Markdown-association tasks.

## Code signing

Development builds are unsigned. Windows SmartScreen or managed enterprise Code Integrity policies may warn about or block unsigned installers. A public release should sign both `GlanceMD.exe` and `GlanceMD-Setup-x64.exe` with an organization-controlled Authenticode certificate and trusted timestamp service. Do not commit private keys or certificate passwords to the repository.

After signing, regenerate and publish the SHA-256 checksum. Test install, launch, file association, upgrade, and uninstall on a clean Windows virtual machine before release.
