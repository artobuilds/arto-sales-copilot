param([switch]$Shortcut)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $PSScriptRoot '.cache\nuget'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.cache\dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet publish -c Release --no-self-contained -o (Join-Path $PSScriptRoot 'app') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Shortcut) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $target = Join-Path $PSScriptRoot 'app\Arto Sales Copilot.exe'
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw 'Missing executable.' }
    $shortcutPath = Join-Path $desktop 'Arto Sales Copilot.lnk'
    if (Test-Path -LiteralPath $shortcutPath) { throw 'Shortcut already exists; inspect before replacing.' }
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcutPath)
    $link.TargetPath = $target
    $link.WorkingDirectory = Join-Path $PSScriptRoot 'app'
    $link.Description = 'Arto Sales Copilot — sales-first call assistant'
    $link.IconLocation = "$target,0"
    $link.Save()
    Write-Output "Shortcut: $shortcutPath"
}
