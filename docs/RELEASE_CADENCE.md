Release Cadence

- Feature branches: frequent builds, short-lived artifacts (14 days)
- main: integration builds, monthly tagged releases (retention 90 days)
- release/*: stabilization branches, artifacts retained 180 days

Tagging
- Use semver tags: vX.Y.Z
- publish-tag workflow builds, signs (optional), creates release, optionally publishes to Marketplace

Notes
- Update CHANGELOG.md for each release; publish-tag workflow extracts notes for the tag when present.
