# SaviaUp Backend Admin

API administrativa global e independiente para SaviaUp, construida en .NET 10 con arquitectura hexagonal. Este servicio es el backend de `saviaup.frontend.admin` y no forma parte del API operacional de restaurantes.

## Límite de responsabilidad

El proyecto no referencia ni modifica `saviaup.backend`. Se integra por base de datos mediante adaptadores explícitos y tres conexiones:

| Conexión | Propiedad | Uso |
| --- | --- | --- |
| `AdminDatabase` | Este servicio | Administradores, planes, historial de precios, asignaciones, excepciones, sincronización y auditoría |
| `PlatformDatabase` | Plataforma operacional existente | Usuarios, organizaciones, membresías, catálogo de permisos y materialización de `tenant_permissions` |
| `ApplicationDatabase` | Plataforma operacional existente | Roles, órdenes, mesas y turnos de caja para gestión y monitoreo |

Solo `AdminDatabase` recibe migraciones desde este repositorio. Los contextos operacionales no implementan `IDesignTimeDbContextFactory` ni tienen migraciones.

## Cómo funcionan los planes

Un plan guarda códigos de permiso, precio mensual, moneda, estado e historial de precios. La base operacional nunca recibe `PlanId` ni consulta tablas administrativas.

```text
Plan + excepciones por organización
                 ↓
       permisos efectivos
                 ↓
 PlatformDatabase.tenant_permissions
```

El cambio se guarda primero en la base administrativa con estado `PENDING`. Luego se reemplaza el conjunto completo de permisos de la organización dentro de una transacción local de `PlatformDatabase`:

- `SYNCED`: la materialización terminó correctamente.
- `FAILED`: la configuración administrativa se conservó y puede reintentarse con `POST /api/admin/organizations/{id}/permissions/sync`.

No se simula una transacción distribuida entre bases. Así un fallo operacional queda visible, auditable y recuperable.

Una organización no necesita tener un plan. `PATCH /api/admin/organizations/{id}/permissions/{permissionCode}` inicia automáticamente la gestión manual con `PlanId = null`. En el primer cambio se conserva el conjunto de permisos que el tenant ya tiene en la plataforma y solo se modifica el permiso solicitado. Los cambios posteriores continúan sincronizando el conjunto manual completo con `tenant_permissions`.

Ejemplos que pueden configurarse desde la UI, sin quedar quemados en código:

- `STANDARD`: mesas, categorías y productos.
- `MEDIUM`: capacidades de `STANDARD` más inventario y cajas.

## Arquitectura

```text
saviaup.backend.admin.Domain          Entidades y Result
saviaup.backend.admin.Application     Casos de uso, contratos y puertos
saviaup.backend.admin.Infrastructure  EF Core, PostgreSQL, JWT, email y adaptadores operacionales
saviaup.backend.admin.Api             Controllers, auth, CORS, rate limiting y middleware
tests/
  saviaup.backend.admin.Application.Tests
  saviaup.backend.admin.IntegrationTests
```

La dirección de dependencias es `Domain <- Application <- Infrastructure <- Api`. No se usan Minimal APIs, MediatR, AutoMapper, Generic Repository ni ASP.NET Identity completo.

## Capacidades disponibles

- identidad administrativa separada con roles `SUPER_ADMIN` y `SUPPORT`;
- bootstrap explícito del primer administrador, sin credenciales versionadas;
- usuarios administrativos: creación, listado y activación/desactivación;
- consulta global de usuarios y sus organizaciones;
- activación y reactivación de membresías;
- reasignación de miembros no propietarios;
- restablecimiento de contraseña compatible con el frontend operacional;
- consulta, activación y desactivación de organizaciones;
- transferencia segura de owner;
- catálogo, creación y edición de planes y pricing;
- historial de cambios de precio;
- asignación de plan y excepciones de permisos por organización;
- supervisión consolidada de órdenes, ventas, mesas y cajas;
- reglas configurables de estado operacional, con umbral, severidad y activación por hallazgo;
- auditoría inmutable de mutaciones sensibles;
- correlation id, CORS restringido, rate limiting de login y errores estables.

## Configuración local

Requisitos: .NET SDK 10 y PostgreSQL. Las conexiones `PlatformDatabase` y `ApplicationDatabase` deben ser las mismas que usa el backend operacional actual.

Los proyectos `Api` e `Infrastructure` comparten el `UserSecretsId` `saviaup-backend-admin-local`. Por eso la API en ejecución y la fábrica de diseño de EF Core leen el mismo almacén local. Usa User Secrets o variables de entorno; no reemplaces los placeholders de `appsettings.json` con secretos reales:

```powershell
cd saviaup.backend.admin.Api
dotnet user-secrets set "ConnectionStrings:AdminDatabase" "Host=localhost;Port=5432;Database=saviaup_admin;Username=postgres;Password=<password>"
dotnet user-secrets set "ConnectionStrings:PlatformDatabase" "<misma conexión PlatformDatabase del backend operacional>"
dotnet user-secrets set "ConnectionStrings:ApplicationDatabase" "<misma conexión ApplicationDatabase del backend operacional>"
dotnet user-secrets set "AdminJwt:SigningKey" "<clave aleatoria de al menos 32 bytes>"
```

