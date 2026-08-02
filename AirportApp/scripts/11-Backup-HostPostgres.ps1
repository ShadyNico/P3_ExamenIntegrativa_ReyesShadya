param(
    [string]$HostName = 'host.docker.internal',
    [int]$Port = 5433,
    [string]$Database = 'airportapp',
    [string]$Username = 'airportapp'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $projectRoot '.env'
$backupDirectory = Join-Path $projectRoot 'backups'
$backupPath = Join-Path $backupDirectory 'airportapp.dump'
if (-not (Test-Path -LiteralPath $envFile)) {
    throw 'Falta .env.'
}

$passwordLine = Get-Content -LiteralPath $envFile |
    Where-Object { $_ -match '^POSTGRES_PASSWORD=' } |
    Select-Object -First 1
if (-not $passwordLine) {
    throw 'Falta POSTGRES_PASSWORD en .env.'
}

$databasePassword = $passwordLine.Substring($passwordLine.IndexOf('=') + 1).Trim().Trim("'")
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
$env:PGPASSWORD = $databasePassword
try {
    docker run --rm --env PGPASSWORD `
        --mount "type=bind,source=$backupDirectory,target=/backup" `
        postgres:18-alpine `
        pg_dump --host $HostName --port $Port --username $Username `
        --dbname $Database --format custom --file /backup/airportapp.dump
    if ($LASTEXITCODE -ne 0) {
        throw 'El respaldo falló.'
    }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Variable databasePassword -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $backupPath)) {
    throw 'No se generó el respaldo.'
}
Write-Host "Respaldo lógico creado en $backupPath."
