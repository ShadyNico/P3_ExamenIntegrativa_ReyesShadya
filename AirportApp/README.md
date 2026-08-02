# AirportApp

> El módulo completo de reserva y pago simulado de servicios aeroportuarios se
> documenta en
> [`DOCUMENTACION_SERVICIOS_AEROPORTUARIOS.md`](DOCUMENTACION_SERVICIOS_AEROPORTUARIOS.md).

Aplicación ASP.NET Core MVC sobre PostgreSQL que adapta la estructura funcional
de SakilaApp al dominio aeroportuario. Usa dos contextos aislados:

- `DomainDbContext`: las 14 tablas originales del esquema `airportdb`.
- `ApplicationDbContext`: Identity, carrito, órdenes, inventario y pagos en el
  esquema `app`.

Las migraciones de la aplicación nunca eliminan ni modifican las tablas de
`airportdb`.

## Datos completos

La carpeta hermana `../airport-db` conserva el volcado original y los 39
fragmentos `.tsv.zst`. El importador validado carga exactamente 59.502.421
filas. No se descartaron datos; las tablas `airport_reachable` y `flight_log`
se conservan aunque el origen contiene cero filas.

Consulte [los scripts PostgreSQL](../airport-db/postgresql) y
[el mapeo de entidades](Data/DomainDbContext.cs).

## Requisitos

- .NET SDK 10.
- PostgreSQL 12 o posterior; la validación de la carga se realizó con 18.3.
- Node.js 22.15 o posterior y `psql` para importar desde el host.
- Docker con Compose, si se usa el flujo contenedorizado.
- Ollama opcional para el asistente local.

La carga completa necesita aproximadamente 7 GB para tablas e índices, además
de espacio temporal y los 657 MB de origen comprimido.

## Inicio rápido con Docker Compose

Desde `AirportApp`:

```powershell
Copy-Item .env.example .env
# Edite .env y reemplace todos los marcadores necesarios.
docker compose up --build
```

`airportdb-import` espera a PostgreSQL, transmite los 39 archivos, ejecuta
claves e índices y verifica todos los conteos. Solo después inicia la
aplicación en `http://localhost:5164`. La primera carga puede tardar bastante;
las siguientes se omiten mediante `public.airportdb_import_state`.

Para observar la carga:

```powershell
docker compose logs -f airportdb-import
```

## Ejecución local

1. Cree una base UTF-8 y cargue AirportDB:

   ```powershell
   $env:PGHOST='localhost'
   $env:PGPORT='5432'
   $env:PGDATABASE='airportapp'
   $env:PGUSER='airportapp'
   $env:PGPASSWORD='su_clave_local'
   node ..\airport-db\postgresql\import.mjs --source ..\airport-db --reset
   ```

