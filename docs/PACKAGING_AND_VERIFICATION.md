Packaging and Post-Test Verification (T10)

VSIX Build and ZIP Verification
- Build Release: `msbuild CodexVS22.sln /t:Rebuild /p:Configuration=Release /m`.
- Locate `.vsix` under `bin/Release` and verify size/signing (if enabled).
- ZIP install verification: package the VSIX into a ZIP for distribution checks, verify integrity and content structure.
- Local install: double-click VSIX, ensure it installs into `/rootsuffix Exp` during dev.

Parity Checklist
- Protocol handling parity: kinds, tolerant parsing.
- Diff/patch parity: conflict, RO/SCC guards.
- Exec parity: stream/ANSI/trim behavior.
- MCP parity: tools/prompts list + persistence via options.
- Options parity: validation defaults, solution overrides.

EULA/Branding
- See EULA.md and BRANDING.md for policy and asset usage notes.

CHANGELOG
- See CHANGELOG.md for notable features and limits.

Demo GIFs
- See docs/DEMO_GIFS.md for capture plan and embed locations.

