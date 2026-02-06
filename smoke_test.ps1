$ErrorActionPreference = "Stop"

Write-Host "Starting Smoke Test..."

# 1. Build Solution (from root, assumes .sln exists or dotnet build finds projects)
Write-Host "Building Solution..."
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { Write-Error "Build Failed"; exit 1 }

# 2. Run Unit Tests
Write-Host "Running Tests..."
dotnet test
if ($LASTEXITCODE -ne 0) { Write-Error "Tests Failed"; exit 1 }

Write-Host "Smoke Test Passed!"
