$ErrorActionPreference = 'Stop'

function Main {
    # Ensure TLS 1.2 is enabled for older PowerShell versions
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12

    # Check 64-bit OS
    if (-not [System.Environment]::Is64BitOperatingSystem) {
        Write-Error "error: 32-bit Windows is not supported. rimdex requires 64-bit Windows."
        return
    }

    $asset = "rimdex-win-x64.zip"
    $url = "https://github.com/realloon/rimdex/releases/latest/download/$asset"
    $installDir = [System.IO.Path]::Combine($env:USERPROFILE, ".local", "bin")

    # Create temporary directory
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    $zipPath = Join-Path $tempDir $asset

    try {
        Write-Host "Downloading $asset..."
        $retries = 3
        $count = 0
        $downloaded = $false

        while ($count -lt $retries) {
            try {
                Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
                $downloaded = $true
                break
            }
            catch {
                $count++
                if ($count -lt $retries) {
                    Write-Host "Download failed, retrying ($count/$retries)..."
                    Start-Sleep -Seconds 1
                }
            }
        }

        if (-not $downloaded) {
            Write-Error "error: failed to download from $url"
            return
        }

        # Extract archive
        Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force

        $sourceExe = Join-Path $tempDir "rimdex.exe"
        if (-not (Test-Path $sourceExe)) {
            Write-Error "error: 'rimdex.exe' not found in downloaded archive."
            return
        }

        # Install binary
        if (-not (Test-Path $installDir)) {
            New-Item -ItemType Directory -Path $installDir -Force | Out-Null
        }

        $targetExe = Join-Path $installDir "rimdex.exe"
        Move-Item -Path $sourceExe -Destination $targetExe -Force
        Write-Host "Installed rimdex to $targetExe"

        # Configure User PATH if not already present
        $userPath = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
        $userPaths = if ($userPath) { $userPath -split ';' } else { @() }

        $alreadyInUserPath = $false
        foreach ($p in $userPaths) {
            if ($p.TrimEnd('\') -ieq $installDir.TrimEnd('\')) {
                $alreadyInUserPath = $true
                break
            }
        }

        if (-not $alreadyInUserPath) {
            $newUserPath = ($userPaths + $installDir) -join ';'
            [System.Environment]::SetEnvironmentVariable("Path", $newUserPath, [System.EnvironmentVariableTarget]::User)
            Write-Host "Added $installDir to User PATH"
        }

        # Update current session PATH so rimdex is immediately available
        $procPath = $env:Path
        $procPaths = if ($procPath) { $procPath -split ';' } else { @() }
        $alreadyInProcPath = $false
        foreach ($p in $procPaths) {
            if ($p.TrimEnd('\') -ieq $installDir.TrimEnd('\')) {
                $alreadyInProcPath = $true
                break
            }
        }

        if (-not $alreadyInProcPath) {
            $env:Path = "$installDir;$env:Path"
        }

        Write-Host "Done."
    }
    finally {
        if (Test-Path $tempDir) {
            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Main
