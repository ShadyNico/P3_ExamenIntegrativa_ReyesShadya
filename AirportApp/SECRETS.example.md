# Inventario de secretos de AirportApp

Este archivo solo enumera nombres y orígenes. No contiene valores reales.

| Configuración | Desarrollo local | Docker Compose | Docker Swarm |
|---|---|---|---|
| PostgreSQL | User Secrets `ConnectionStrings:*` | `.env` → `POSTGRES_PASSWORD` | secreto `postgres_password` |
| SMTP | User Secrets `EmailSettings:*` | variables `EMAIL_*` | secreto `email_password` y variables no sensibles |
| Google OAuth | User Secrets `Authentication:Google:*` | variables `GOOGLE_*` | secretos `google_client_id`, `google_client_secret` |
| PayPal | User Secrets `PayPal:*` | variables `PAYPAL_*` | secretos `paypal_client_id`, `paypal_client_secret` |
| PayPhone | User Secrets `PayPhone:*` | variables `PAYPHONE_*` | secretos `payphone_token`, `payphone_store_id` |
| Certificado Data Protection | almacén seguro fuera del repositorio | opcional en un solo nodo | secretos `data_protection_certificate`, `data_protection_certificate_password` |

Los archivos `.env`, `*.pfx`, `*.p12`, dumps, claves de Data Protection y
`SECRETS.md` están ignorados por Git y por el contexto Docker.

Para rotar un secreto de Swarm, cree uno con un nombre versionado, actualice
`docker-stack.yml`, despliegue gradualmente y elimine la versión anterior solo
cuando ninguna tarea la utilice.
