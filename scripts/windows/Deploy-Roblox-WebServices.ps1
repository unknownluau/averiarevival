param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot,
    [Parameter(Mandatory = $true)]
    [string]$DeployRoot,
    [string]$ApiProxyServiceName = "Roblox.ApiProxy",
    [string]$WebsiteServiceName = "Roblox.Website",
    [string]$DataServiceName = "Roblox.Services.Data",
    [string]$DataStoreServiceName = "Roblox.Services.DataStore",
    [string]$ApiServiceName = "Roblox.Services.Api",
    [string]$DonationServiceName = "Roblox.Services.Donation",
    [string]$AvatarServiceName = "Roblox.Services.Avatar",
    [string]$ThumbnailsServiceName = "Roblox.Services.Thumbnails",
    [string]$UsersServiceName = "Roblox.Services.Users",
    [string]$AdminServiceName = "Roblox.Services.Admin",
    [string]$GamesServiceName = "Roblox.Services.Games"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Section {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message =="
}

function Resolve-ServiceEntry {
    param([string]$Name)

    $service = Get-Service | Where-Object { $_.Name -ieq $Name -or $_.DisplayName -ieq $Name } | Select-Object -First 1
    if ($null -ne $service) {
        return $service
    }

    $closeMatches = Get-Service | Where-Object {
        $_.Name -like "*$Name*" -or $_.DisplayName -like "*$Name*"
    } | Select-Object -First 5

    if ($closeMatches.Count -gt 0) {
        $matchSummary = $closeMatches | ForEach-Object { "'$($_.Name)' (DisplayName: '$($_.DisplayName)')" }
        Write-Warning "Service '$Name' was not found. Close matches: $($matchSummary -join ', ')"
    }
    else {
        Write-Warning "Service '$Name' was not found. No close matches were found."
    }

    return $null
}

function Stop-ServiceIfExists {
    param([string]$Name)

    $service = Resolve-ServiceEntry -Name $Name
    if ($null -eq $service) {
        return
    }

    if ($service.Status -ne "Stopped") {
        Write-Host "Stopping service '$($service.Name)'..."
        Stop-Service -Name $service.Name -Force
        $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(45))
    }
}

function Start-ServiceIfExists {
    param([string]$Name)

    $service = Resolve-ServiceEntry -Name $Name
    if ($null -eq $service) {
        return
    }

    Write-Host "Starting service '$($service.Name)'..."
    Start-Service -Name $service.Name
    $service.WaitForStatus("Running", [TimeSpan]::FromSeconds(45))
}

function Sync-Directory {
    param(
        [string]$From,
        [string]$To
    )

    if (-not (Test-Path -LiteralPath $From)) {
        throw "Source directory '$From' does not exist."
    }

    New-Item -ItemType Directory -Force -Path $To | Out-Null

    $probeFile = Join-Path $To (".deploy-write-test-" + [System.Guid]::NewGuid().ToString("N") + ".tmp")
    try {
        Set-Content -LiteralPath $probeFile -Value "ok" -Encoding ascii
        Remove-Item -LiteralPath $probeFile -Force
    }
    catch {
        throw "Write access check failed for '$To'. Ensure the deploy user can modify this folder and its files. $($_.Exception.Message)"
    }

    $logPath = Join-Path ([System.IO.Path]::GetTempPath()) ("robocopy-" + [System.Guid]::NewGuid().ToString("N") + ".log")
    $robocopyArgs = @(
        $From
        $To
        "/MIR"
        "/R:2"
        "/W:2"
        "/NFL"
        "/NDL"
        "/NP"
        "/XF"
        "appsettings*.json"
        "game-servers.json"
    )

    & robocopy @robocopyArgs *> $logPath
    $exitCode = $LASTEXITCODE
    if ($exitCode -gt 7) {
        $log = Get-Content -LiteralPath $logPath -Raw
        throw "robocopy failed from '$From' to '$To' with exit code $exitCode.`n$log"
    }

    Write-Host "robocopy exit code: $exitCode"
    Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
}

Write-Section "Validating paths"
$SourceRoot = [System.IO.Path]::GetFullPath($SourceRoot)
$DeployRoot = [System.IO.Path]::GetFullPath($DeployRoot)
Write-Host "SourceRoot: $SourceRoot"
Write-Host "DeployRoot: $DeployRoot"

$services = @(
    @{ Name = $ApiProxyServiceName; Folder = "Roblox.ApiProxy" }
    @{ Name = $WebsiteServiceName; Folder = "Roblox.Website" }
    @{ Name = $DataServiceName; Folder = "Roblox.Services.Data" }
    @{ Name = $DataStoreServiceName; Folder = "Roblox.Services.DataStore" }
    @{ Name = $ApiServiceName; Folder = "Roblox.Services.Api" }
    @{ Name = $DonationServiceName; Folder = "Roblox.Services.Donation" }
    @{ Name = $AvatarServiceName; Folder = "Roblox.Services.Avatar" }
    @{ Name = $ThumbnailsServiceName; Folder = "Roblox.Services.Thumbnails" }
    @{ Name = $UsersServiceName; Folder = "Roblox.Services.Users" }
    @{ Name = $AdminServiceName; Folder = "Roblox.Services.Admin" }
    @{ Name = $GamesServiceName; Folder = "Roblox.Services.Games" }
)

foreach ($entry in $services) {
    Write-Section "Deploying $($entry.Name)"
    $from = Join-Path $SourceRoot $entry.Folder
    $to = Join-Path $DeployRoot $entry.Folder
    try {
        Stop-ServiceIfExists -Name $entry.Name
        Write-Host "Syncing '$from' -> '$to'"
        Sync-Directory -From $from -To $to
        Start-ServiceIfExists -Name $entry.Name
    }
    catch {
        throw "Failed while deploying service '$($entry.Name)' from '$from' to '$to'. $($_.Exception.Message)"
    }
}

Write-Section "Cleanup"
if (Test-Path -LiteralPath $SourceRoot) {
    Remove-Item -LiteralPath $SourceRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Deployment complete."
