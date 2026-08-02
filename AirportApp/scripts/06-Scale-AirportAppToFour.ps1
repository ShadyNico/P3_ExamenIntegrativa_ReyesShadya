param([string]$StackName = 'airportapp')

$ErrorActionPreference = 'Stop'
docker service scale "${StackName}_airportapp=4"
docker service ps "${StackName}_airportapp"
