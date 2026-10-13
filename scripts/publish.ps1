$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts\Scheduler"

dotnet publish (Join-Path $root "src\Scheduler.App\Scheduler.App.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishTrimmed=false `
    -o $out

Write-Host "Published to $out"
Write-Host "Zip that folder and give Scheduler.exe to users. They do not need the .NET SDK."