2. Configure secretos fuera de `appsettings.json`:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:DomainConnection" "Host=localhost;Port=5432;Database=airportapp;Username=airportapp;Password=..."
   dotnet user-secrets set "ConnectionStrings:ApplicationConnection" "Host=localhost;Port=5432;Database=airportapp;Username=airportapp;Password=..."
   ```

3. Aplique la migración de `app` explícitamente y ejecute:

   ```powershell
   dotnet ef database update --context ApplicationDbContext
   dotnet run
   ```

El arranque también ejecuta `MigrateAsync` de forma idempotente para el
contexto de aplicación. Nunca genera migraciones para el contexto de dominio.

## Autenticación

Identity exige cuenta confirmada, bloqueo por intentos fallidos y contraseña
con mayúscula, minúscula y dígito. La interfaz incluida permite confirmación de
correo, recuperación, autenticación de dos factores y códigos de recuperación.
El enlace de confirmación no se muestra en pantalla.

Google OAuth se habilita únicamente si existen
`Authentication:Google:ClientId` y `ClientSecret`. Configure en Google el URI
de retorno `/signin-google`.

Los roles son `Administrador`, `Supervisor` y `Consulta`. El seeder siempre es
idempotente para roles. Usuarios de demostración solo se crean cuando
`SeedUsers:Enabled=true` y sus credenciales se proporcionan por configuración.

## Comercio y pagos

La tienda crea un catálogo local de 50 vuelos reales recientes, con precio
promedio de sus reservas cuando existe y stock administrable. El carrito y las
órdenes pertenecen al identificador inmutable del usuario.

PayPhone y PayPal requieren credenciales de prueba externas. Se admiten el
flujo PayPal por redirección y el botón JavaScript Sandbox. Las respuestas
crudas de las pasarelas no se guardan ni se muestran. La confirmación manual
está deshabilitada; solo puede habilitarse deliberadamente con
`Payments:AllowManualConfirmation=true` y rol Administrador.

Las pruebas reales de Sandbox requieren cuentas y credenciales del propietario;
la suite automatizada prueba cálculos y respuestas adversas sin efectuar
cargos.

El flujo de servicios aeroportuarios conserva la tarjeta y el PayPal simulados
y agrega dos opciones de pasarela: PayPal Sandbox mediante Orders API y
PayPhone mediante `API Links` y redirección HTTPS. PayPal solo confirma la
reserva después de capturar y validar la orden en el servidor, comprobar el
monto y volver a comprobar la capacidad disponible. El enlace PayPhone deja la
orden pendiente hasta contar con notificación o conciliación verificable. Consulte
[`DOCUMENTACION_SERVICIOS_AEROPORTUARIOS.md`](DOCUMENTACION_SERVICIOS_AEROPORTUARIOS.md).

## Ollama

Valores predeterminados:

```text
Ollama:BaseUrl=http://localhost:11434
Ollama:Model=llama3.2:1b
Ollama:TimeoutSeconds=120
```

El endpoint `POST /api/ia/generar` limita la consulta a 1000 caracteres, aplica
20 solicitudes por minuto y normaliza errores de timeout, HTTP, JSON y
respuesta vacía. La colección de Postman está en
`postman/AirportApp.postman_collection.json`.

## Compilación y pruebas

```powershell
dotnet build ..\AirportApp.slnx
dotnet test ..\AirportApp.slnx
dotnet list package --vulnerable --include-transitive
```

Las pruebas cubren cálculos comerciales, cantidades y redondeo, mapeo de las
14 tablas, clave compuesta de clima, aislamiento del esquema `app`,
concurrencia optimista y respuestas de Ollama.

## Migraciones

La migración inicial está en `Data/MigrationsIdentity` y el SQL idempotente en
`Data/MigrationsIdentity/application_schema.sql`. Para otra base limpia:

```powershell
dotnet ef database update --context ApplicationDbContext
```

No ejecute `dotnet ef migrations add` contra `DomainDbContext`: ese esquema se
gestiona con los scripts de `../airport-db/postgresql`.

## Docker Swarm

1. Construya `airportapp:latest` y distribúyala o publíquela en un registro.
2. Cree los secretos descritos en `SECRETS.example.md`.
3. Cree `airportapp_shared_keys` con un driver realmente compartido (por
   ejemplo, NFS). Un volumen local con el mismo nombre en cada nodo no sirve.
4. Proporcione un PFX para cifrar las claves de Data Protection.
5. Etiquete el nodo persistente o deje que `04-Deploy-SwarmStack.ps1` lo haga.
6. Restaure un dump completo antes de validar el servicio.

Los scripts `01` a `12` inicializan, despliegan, verifican, escalan, drenan,
respaldan y restauran. `pg_restore` usa `--clean --if-exists`; por tanto la
restauración reemplaza los objetos presentes y debe ejecutarse durante una
ventana controlada.

## Healthcheck

`GET /health` devuelve 200 solo si ambos contextos alcanzan PostgreSQL; en caso
contrario devuelve 503 sin exponer cadenas de conexión.
