# Testing

`GlanceMD.Core.Tests` covers source immutability, encoding, extension validation, raw HTML suppression, deterministic outlines, Mermaid placeholders, copy markup, image URL rewriting, unsafe URI schemes, traversal, image size policy, external-link confirmation, and recent-file ordering.

Before release, manually verify:

1. Open `test-data/documents/reference.md` by picker, drag/drop, command line, and file association.
2. Confirm text selection, code/source/diagram copy, find, outline, zoom, and theme behavior.
3. Print to PDF and inspect page breaks, diagrams, tables, and long code.
4. Test keyboard-only navigation and Windows Narrator.
5. Run with networking disabled.
6. Install, launch, and uninstall x64 and ARM64 MSIX packages.
