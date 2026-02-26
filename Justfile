set windows-shell := ["powershell.exe", "-NoLogo", "-Command"]

# Pre-commit: fast checks
ci-fast: hygiene restore build-vsix test

# Pre-push: deeper checks
ci-deep: ci-fast package-check

hygiene:
    powershell -NoLogo -File scripts/hygiene.ps1

restore:
    & "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; msbuild CodexVS22.sln /t:Restore /m

# Build the VSIX project with MSBuild (split from SDK-style test project).
build-vsix:
    & "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; msbuild CodexVS22.csproj /t:Rebuild /p:Configuration=Release /p:DeployExtension=false /m

# Build and run tests with dotnet for SDK-style test project support.
test:
    & "C:\Users\curtp\.codex\scripts\ensure-vcvars.ps1" -Quiet; dotnet run --project CodexVS22.Tests/CodexVS22.Tests.csproj -c Release

package-check:
    powershell -NoLogo -Command "if (-not (Get-ChildItem -Recurse -Filter *.vsix | Select-Object -First 1)) { throw 'VSIX not found after build.' }"
