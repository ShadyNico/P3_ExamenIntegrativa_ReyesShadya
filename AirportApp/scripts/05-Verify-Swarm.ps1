param([string]$StackName = 'airportapp')

$ErrorActionPreference = 'Stop'
docker node ls
docker stack services $StackName
docker stack ps $StackName --no-trunc
docker service ps "${StackName}_airportapp" --no-trunc
docker service ps "${StackName}_postgres" --no-trunc

try {
    $response = Invoke-WebRequest -Uri 'http://localhost:5164/health' -UseBasicParsing -TimeoutSec 15
    Write-Host "HTTP /health = $($response.StatusCode)"
} catch {
    Write-Warning "La aplicación todavía no está saludable: $($_.Exception.Message)"
}
