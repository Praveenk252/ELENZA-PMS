---
name: backup
description: "Backup the entire ElenzaIndia site from FTP server to a local timestamped folder and publish to git. Use when the user wants to backup the site, save a snapshot, or download the live server files."
---

# Backup Site

Download the full site from the live FTP server, save locally, and publish to git.

## MANDATORY: Run via a subagent

- **NEVER run the backup in the main agent session.** The FTP download can take several minutes, produces large output, and may be interrupted by timeouts or user aborts that leave the repo in a half-committed state.
- Always delegate the entire backup to the `general` subagent type via the Task tool.
- Construct the Task prompt by copying the **Steps** and **Script** sections below verbatim into the subagent prompt, plus:
  - The absolute path to this skill file: `C:\Users\Praveen\Documents\Codex\2026-05-28\requirement-specification-elenzaindia-com-production-management\.agents\skills\backup\SKILL.md`
  - The project directory: `C:\Users\Praveen\Documents\Codex\2026-05-28\requirement-specification-elenzaindia-com-production-management`
  - The instruction to save the script to the pre-approved temp dir `C:\Users\Praveen\AppData\Local\Temp\opencode\backup.ps1`, override `$projectDir` with the project directory, and run it with a long timeout (>= 600000 ms).
  - Tell the subagent to report back: total files downloaded, failures, zip size/location, git commit hash, and confirmation that the push to origin/master succeeded.

## Steps

1. **Create timestamped backup folder**: Generate a timestamp and create `backup/site-ftp-YYYYMMDD-HHMMSS/`

2. **Download all files recursively** from FTP using PowerShell:
   - FTP Host: `win8036.site4now.net`
   - FTP User: `elerp1-001`
   - FTP Pass: `Swishcat1@`
   - FTP Root: `/site1/`
   - Use `FtpWebRequest` with `ListDirectoryDetails` to list directories
   - Detect directories via `<DIR>` in listing output
   - URL-encode filenames with `EscapeDataString()` before downloading
   - Use `WebClient.DownloadFile()` for each file

3. **Create zip archive**: Compress the backup folder into `backup/site-ftp-YYYYMMDD-HHMMSS.zip`

4. **Publish to git**: Commit and push the backup to the remote repository
   - Stage the backup folder and zip
   - Commit with message `backup: site snapshot YYYYMMDD-HHMMSS`
   - Push to origin master

5. **Report**: List total files downloaded, any failures, and git commit hash

## Script

Save and run this PowerShell script via `powershell -ExecutionPolicy Bypass -File <path>`:

```powershell
$ErrorActionPreference = 'Continue'
$ftpHost = "win8036.site4now.net"
$ftpUser = "elerp1-001"
$ftpPass = "Swishcat1@"
$rootPath = "/site1/"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$projectDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$localRoot = Join-Path $projectDir "backup/site-ftp-$timestamp"
$totalFiles = 0
$failures = @()

New-Item -ItemType Directory -Force -Path $localRoot | Out-Null
Write-Host "Backup root: $localRoot"

function Get-Listing($ftpPath) {
    $url = "ftp://$ftpHost$rootPath$ftpPath"
    $req = [System.Net.FtpWebRequest]::Create($url)
    $req.Credentials = New-Object System.Net.NetworkCredential($ftpUser, $ftpPass)
    $req.Method = [System.Net.WebRequestMethods+Ftp]::ListDirectoryDetails
    $req.UsePassive = $true
    $req.Timeout = 30000
    try {
        $resp = $req.GetResponse()
        $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $data = $sr.ReadToEnd()
        $sr.Close()
        $resp.Close()
        return $data
    } catch {
        Write-Host "  LIST FAILED: $url"
        return ""
    }
}

function Download-Dir($ftpPath, $localDir) {
    New-Item -ItemType Directory -Force -Path $localDir | Out-Null
    $listing = Get-Listing $ftpPath
    $lines = $listing.Split("`n") | Where-Object { $_.Trim() -ne "" }
    foreach ($line in $lines) {
        $line = $line.Trim()
        if ($line -eq "" -or $line -match "^total") { continue }
        $isDir = $line -match "<DIR>"
        $parts = $line -split "\s+"
        $name = $parts[-1]
        if ($name -eq "." -or $name -eq "..") { continue }
        if ($isDir) {
            Write-Host "DIR:  $ftpPath$name/"
            Download-Dir "$ftpPath$name/" (Join-Path $localDir $name)
        } else {
            $encodedName = [System.Uri]::EscapeDataString($name)
            $fileUrl = "ftp://$ftpHost$rootPath$ftpPath$encodedName"
            $localFile = Join-Path $localDir $name
            Write-Host "FILE: $ftpPath$name"
            try {
                $wc = New-Object System.Net.WebClient
                $wc.Credentials = New-Object System.Net.NetworkCredential($ftpUser, $ftpPass)
                $wc.DownloadFile($fileUrl, $localFile)
                $script:totalFiles++
            } catch {
                $errMsg = $_.Exception.Message
                Write-Host "  FAILED: $errMsg"
                $script:failures += "$ftpPath$name ($errMsg)"
            }
        }
    }
}

# Step 1-2: Download from FTP
Download-Dir "" $localRoot

# Step 3: Create zip
$zipPath = Join-Path $projectDir "backup/site-ftp-$timestamp.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$localRoot/*" -DestinationPath $zipPath -Force
Write-Host "`nZIP: $zipPath ($([math]::Round((Get-Item $zipPath).Length/1MB, 1)) MB)"

# Step 4: Publish to git
Write-Host "`n--- GIT PUBLISH ---"
git -C $projectDir add -f "backup/site-ftp-$timestamp/" $zipPath 2>&1
$commitMsg = "backup: site snapshot $timestamp"
git -C $projectDir commit -m $commitMsg 2>&1
$commitHash = git -C $projectDir log --oneline -1 2>&1
Write-Host "Committed: $commitHash"
git -C $projectDir push origin master 2>&1
Write-Host "Pushed to origin/master"

# Step 5: Report
Write-Host "`n--- SUMMARY ---"
Write-Host "Total files downloaded: $totalFiles"
Write-Host "Failures: $($failures.Count)"
if ($failures.Count -gt 0) { $failures | ForEach-Object { Write-Host "  FAIL: $_" } }
Write-Host "Backup: $localRoot"
Write-Host "Zip: $zipPath"
Write-Host "Git: $commitHash"
```

## Notes

- Some `.xlsx` files may fail to download (550 error) — these are non-critical
- The `App_Data/elenza_pms.accdb` file is the live database — always included
- Backups are saved in `backup/` directory in the project root
- Git commits are pushed to `origin/master` automatically
- The `.gitignore` should exclude `backup/site-ftp-*.zip` if zips are too large for git
