param(
    [Parameter(Mandatory = $true)]
    [string]$WorkerName
)

$ErrorActionPreference = 'Stop'
docker node update --availability active $WorkerName
docker node ls
