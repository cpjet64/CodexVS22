Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$errors = 0
Write-Output "=== Repo Hygiene Check ==="

Write-Output -NoEnumerate "Large files (>10MB): "
$large = @()
$tracked = git ls-files
foreach ($f in $tracked) {
  if (-not $f) { continue }
  if ($f -like "vendor/*") { continue }
  if (Test-Path -LiteralPath $f -PathType Leaf) {
    $size = (Get-Item -LiteralPath $f).Length
    if ($size -gt 10485760) {
      $large += ("  {0} ({1}MB)" -f $f, [int]($size / 1MB))
    }
  }
}
if ($large.Count -eq 0) {
  Write-Output "PASS"
} else {
  Write-Output "FAIL"
  $large | ForEach-Object { Write-Output $_ }
  $errors++
}

Write-Output -NoEnumerate "Merge conflict markers: "
$conflicts = git grep -l '<<<<<<< ' -- '*.rs' '*.toml' '*.json' '*.ts' '*.js' '*.py' '*.md' 2>$null
if ($LASTEXITCODE -eq 0 -and $conflicts) {
  Write-Output "FAIL - conflict markers found"
  $errors++
} else {
  Write-Output "PASS"
}

Write-Output -NoEnumerate "Required files: "
$required = @(".gitignore")
$missing = @()
foreach ($f in $required) {
  if (-not (Test-Path -LiteralPath $f)) {
    $missing += $f
  }
}
if ($missing.Count -eq 0) {
  Write-Output "PASS"
} else {
  Write-Output ("FAIL - missing: {0}" -f ($missing -join ", "))
  $errors++
}

if ($errors -gt 0) {
  Write-Output ("=== {0} hygiene issue(s) found ===" -f $errors)
  exit 1
}

Write-Output "=== All hygiene checks passed ==="
