param(
    [string]$Image = 'airportapp:latest',
    [string]$OutputPath = (Join-Path $PWD 'airportapp.tar')
)

$ErrorActionPreference = 'Stop'
docker image inspect $Image | Out-Null
docker image save --output $OutputPath $Image
Write-Host "Imagen exportada en: $OutputPath"
Write-Host "En cada worker ejecute: docker image load --input `"$OutputPath`""
