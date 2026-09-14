<#
.SYNOPSIS
  One-time (and re-runnable) local setup for the LearnCloud API.

.DESCRIPTION
  1. Generates a database password and a JWT signing key, once, and stores them in
     dotnet user-secrets for src/LearnCloud.Api. Nothing secret is written to the repo.
  2. Starts PostgreSQL in Docker (deployment/docker-compose.dev.yml).
  3. Applies EF Core migrations with the API's --migrate mode.

  Re-running reuses the stored secrets, so the existing database volume keeps working.
  Requires Docker Desktop and the .NET SDK. Works in Windows PowerShell 5.1 and pwsh.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $root 'src\LearnCloud.Api\LearnCloud.Api.csproj'
$compose = Join-Path $root 'deployment\docker-compose.dev.yml'

# Native tools (docker, dotnet) write progress to stderr. Under 'Stop', Windows PowerShell
# 5.1 turns that into a terminating error, so native calls run under 'Continue' and are
# judged by their exit code instead.
function Invoke-Native([scriptblock]$command, [string]$failure) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $command 2>&1 | ForEach-Object { "$_" } } finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { throw $failure }
}

function New-RandomToken([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    # URL-safe, no quotes or semicolons, so it is safe inside a connection string.
    return [Convert]::ToBase64String($buffer).TrimEnd('=').Replace('+', 'A').Replace('/', 'B')
}

Write-Host 'Reading existing user-secrets...'
$existing = @{}
dotnet user-secrets list --project $apiProject 2>$null | ForEach-Object {
    if ($_ -match '^\s*([^=]+?)\s*=\s*(.*)$') { $existing[$Matches[1]] = $Matches[2] }
}

$dbPort = 55432   # must match deployment/docker-compose.dev.yml
$dbPassword = $null
if ($existing.ContainsKey('ConnectionStrings:Default') -and $existing['ConnectionStrings:Default'] -match 'Password=([^;]+)') {
    $dbPassword = $Matches[1]
    Write-Host 'Reusing stored database password.'
} else {
    $dbPassword = New-RandomToken 24
    Write-Host 'Generated a database password.'
}
# Always rewritten so a port change reaches existing setups.
$conn = "Host=127.0.0.1;Port=$dbPort;Database=learncloud;Username=learncloud;Password=$dbPassword"
dotnet user-secrets set 'ConnectionStrings:Default' $conn --project $apiProject | Out-Null

if (-not $existing.ContainsKey('Jwt:Secret')) {
    dotnet user-secrets set 'Jwt:Secret' (New-RandomToken 48) --project $apiProject | Out-Null
    Write-Host 'Generated and stored a JWT signing key.'
} else {
    Write-Host 'Reusing stored JWT signing key.'
}

Write-Host 'Starting PostgreSQL...'
$env:LEARNCLOUD_DEV_DB_PASSWORD = $dbPassword
Invoke-Native { docker compose -f $compose up -d --wait } 'docker compose failed. Is Docker Desktop running?'

Write-Host 'Applying migrations...'
Invoke-Native { dotnet run --project $apiProject -- --migrate } 'Migration failed.'

Write-Host ''
Write-Host 'Ready. Start the API with:'
Write-Host '  dotnet run --project src/LearnCloud.Api'
Write-Host 'Swagger: http://localhost:5080/swagger   Health: http://localhost:5080/health/ready'
Write-Host "Database: 127.0.0.1:$dbPort (container learncloud-dev-db)"
