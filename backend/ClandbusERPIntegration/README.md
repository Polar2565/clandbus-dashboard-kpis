# Backend · ClandBus ERP Integration

API ASP.NET Core 8 que autentica contra Acumatica, mantiene sesiones aisladas por navegador, sincroniza datos personales y persiste historial en SQL Server.

## Responsabilidades

- Login, perfil y logout de Acumatica.
- Cookie local HTTP-only y cliente REST independiente por sesión.
- Sincronización de casos y tareas del usuario.
- Importación del Excel más reciente desde `App_Data/TaskImports`.
- Persistencia por `UserKey` y bitácora profesional.
- Resumen, tendencias y productividad.

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
| GET | `/api/Dashboard/cases` | Casos personales |
| GET | `/api/Dashboard/tasks` | Tareas personales |
| GET | `/api/Dashboard/trends?period=` | Tendencias |
| GET/POST | `/api/Productivity/records` | Lista/crea bitácora |
| PUT/DELETE | `/api/Productivity/records/{id}` | Actualiza/elimina registro propio |

Salvo Salud y Login, las rutas personales requieren sesión.

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
