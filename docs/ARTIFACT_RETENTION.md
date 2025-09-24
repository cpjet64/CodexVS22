Artifact Retention Policy

Branch-based retention settings for CI artifacts:
- main: 90 days (vsix-main)
- release/*: 180 days (vsix-release)
- feature/*: 14 days (vsix-feature)

Notes
- Artifacts include built VSIX, build logs, and Release output tree.
- All values configurable via GitHub Actions upload-artifact retention-days.
