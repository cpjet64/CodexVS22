VS Marketplace Listing Checklist

Manifest mapping
- DisplayName: matches listing title
- Description: concise, first paragraph for listing
- Icon/PreviewImage: `Resources/Icon.png` (ensure proper resolutions)
- Categories: Coding;Tools;Productivity
- Tags: AI;Assistant;Coding;Code Generation;Chat;Productivity;OpenAI;MCP;Tools;Visual Studio;VS2022
- License: EULA.md (published in repo)

Listing fields to validate
- Title, short description, long description
- Category selection and tags
- Screenshots/GIFs: use `docs/gifs/*.gif`
- Changelog: link CHANGELOG.md
- Support/More info: repository URL, issues link
- Privacy/Telemetry: statement in EULA/README if applicable

Windows-only dependencies
- Visual Studio 2022 (17.x), Extensibility workload
- Smoke test path: `/rootsuffix Exp`

