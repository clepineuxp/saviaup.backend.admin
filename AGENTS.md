# AGENTS.md — SaviaUp Backend Admin

## Límite del servicio

Este repositorio contiene exclusivamente la administración global de SaviaUp. Es un servicio independiente y nunca se deben modificar, referenciar como proyectos ni reutilizar clases internas de `../saviaup.backend`.

El servicio usa tres conexiones:

- `AdminDatabase`: base propia. Es la única sobre la que este repositorio crea migraciones. Contiene administradores, planes, asignaciones, overrides, estado de sincronización y auditoría.
- `PlatformDatabase`: base operativa existente. El adaptador administrativo consulta usuarios, organizaciones, membresías, módulos y permisos, y materializa únicamente el conjunto efectivo de `tenant_permissions`.
- `ApplicationDatabase`: base operativa existente. El adaptador consulta roles, órdenes, mesas y cajas para administración y monitoreo.

La plataforma operativa no conoce planes. Un plan siempre se traduce en este servicio a códigos de permiso y el resultado se sincroniza a `tenant_permissions`.

## Arquitectura obligatoria

Dirección de dependencias:

```text
Domain <- Application <- Infrastructure <- Api
```

- `Domain`: entidades administrativas y resultados; no conoce EF ni ASP.NET.
- `Application`: casos de uso, contratos y puertos; no conoce EF, Npgsql, HTTP ni Infrastructure.
- `Infrastructure`: persistencia administrativa y adaptadores para las bases operativas, JWT, hashing, correo y reloj.
- `Api`: Controllers tradicionales, autenticación, autorización, middleware y composición.

No introducir AutoMapper, MediatR, Autofac, Minimal APIs, Generic Repository, CQRS o ASP.NET Identity completo. Usar mapping explícito, `Result` para errores esperados, `CancellationToken` en todo flujo async y `IDateTimeProvider.UtcNow`.

## Seguridad y consistencia

- La identidad administrativa es distinta de los usuarios operativos.
- No quemar administradores ni contraseñas. El bootstrap solo se activa explícitamente con configuración externa y una contraseña que cumpla la política.
- JWT administrativos no contienen permisos de tenants.
- Nunca registrar contraseñas, JWT ni tokens de recuperación.
- Toda mutación sensible genera auditoría inmutable con actor y correlation id.
- Cambios sobre un plan se guardan primero en `AdminDatabase`; la sincronización operacional se registra como `PENDING`, `SYNCED` o `FAILED` y puede reintentarse.
- El reemplazo de permisos de una organización sí es transaccional dentro de `PlatformDatabase`; no se simula una transacción distribuida entre bases.
- Nunca crear migraciones para `PlatformDatabase` o `ApplicationDatabase` desde este repositorio.
- No deshabilitar ni reasignar al owner. El cambio de owner exige otro miembro activo y un rol alternativo activo para el owner anterior.
- Errores públicos usan `{ success: false, error: { code, message, details } }` y nunca incluyen stack trace.

## Validación

Antes de entregar cambios:

```powershell
dotnet build saviaup.backend.admin.sln
dotnet test saviaup.backend.admin.sln --no-build
```

Actualizar pruebas cuando cambie un contrato, caso de uso o regla de sincronización.
