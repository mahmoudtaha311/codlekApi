# ==========================================================================
#  Upload a folder to a MonsterASP site with Web Deploy - for the NEW API
#  and the dashboard (each on its own site).
#
#  -PublishProfile  the site's .PublishSettings (downloaded from the host panel).
#            It carries the deploy password: keep it OUTSIDE the repository.
#  -Source   the folder to upload.
#  -SubPath  a folder inside the site, e.g. "app" for the dashboard build.
#  -IncludeWebConfig  upload web.config too. Only for the FIRST deploy of a
#            NEW site (it has none); afterwards the host's web.config carries
#            the secrets and must never be overwritten.
#  -DryRun   ask Web Deploy what WOULD change, without changing anything.
#
#  ASCII only on purpose: Windows PowerShell 5.1 misreads UTF-8 scripts
#  without a BOM, and Arabic would break the parser.
# ==========================================================================
param(
    [Parameter(Mandatory = $true)] [string] $PublishProfile,
    [Parameter(Mandatory = $true)] [string] $Source,
    [string] $SubPath = "",
    [switch] $IncludeWebConfig,
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'

$MsDeploy = 'C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe'
if (-not (Test-Path $MsDeploy)) { throw "msdeploy is not installed: $MsDeploy" }
if (-not (Test-Path $PublishProfile))  { throw "Publish profile not found: $PublishProfile" }
if (-not (Test-Path $Source))   { throw "Source folder not found: $Source" }

$Source = (Resolve-Path $Source).Path
$hasWebConfig = Test-Path (Join-Path $Source 'web.config')

if ($IncludeWebConfig -and -not $hasWebConfig) {
    throw "-IncludeWebConfig was given but $Source has no web.config."
}

[xml]$xml = Get-Content $PublishProfile -Raw
$node = $xml.publishData.publishProfile |
        Where-Object { $_.publishMethod -eq 'MSDeploy' } |
        Select-Object -First 1
if (-not $node) { throw "The profile has no MSDeploy section - download the Web Deploy profile, not FTP." }

# The host address must spell the port out (:8172) and point at msdeploy.axd.
$endpoint = [string]$node.publishUrl
if ($endpoint -notmatch '^https?://') { $endpoint = "https://$endpoint" }
if ($endpoint -notmatch ':\d+(/|$)')  { $endpoint = $endpoint -replace '^(https?://[^/]+)', '${1}:8172' }
if ($endpoint -notmatch 'msdeploy\.axd') { $endpoint = "$endpoint/msdeploy.axd?site=$($node.msdeploySite)" }

$site = [string]$node.msdeploySite

# TEST sites only. This script MIRRORS -Source onto the site: whatever is
# not in the folder gets deleted. Given the production profile by mistake,
# it would wipe the live app. Production goes through deploy-web.ps1
# (DoNotDeleteRule) - see CUTOVER.md. A new test site is added here on purpose.
$TestSites = @('site96162', 'site96163')
if ($TestSites -notcontains $site) {
    throw "deploy-site.ps1 deploys to the TEST sites only ($($TestSites -join ', ')), not '$site'."
}

$user = [string]$node.userName
$pw   = [string]$node.userPWD
if ([string]::IsNullOrWhiteSpace($pw)) { throw "The profile has no password (userPWD)." }

$contentPath = if ($SubPath) { "$site/$($SubPath.Trim('/'))" } else { $site }

Write-Host "Source : $Source"
Write-Host "Site   : $site"
Write-Host "Target : $contentPath"
Write-Host "Files  : $((Get-ChildItem $Source -Recurse -File).Count)"
Write-Host ("web.config: " + $(if ($IncludeWebConfig) { 'UPLOADED' } else { 'skipped' }))
if ($DryRun) { Write-Host "DRY RUN - nothing will change." -ForegroundColor Yellow }

$dest = "contentPath=$contentPath,computerName=$endpoint,userName=$user,password=$pw,authtype=Basic"

$msArgs = @(
    '-verb:sync',
    "-source:contentPath=$Source",
    "-dest:$dest",
    '-enableRule:AppOffline',
    '-retryAttempts:3',
    # The host keeps its HTTPS certificate challenge files in .well-known.
    # A mirror sync would delete them (the first dry run showed exactly that),
    # and the certificate renewal would fail weeks later with no visible cause.
    '-skip:objectName=dirPath,absolutePath=\.well-known'
)

if (-not $IncludeWebConfig) {
    # Neither uploaded nor deleted: the host's copy carries the secrets.
    $msArgs += '-skip:objectName=filePath,absolutePath=web\.config$'
}

if ($DryRun) { $msArgs += '-whatif' }

& $MsDeploy @msArgs
$code = $LASTEXITCODE

$pw = $null
$dest = $null
[GC]::Collect()

if ($code -ne 0) { throw "Web Deploy failed with exit code $code" }

Write-Host ""
Write-Host "Done." -ForegroundColor Green
