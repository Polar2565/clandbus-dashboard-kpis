# Backend · ClandBus ERP Integration

API ASP.NET Core 8 que autentica contra Acumatica, mantiene sesiones aisladas por navegador, sincroniza datos personales y persiste historial en SQL Server.

## Responsabilidades

- Login, perfil y logout de Acumatica.
- Cookie local HTTP-only y cliente REST independiente por sesión.
- Sincronización de casos desde Acumatica y tareas desde la exportación Excel.
- Importación del Excel más reciente desde `App_Data/TaskImports`.
- Persistencia por `UserKey` y bitácora profesional.
- Resumen, tendencias y productividad.
- Consolidado ejecutivo del equipo protegido por lista de usuarios autorizados.

## Estructura

```text
Configurations/  Opciones de Acumatica
Controllers/     Endpoints HTTP
Data/            DashboardDbContext
DTOs/            Entradas y salidas
Interfaces/      Contratos de servicios
Models/          Entidades SQL
Services/        Acumatica, sesiones y sincronización
App_Data/TaskImports/  Excel de tareas
```

## Configuración y ejecución

```powershell
cd backend\ClandbusERPIntegration\ClandbusERPIntegration
Copy-Item appsettings.Development.example.json appsettings.Development.json
cd ..\..\..
dotnet run --project backend\ClandbusERPIntegration\ClandbusERPIntegration\ClandbusERPIntegration.csproj --launch-profile https
```

Configura localmente `BaseUrl`, versión, compañía y cadena SQL; nunca usuario, contraseña ni una URL privada en archivos versionados. HTTPS usa `7004`, HTTP `5124` y Swagger `/swagger`.

Agrega en `TeamDashboard:ExecutiveUsers` los usuarios o nombres exactos de Acumatica que podrán consultar el consolidado. Los integrantes no incluidos mantienen acceso exclusivamente personal.

## Endpoints

| Método | Ruta | Función |
| --- | --- | --- |
| GET | `/api/Health` | Salud |
| POST | `/api/Acumatica/login` | Inicia sesión |
| GET | `/api/Acumatica/me` | Usuario actual |
| GET | `/api/Acumatica/orders` | Órdenes heredadas |
| POST | `/api/Acumatica/update-order` | Actualiza descripción |
| POST | `/api/Acumatica/remove-hold` | Retira Hold |
| POST | `/api/Acumatica/logout` | Cierra sesión |
| POST | `/api/Dashboard/sync` | Sincroniza Acumatica |
| POST | `/api/Dashboard/import-tasks` | Importa Excel enviado |
| POST | `/api/Dashboard/import-latest-tasks` | Importa Excel más reciente |
| GET | `/api/Dashboard/task-import-folder` | Carpeta de importación |
| GET | `/api/Dashboard/summary` | KPIs personales |
| GET | `/api/Dashboard/view` | Entrega resumen, casos y tareas en una sola respuesta |
| GET | `/api/Dashboard/cases` | Casos personales |
| GET | `/api/Dashboard/tasks` | Tareas personales |
| GET | `/api/Dashboard/trends?period=` | Tendencias |
| GET | `/api/team-dashboard/access` | Indica si la cuenta puede filtrar por integrante |
| GET | `/api/team-dashboard/owners` | Propietarios disponibles para el selector global |
| GET/POST | `/api/Productivity/records` | Lista/crea bitácora |
| PUT/DELETE | `/api/Productivity/records/{id}` | Actualiza/elimina registro propio |

Salvo Salud y Login, las rutas personales requieren sesión. Los parámetros `owner` de Resumen, Vista, Casos, Tareas, Tendencias y Bitácora exigen además que el usuario autenticado esté en la lista ejecutiva local.

Angular utiliza normalmente `/api/Dashboard/view` para reducir viajes HTTP al cambiar de integrante. El filtro `owner` se aplica en SQL sobre la captura más reciente; la API no carga primero toda la información del equipo en memoria. Los endpoints separados se conservan para integraciones y diagnóstico.

## Flujo de datos

- Los casos se guardan como una captura SQL. De forma predeterminada se consultan mediante el Generic Inquiry `CR-Cases2018R1`.
- Para validar el contrato moderno, configura `Acumatica:PreferDefaultCaseEndpoint` en `true`. La API consultará `Default/{EndpointVersion}/Case` y volverá automáticamente al Generic Inquiry si Acumatica rechaza esa ruta.
- Con `Acumatica:PreferDefaultTaskEndpoint` en `true`, las tareas se consultan directamente desde `Default/{EndpointVersion}/Task`. El `.xlsx` de `App_Data/TaskImports` se conserva únicamente como respaldo manual.
- Con `Acumatica:PreferEmployeeDirectory` en `true`, el selector utiliza empleados activos publicados por `Default/{EndpointVersion}/Employee`; `TeamDashboard:Members` y los propietarios detectados siguen disponibles como respaldo.
- Una sincronización conserva la captura anterior si Acumatica rechaza la consulta, evitando presentar datos de otro usuario como respaldo.
- Las cuentas incluidas en `TeamDashboard:ExecutiveUsers` pueden sincronizar el conjunto del equipo y después filtrar por nombres canónicos.
- `OwnerMappings` relaciona identificadores de Acumatica con los nombres visibles del selector.

La versión de `Acumatica:EndpointVersion` debe coincidir con el endpoint publicado en cada instalación. No la actualices únicamente por cambiar la versión del ERP: valida primero `Case`, `Task`, `Activity` y `Employee` con consultas de solo lectura.

## Compilar

```powershell
dotnet build backend\ClandbusERPIntegration\ClandbusERPIntegration\ClandbusERPIntegration.csproj -c Release
```

## Datos y seguridad

Entity Framework administra snapshots, sincronizaciones y registros profesionales. La migración `sql/001_kpi_user_isolation_and_professional_records.sql` agrega aislamiento con `UserKey`. Las credenciales solo se envían a Acumatica durante login; no se registran ni persisten. CORS permite el frontend local configurado y reiniciar la API invalida las sesiones en memoria.

## Problemas comunes

- **Puerto 7004 ocupado:** detén la instancia anterior.
- **401:** inicia sesión otra vez; la API pudo reiniciarse.
- **SQL:** verifica LocalDB, cadena y migración.
- **Excel:** confirma `.xlsx`, carpeta y columnas esperadas.
- **Acumatica:** revisa URL, versión, permisos y conectividad.
- **Cambio de integrante lento o incorrecto:** revisa `/api/Dashboard/view?owner=...`, la captura más reciente y los mapeos; nunca reutilices la respuesta tardía del integrante anterior.
