$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot

foreach ($command in @("dotnet", "node", "npm", "docker")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command '$command' was not found. Install the prerequisite and run this script again."
    }
}

if (-not (Test-Path ".env")) {
    throw "Create .env from .env.example and replace every REPLACE_WITH value before starting CivicFlow."
}

$settings = @{}
Get-Content ".env" | ForEach-Object {
    if ($_ -match '^\s*([^#][^=]*)=(.*)$') { $settings[$Matches[1].Trim()] = $Matches[2].Trim() }
}
foreach ($required in @("MSSQL_SA_PASSWORD", "CIVICFLOW_JWT_KEY", "CIVICFLOW_DEMO_PASSWORD")) {
    if ([string]::IsNullOrWhiteSpace($settings[$required]) -or $settings[$required] -like "REPLACE_WITH*") {
        throw "Set '$required' to a non-placeholder local value in .env."
    }
}

$composeCommand = $null
docker compose version 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { $composeCommand = "plugin" }
elseif (Get-Command docker-compose -ErrorAction SilentlyContinue) { $composeCommand = "standalone" }
else { throw "Docker Compose was not found. Install Docker Desktop with Compose support." }

if ($composeCommand -eq "plugin") { docker compose up -d }
else { docker-compose up -d }
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed to start SQL Server and Azurite." }

Write-Host "Waiting for SQL Server..." -ForegroundColor Cyan
for ($attempt = 1; $attempt -le 30; $attempt++) {
    $health = docker inspect --format '{{.State.Health.Status}}' civicflow-sqlserver 2>$null
    if ($health -eq "healthy") { break }
    Start-Sleep -Seconds 2
}
if ($health -ne "healthy") { throw "SQL Server did not become healthy. Check Docker Desktop and retry." }

Write-Host "Waiting for Azurite..." -ForegroundColor Cyan
for ($attempt = 1; $attempt -le 30; $attempt++) {
    $blobHealth = docker inspect --format '{{.State.Health.Status}}' civicflow-azurite 2>$null
    if ($blobHealth -eq "healthy") { break }
    Start-Sleep -Seconds 1
}
if ($blobHealth -ne "healthy") { throw "Azurite did not become healthy. Check Docker Desktop and retry." }

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__CivicFlowDatabase = "Server=localhost,1433;Database=CivicFlow;User Id=sa;Password=$($settings.MSSQL_SA_PASSWORD);Encrypt=False;TrustServerCertificate=True"
$env:Jwt__Key = $settings.CIVICFLOW_JWT_KEY
$env:DemoAccounts__Enabled = "true"
$env:DemoAccounts__Password = $settings.CIVICFLOW_DEMO_PASSWORD

$apiCommand = "dotnet run --project '$ProjectRoot\src\CivicFlow.Api'"

$clientCommand = @"
Set-Location '$ProjectRoot\src\CivicFlow.Client'
if (-not (Test-Path 'node_modules')) { npm install }
npm run dev
"@

Start-Process powershell -ArgumentList "-NoExit", "-Command", $apiCommand
Start-Sleep -Seconds 5
Start-Process powershell -ArgumentList "-NoExit", "-Command", $clientCommand
Start-Sleep -Seconds 3
Start-Process "http://localhost:5173"

Write-Host "CivicFlow is starting. Keep both terminal windows open." -ForegroundColor Green
