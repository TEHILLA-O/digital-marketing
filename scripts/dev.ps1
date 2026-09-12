$ErrorActionPreference = "Stop"
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet restore
dotnet build
Write-Host "Start infrastructure: docker compose up postgres redis kafka -d"
Write-Host "Start Aspire:         dotnet run --project src/LedgerX.AppHost"
Write-Host "Or start APIs on 5101-5104 and Web on 5100."
