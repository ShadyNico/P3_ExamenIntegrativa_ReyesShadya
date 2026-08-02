param(
    [Parameter(Mandatory = $true)]
    [string]$AdvertiseAddress
)

$ErrorActionPreference = 'Stop'
$state = docker info --format '{{.Swarm.LocalNodeState}}'

if ($state -eq 'inactive') {
    docker swarm init --advertise-addr $AdvertiseAddress
} elseif ($state -ne 'active') {
    throw "Estado Swarm no esperado: $state"
}

docker node ls
