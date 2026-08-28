$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot

foreach ($command in @("dotnet", "node", "npm", "docker")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command '$command' was not found. Install the prerequisite and run this script again."
    }
}

$databasePassword = "REDACTED_HISTORICAL_DEVELOPMENT_SECRET"
if (-not (Test-Path ".env")) {
    "MSSQL_SA_PASSWORD=$databasePassword" | Set-Content ".env"
} else {
    $passwordLine = Get-Content ".env" | Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' } | Select-Object -First 1
    if ($passwordLine) { $databasePassword = $passwordLine.Substring("MSSQL_SA_PASSWORD=".Length) }
}

docker compose up -d

Write-Host "Waiting for SQL Server..." -ForegroundColor Cyan
for ($attempt = 1; $attempt -le 30; $attempt++) {
    $health = docker inspect --format '{{.State.Health.Status}}' civicflow-sqlserver 2>$null
    if ($health -eq "healthy") { break }
    Start-Sleep -Seconds 2
}
if ($health -ne "healthy") { throw "SQL Server did not become healthy. Check Docker Desktop and retry." }

$apiCommand = @"
`$env:ASPNETCORE_ENVIRONMENT='Development'
`$env:ConnectionStrings__CivicFlowDatabase='Server=localhost,1433;Database=CivicFlow;User Id=sa;Password=$databasePassword;TrustServerCertificate=True'
dotnet run --project '$ProjectRoot\src\CivicFlow.Api'
"@

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
