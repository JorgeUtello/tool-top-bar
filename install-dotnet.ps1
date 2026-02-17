Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1'
$d = Join-Path $env:USERPROFILE '.dotnet'
if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d | Out-Null }
Write-Host "Installing to: $d"
& .\dotnet-install.ps1 -Channel 7.0 -InstallDir $d
