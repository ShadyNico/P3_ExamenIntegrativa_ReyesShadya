param(
    [Parameter(Mandatory = $true)]
    [string]$WorkerName,
    [string]$StackName = 'airportapp'
)

$ErrorActionPreference = 'Stop'
$role = docker node inspect $WorkerName --format '{{.Spec.Role}}'
$postgresNode = docker service ps "${StackName}_postgres" --filter desired-state=running --format '{{.Node}}'
if ($role -ne 'worker') {
    throw "$WorkerName no es un worker."
}
if ($postgresNode -contains $WorkerName) {
    throw 'No se drenará el nodo que conserva PostgreSQL.'
}

docker node update --availability drain $WorkerName
docker node ls
docker service ps "${StackName}_airportapp"
