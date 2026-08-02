param([string]$StackName = 'airportapp')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$backupPath = Join-Path $projectRoot 'backups\airportapp.dump'
if (-not (Test-Path -LiteralPath $backupPath)) {
    throw 'Falta backups/airportapp.dump. Ejecute primero 11-Backup-HostPostgres.ps1.'
}

$containerId = docker ps `
    --filter "label=com.docker.swarm.service.name=${StackName}_postgres" `
    --format '{{.ID}}' | Select-Object -First 1
if (-not $containerId) {
    throw 'La réplica PostgreSQL no está en este nodo o todavía no está Running.'
}

docker cp $backupPath "${containerId}:/tmp/airportapp.dump"
docker exec $containerId pg_restore `
    --username airportapp --dbname airportapp --exit-on-error `
    --clean --if-exists /tmp/airportapp.dump
Write-Host 'AirportDB y el esquema app fueron restaurados.'
