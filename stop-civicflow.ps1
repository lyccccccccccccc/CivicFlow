$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot
docker compose down
Write-Host "CivicFlow SQL Server and Azurite containers stopped. Close the API and client terminal windows if they are still open."
