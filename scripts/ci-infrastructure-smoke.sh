#!/usr/bin/env bash
set -euo pipefail

sql_password="$(openssl rand -hex 24)Aa1!"
jwt_key="$(openssl rand -hex 48)"
resident_password="$(openssl rand -hex 12)Aa1!"
echo "::add-mask::$sql_password"
echo "::add-mask::$jwt_key"
echo "::add-mask::$resident_password"

cleanup() {
  if [[ -n "${api_pid:-}" ]]; then kill "$api_pid" 2>/dev/null || true; fi
  docker rm -f civicflow-ci-sql civicflow-ci-azurite >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run -d --name civicflow-ci-sql -e ACCEPT_EULA=Y -e MSSQL_PID=Developer \
  -e "MSSQL_SA_PASSWORD=$sql_password" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest >/dev/null
docker run -d --name civicflow-ci-azurite -p 10000:10000 \
  mcr.microsoft.com/azure-storage/azurite:3.35.0 azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck >/dev/null

for attempt in {1..60}; do
  if docker exec civicflow-ci-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sql_password" -C -Q "SELECT 1" >/dev/null 2>&1; then break; fi
  if [[ "$attempt" == 60 ]]; then echo "SQL Server did not become ready"; exit 1; fi
  sleep 2
done
docker exec civicflow-ci-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sql_password" -C \
  -Q "IF DB_ID(N'CivicFlowCi') IS NULL CREATE DATABASE [CivicFlowCi]" >/dev/null
for attempt in {1..30}; do
  if curl --silent --output /dev/null http://127.0.0.1:10000/devstoreaccount1; then break; fi
  if [[ "$attempt" == 30 ]]; then echo "Azurite did not become ready"; exit 1; fi
  sleep 1
done

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:5168
export ConnectionStrings__CivicFlowDatabase="Server=localhost,1433;Database=CivicFlowCi;User Id=sa;Password=$sql_password;Encrypt=False;TrustServerCertificate=True"
export Jwt__Key="$jwt_key"
export DemoAccounts__Enabled=false
dotnet run --project src/CivicFlow.Api --configuration Release --no-build >civicflow-ci-api.log 2>&1 &
api_pid=$!

for attempt in {1..60}; do
  if curl --fail --silent http://127.0.0.1:5168/health >/dev/null; then break; fi
  if [[ "$attempt" == 60 ]]; then sed -E 's/(Password|Key)=[^; ]+/\1=[REDACTED]/gi' civicflow-ci-api.log; exit 1; fi
  sleep 2
done

email="ci-$(date +%s)-$RANDOM@example.test"
auth="$(curl --fail --silent -H 'Content-Type: application/json' -d \
  "{\"email\":\"$email\",\"password\":\"$resident_password\",\"firstName\":\"CI\",\"lastName\":\"Resident\"}" \
  http://127.0.0.1:5168/api/auth/register)"
token="$(jq -r .accessToken <<<"$auth")"
category_id="$(curl --fail --silent http://127.0.0.1:5168/api/categories | jq -r '.[0].id')"
case_response="$(curl --fail --silent -H "Authorization: Bearer $token" -H 'Content-Type: application/json' -d \
  "{\"categoryId\":\"$category_id\",\"title\":\"CI attachment smoke\",\"description\":\"Disposable real SQL and Azurite smoke request.\",\"address\":\"100 Example Street\"}" \
  http://127.0.0.1:5168/api/cases)"
case_id="$(jq -r .id <<<"$case_response")"
printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=' | base64 -d > /tmp/civicflow-ci.png
upload="$(curl --fail --silent -H "Authorization: Bearer $token" -H "Idempotency-Key: ci-$RANDOM-$(date +%s)" \
  -F file=@/tmp/civicflow-ci.png\;type=image/png -F visibility=Public \
  "http://127.0.0.1:5168/api/cases/$case_id/attachments")"
attachment_id="$(jq -r .id <<<"$upload")"
curl --fail --silent -H "Authorization: Bearer $token" \
  "http://127.0.0.1:5168/api/cases/$case_id/attachments/$attachment_id/content" >/tmp/civicflow-ci-download.png
test -s /tmp/civicflow-ci-download.png
echo "Real SQL Server and Azurite API upload/download smoke test passed."
