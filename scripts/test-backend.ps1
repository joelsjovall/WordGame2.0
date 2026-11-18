param(
    [ValidateSet("All", "Unit", "Integration")]
    [string]$Suite = "All"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "backend/WordGame.sln"

dotnet build $solution --configuration Release --warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$targets = switch ($Suite) {
    "All" { $solution }
    "Unit" {
        Join-Path $repoRoot "backend/WordGame.Domain.Tests/WordGame.Domain.Tests.csproj"
        Join-Path $repoRoot "backend/WordGame.Application.Tests/WordGame.Application.Tests.csproj"
        Join-Path $repoRoot "backend/WordGame.Infrastructure.Tests/WordGame.Infrastructure.Tests.csproj"
    }
    "Integration" { Join-Path $repoRoot "backend/WordGame.Api.Tests/WordGame.Api.Tests.csproj" }
}

foreach ($target in $targets) {
    dotnet test $target --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
