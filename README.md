# P3 Examen Integrativa — AirportApp

Proyecto individual del examen integrativo, Tipo 5: **servicios aeroportuarios**.
La solución permite consultar información aeroportuaria, reservar servicios,
crear órdenes y procesar pagos de prueba mediante PayPal Sandbox o enlaces de
PayPhone.

## Contenido del repositorio

- `AirportApp/`: aplicación ASP.NET Core MVC con Identity, Entity Framework
  Core, Npgsql, pagos, servicios aeroportuarios y Docker.
- `AirportApp.Tests/`: suite automatizada de 38 pruebas.
- `airport-db/`: conjunto completo de AirportDB, incluidos los 39 fragmentos
  `.tsv.zst`, scripts de importación y verificaciones. El inventario registra
  59.502.421 filas en las 14 tablas originales.
- `AirportApp.slnx`: solución .NET 10.
- `P3ExamenReyesShadya_InformeTecnico_APA7.docx`: informe técnico con las
  evidencias y los comandos del proyecto.

## Tecnologías

- ASP.NET Core MVC y .NET 10
- Entity Framework Core y Npgsql
- PostgreSQL
- ASP.NET Core Identity, Google OAuth, SMTP y autenticación de dos factores
- PayPal Sandbox Orders API
- PayPhone API Links
- Docker Compose y Docker Swarm
- Ollama con `llama3.2:1b`, opcional

## Preparación rápida

La documentación detallada está en [`AirportApp/README.md`](AirportApp/README.md)
y los marcadores de configuración se encuentran en
[`AirportApp/.env.example`](AirportApp/.env.example) y
[`AirportApp/SECRETS.example.md`](AirportApp/SECRETS.example.md).

```powershell
dotnet restore .\AirportApp.slnx
dotnet build .\AirportApp.slnx --configuration Release
dotnet test .\AirportApp.slnx --configuration Release
cd .\AirportApp
dotnet ef database update --context ApplicationDbContext
dotnet run --launch-profile http
```

La aplicación se abre en `http://localhost:5164`. Las cadenas de conexión y
credenciales de proveedores deben configurarse mediante .NET User Secrets,
variables de entorno o Docker Secrets. El repositorio no contiene secretos
reales.

## Resultado de pruebas

La última ejecución local obtuvo **38 pruebas aprobadas, 0 fallidas y 0
omitidas**.