También puedes establecer `AdminDatabase` desde el proyecto de infraestructura; el secreto termina en el mismo almacén:

```powershell
cd ..
dotnet user-secrets --project saviaup.backend.admin.Infrastructure set "ConnectionStrings:AdminDatabase" "Host=localhost;Port=5432;Database=saviaup_admin;Username=postgres;Password=<password>"
```

Restaura la herramienta local y aplica exclusivamente la migración administrativa usando `Api` como proyecto de inicio:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project saviaup.backend.admin.Infrastructure --startup-project saviaup.backend.admin.Api --context AdminDbContext
```

El flujo equivalente con `Infrastructure` como proyecto de inicio es:

```powershell
dotnet tool run dotnet-ef database update --project saviaup.backend.admin.Infrastructure --startup-project saviaup.backend.admin.Infrastructure --context AdminDbContext
```

Para crear una nueva migración administrativa:

```powershell
dotnet tool run dotnet-ef migrations add NombreMigracion --project saviaup.backend.admin.Infrastructure --startup-project saviaup.backend.admin.Infrastructure --context AdminDbContext --output-dir Persistence/Migrations
```

La fábrica de diseño solo resuelve `ConnectionStrings:AdminDatabase` o, para automatización/CI, `SAVIAUP_ADMIN_CONNECTION`. Nunca selecciona `PlatformDatabase` ni `ApplicationDatabase`, por lo que las migraciones no pueden apuntar accidentalmente a una base operacional.

Para crear el primer administrador, configura temporalmente:

```powershell
cd saviaup.backend.admin.Api
dotnet user-secrets set "BootstrapAdmin:Enabled" "true"
dotnet user-secrets set "BootstrapAdmin:Name" "Administrador SaviaUp"
dotnet user-secrets set "BootstrapAdmin:Email" "admin@tu-dominio.com"
dotnet user-secrets set "BootstrapAdmin:Password" "<contraseña fuerte de 12+ caracteres>"
dotnet run
```

Después del primer inicio, desactiva `BootstrapAdmin:Enabled`. El proceso es idempotente por correo normalizado y nunca registra la contraseña.

La API local inicia en `http://localhost:5100`; Swagger está disponible en `/swagger` y el health check de las tres bases en `/health`.

## Frontend administrativo

`../saviaup.frontend.admin` usa el adaptador HTTP real por defecto y apunta localmente a `http://localhost:5100`. Inícialo con:

```powershell
cd ../saviaup.frontend.admin
npm install
npm start
```

El frontend se sirve en `http://localhost:4300`. El token administrativo se guarda en `sessionStorage` y se envía como Bearer exclusivamente a `/api/admin`.

## Aprovisionamiento de organizaciones

Este servicio no cambia el endpoint operacional que crea organizaciones. Después de crear una organización, un administrador u orquestador debe ejecutar:

```http
PUT /api/admin/organizations/{organizationId}/plan
Authorization: Bearer <admin-jwt>

{
  "planId": "<active-plan-id>",
  "preserveOverrides": false
}
```

Esto permite incorporar el plan al flujo de alta sin hacer que la base o el API operacionales conozcan el modelo comercial.

## Endpoints principales

```text
POST  /api/admin/auth/login

GET   /api/admin/dashboard
GET   /api/admin/operations
GET   /api/admin/operations/settings
PUT   /api/admin/operations/settings
GET   /api/admin/users
POST  /api/admin/users/{userId}/password-reset
PATCH /api/admin/memberships/{membershipId}/status
POST  /api/admin/memberships/reassign

GET   /api/admin/organizations
GET   /api/admin/organizations/{id}
PATCH /api/admin/organizations/{id}/status
PUT   /api/admin/organizations/{id}/owner
PATCH /api/admin/organizations/{id}/permissions/{permissionCode}
PUT   /api/admin/organizations/{id}/plan
POST  /api/admin/organizations/{id}/permissions/sync

GET   /api/admin/plans
GET   /api/admin/plans/{id}
GET   /api/admin/plans/permissions/catalog
POST  /api/admin/plans
PUT   /api/admin/plans/{id}
PATCH /api/admin/plans/{id}/status

GET   /api/admin/admin-users
POST  /api/admin/admin-users
PATCH /api/admin/admin-users/{id}/status
GET   /api/admin/operations
```

`SUPPORT` puede consultar; las mutaciones requieren `SUPER_ADMIN`.

## Validación

```powershell
dotnet build saviaup.backend.admin.sln
dotnet test saviaup.backend.admin.sln --no-build
dotnet format saviaup.backend.admin.sln --verify-no-changes
```

Las pruebas cubren autenticación administrativa, validación de permisos de planes, materialización efectiva, persistencia de fallos de sincronización y contratos HTTP de autenticación/errores.
