$dir = "$HOME\.local\bin"
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$zip = "$env:TEMP\rimdex-win-x64.zip"

Invoke-WebRequest "https://github.com/realloon/rimdex/releases/latest/download/rimdex-win-x64.zip" -OutFile $zip -UseBasicParsing
Expand-Archive $zip -DestinationPath $dir -Force
Remove-Item $zip -Force

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -split ';' -notcontains $dir) {
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$dir", "User")
}

Write-Host "Installed rimdex to $dir\rimdex.exe"
