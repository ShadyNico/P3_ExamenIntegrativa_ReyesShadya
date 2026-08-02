# Servicios aeroportuarios y pasarelas de pago

## Métodos disponibles

El selector de pago conserva los métodos de demostración y añade dos
pasarelas externas:

- tarjeta de crédito simulada;
- tarjeta de débito simulada;
- PayPal simulado;
- PayPal Sandbox;
- PayPhone API Links.

PayPal Sandbox crea la orden desde el servidor, muestra el SDK oficial en el
navegador y captura la orden nuevamente desde el backend. PayPhone crea un
enlace HTTPS mediante una solicitud autenticada y redirige al usuario a la
página externa del proveedor. AirportApp no solicita un número celular para
este flujo.

## Seguridad y consistencia

- Los secretos se leen desde .NET User Secrets, variables de entorno o Docker
  Secrets; no se guardan en `appsettings.json`.
- El navegador nunca recibe el secreto de PayPal ni el token de PayPhone.
- AirportApp no almacena números completos de tarjeta ni CVV.
- El total se vuelve a calcular en el servidor y se envía en centavos.
- La respuesta de PayPhone debe contener una URL HTTPS absoluta.
- Si PayPhone devuelve `Link Inválido` al enviar el StoreID opcional, el
  servicio reintenta una vez sin ese campo.
- La capacidad se comprueba nuevamente dentro de una transacción serializable
  antes de confirmar una reserva.
- La orden PayPhone permanece pendiente hasta disponer de una notificación o
  conciliación verificable.
- Los identificadores externos tienen índices únicos para evitar duplicados.

## Configuración local

Desde `AirportApp`, los valores del propietario se configuran sin copiarlos a
la documentación ni al repositorio:

```powershell
dotnet user-secrets set "PayPal:ClientId" "<CLIENT_ID_SANDBOX>"
dotnet user-secrets set "PayPal:ClientSecret" "<CLIENT_SECRET_SANDBOX>"
dotnet user-secrets set "PayPal:BaseUrl" "https://api-m.sandbox.paypal.com"
dotnet user-secrets set "PayPal:CurrencyCode" "USD"

dotnet user-secrets set "PayPhone:Token" "<TOKEN_PAYPHONE>"
dotnet user-secrets set "PayPhone:StoreId" "<STORE_ID_OPCIONAL>"
```

El token debe pertenecer a una aplicación PayPhone habilitada para API Links.
El Client ID, la clave secreta y la contraseña de codificación de PayPhone no
se envían en este flujo.

## Base de datos y ejecución

```powershell
dotnet ef database update --context ApplicationDbContext
dotnet build ..\AirportApp.slnx --configuration Release
dotnet test ..\AirportApp.slnx --configuration Release
dotnet run --launch-profile http
```

En Docker Swarm se esperan los secretos externos descritos en
`SECRETS.example.md`.

## Pruebas

Las pruebas automatizadas comprueban que los métodos externos no soliciten
campos de tarjeta simulada, que PayPhone reciba subtotal, IVA y total en
centavos sin alterar el 15 % de impuesto, y que el enlace devuelto sea HTTPS.
Una prueba completa requiere una cuenta compradora Sandbox de PayPal o una
aplicación de prueba activa de PayPhone.

