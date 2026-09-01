$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot
docker compose version 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { docker compose down }
elseif (Get-Command docker-compose -ErrorAction SilentlyContinue) { docker-compose down }
else { throw "Docker Compose was not found." }
Write-Host "CivicFlow SQL Server and Azurite containers stopped. Close the API and client terminal windows if they are still open."
