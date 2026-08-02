$ErrorActionPreference = 'Stop'

# El token se muestra solo en consola y nunca se guarda en el proyecto.
docker swarm join-token worker
