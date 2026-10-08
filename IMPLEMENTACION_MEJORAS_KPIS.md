# Implementación de mejoras KPIs

**Fecha:** 8 de octubre de 2026  
**Proyecto:** ClandBus Gestión de Soporte  
**Base técnica verificada:** Angular 20.3, TypeScript 5.9, ASP.NET Core 8, EF Core 8.0.10 y SQL Server LocalDB.

## 1. Resumen de cambios realizados

- Se consolidó un sistema visual ejecutivo y responsivo sin reemplazar la identidad de la barra lateral.
- Se reorganizó la barra superior, estados de sesión, tarjetas, tablas, filtros y modales.
- Se corrigió la semántica temporal de KPIs: casos creados usan `CreatedAt`; tareas concluidas usan `CompletedAt`; tareas en curso usan `StartAt`.
- Se agregaron utilidades tipadas y pruebas para periodos, estados, completitud y vencimiento.
- Productividad ahora incluye día, semana, mes, trimestre, año, histórico y rango manual.
- Se agregó comparación contra el periodo anterior y evolución de iniciadas frente a completadas.
- Se implementó una bitácora profesional persistente para resultados, dificultades, soluciones, aprendizajes y áreas de mejora.
- La bitácora puede relacionarse con tareas y casos reales ya cargados.
- Se agregaron vistas específicas de Análisis, Bitácora profesional y Reporte.
- Se agregó exportación imprimible a PDF y exportación CSV compatible con Excel.
- Se aisló la sesión de Acumatica por navegador y se eliminaron el `CookieContainer`, identidad y credenciales globales compartidos.
- Se particionaron snapshots y registros profesionales por usuario en backend y SQL.
- Los endpoints con datos privados ahora exigen una sesión vigente en el backend.
- Se añadió validación de DTOs de login y bitácora.
- Se creó y aplicó localmente una migración SQL aditiva, idempotente y acompañada por script de reversión.

## 2. Mejoras visuales implementadas

- Tokens globales para colores, superficies, sombras, radios, tipografía, focos y estados semánticos.
- Topbar compacta con contexto, sincronización, sesión y acción de refresco.
- Sidebar corporativa conservada con mejor alineación, hover, foco, selección y modo colapsado.
- Cards de KPI más densas, legibles y consistentes.
- Estados rojo, ámbar, verde, azul y gris acompañados por texto, no solo por color.
- Tablas y listados con mayor legibilidad, separación y estados interactivos.
- Modal de acceso centrado con fondo desenfocado y jerarquía profesional.
- Adaptaciones para escritorio, laptop, tablet y móvil.
- Estados vacíos explícitos en Productividad, Bitácora y gráficas.

## 3. Funcionalidades nuevas

### Productividad y evolución

- Periodos: día, semana, mes, trimestre, año, histórico y rango personalizado.
- Comparación porcentual con el periodo inmediatamente anterior.
- Serie temporal separada de tareas iniciadas y concluidas.
- Distribución por categorías y balance de actividades.
- Diferenciación entre volumen de trabajo y resultados documentados.

### Bitácora profesional

- Alta, consulta, edición y eliminación de registros propios.
- Tipos: Migración, Caso, Certificación, Desarrollo y Otra actividad.
- Relación opcional con una tarea o caso real.
- Campos opcionales: resultado, problema, solución, aprendizaje, área de mejora y observaciones.
- Persistencia SQL aislada por usuario.

### Reportes

- Vista previa por el periodo seleccionado.
- Indicadores calculados diferenciados de observaciones manuales.
- Actividades, casos con actividad, migraciones y bitácora profesional.
- PDF mediante vista profesional de impresión del navegador.
- Archivo CSV UTF-8 compatible con Excel, sin agregar bibliotecas externas.

### Seguridad y sesiones

- Cookie de aplicación aleatoria de 256 bits, `HttpOnly`, `Secure`, `SameSite=None` y vigencia de ocho horas.
- Un `AcumaticaService`, `HttpClient` y `CookieContainer` independientes por sesión.
- Cierre de sesión limitado a la sesión del navegador correspondiente.
- Consultas SQL filtradas por el usuario autenticado.
- CRUD profesional filtrado por usuario tanto en lectura como en modificación.
- `summary`, `cases`, `tasks`, `trends`, sincronización, importaciones y ruta de importación requieren sesión.

## 4. Componentes modificados

### Angular

- `src/styles.scss`
- `src/app/app.scss`
- `src/app/app.spec.ts`
- `src/app/core/models/dashboard.models.ts`
- `src/app/core/utils/kpi.utils.ts`
- `src/app/core/utils/kpi.utils.spec.ts`
- `src/app/features/cases/cases.component.ts`
- `src/app/features/tasks/tasks.component.ts`
- `src/app/features/tasks/tasks.component.html`
- `src/app/features/productivity/productivity.component.ts`
- `src/app/features/productivity/productivity.component.html`
- `src/app/features/productivity/productivity.component.scss`
- `src/app/features/productivity/productivity.models.ts`
- `src/app/features/productivity/productivity-records.service.ts`

### Backend

- `Program.cs`
- `Controllers/AcumaticaController.cs`
- `Controllers/DashboardController.cs`
- `Controllers/ProductivityController.cs`
- `Services/AcumaticaService.cs`
- `Services/AcumaticaSessionStore.cs`
- `Services/SynchronizationService.cs`
- `Interfaces/IAcumaticaService.cs`
- `Interfaces/IAcumaticaSessionStore.cs`
- `Interfaces/ISynchronizationService.cs`
- `DTOs/LoginRequestDto.cs`
- `DTOs/ProfessionalRecordRequest.cs`
- `Models/CaseSnapshot.cs`
- `Models/TaskSnapshot.cs`
- `Models/SyncRun.cs`
- `Models/ProfessionalRecord.cs`
- `Data/DashboardDbContext.cs`

## 5. Cambios en backend y contratos

### Sesión

- `POST /api/Acumatica/login` crea una sesión aislada y devuelve la identidad resuelta.
- `GET /api/Acumatica/me` consulta exclusivamente la cookie del navegador.
- `POST /api/Acumatica/logout` elimina y cierra solo esa sesión.
- Operaciones heredadas de órdenes también requieren sesión.

### Bitácora profesional

- `GET /api/Productivity/records?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `POST /api/Productivity/records`
- `PUT /api/Productivity/records/{id}`
- `DELETE /api/Productivity/records/{id}`

El backend valida tipo, tamaños máximos, fecha e identidad propietaria.

### Snapshots

`Cases`, `Tasks` y `SyncRuns` incorporan `UserKey`. Sincronización, resumen, tendencias e importación consultan únicamente la partición del usuario actual.

## 6. Cambios en base de datos

Scripts añadidos:

- `sql/001_kpi_user_isolation_and_professional_records.sql`
- `sql/001_kpi_user_isolation_and_professional_records.rollback.sql`

La migración:

- Agrega `UserKey` a casos, tareas y ejecuciones.
- Crea `ProfessionalRecords`.
- Crea índices por usuario, identificador y fecha.
- Es aditiva, transaccional e idempotente.
- No elimina ni reasigna información histórica.

El script se aplicó satisfactoriamente sobre la base local de desarrollo. El rollback se entrega para control de cambios, pero es destructivo para los registros profesionales y no se ejecutó.

## 7. Dependencias agregadas

No se agregaron dependencias de ejecución. Los reportes reutilizan capacidades nativas del navegador. `npx` utilizó Prettier temporalmente para formatear una plantilla; no se agregó al `package.json`.

## 8. Compilaciones ejecutadas

| Verificación | Resultado |
| --- | --- |
| `npm run build` | Aprobada |
| TypeScript / Angular template compiler | Aprobada |
| Bundle Angular | 503.78 kB bruto; 123.63 kB estimado transferido |
| `dotnet build ... -c Release` | Aprobada, 0 errores y 0 advertencias |
| Inicio de API HTTPS | Aprobado |
| `GET /api/Health` | HTTP 200 |

Los primeros intentos de compilación .NET sobre la misma carpeta de salida fallaron porque el ejecutable de la API estaba abierto. Se detuvo únicamente ese proceso local, se recompiló correctamente y se reinició la API.

## 9. Pruebas realizadas

| Prueba | Resultado |
| --- | --- |
| Suite Angular/Karma en Chrome 154 | 9/9 aprobadas |
| Pruebas nuevas de KPIs | 7/7 aprobadas |
| Creación del componente raíz | Aprobada |
| Render del contexto de aplicación | Aprobada |
| Dashboard sin sesión | `summary`, `cases`, `tasks` devuelven HTTP 401 |
| Bitácora sin sesión | `Productivity/records` devuelve HTTP 401 |
| Migración SQL ejecutada nuevamente | Idempotencia validada |
| Revisión visual en navegador | Shell, sidebar, topbar, pantalla protegida y modal de login correctos |
| Sesión real de Javier Solis | Detectada como activa y reutilizada entre pantallas |
| Sincronización real disponible | Última captura confirmada: 8/10/2026 11:58:02 a.m. |
| Resumen con datos reales | 9 casos activos, 7 esperando cliente, 5 tareas pendientes y 2 vencidas |
| Detalle interactivo de KPI | Modal de las 2 tareas vencidas validado con nombres, casos, fechas, estados y avances |
| Casos con datos reales | 55 históricos; búsqueda exacta, periodo semanal, distribución y detalle de actividad aprobados |
| Tareas con datos reales | 69 históricas, 63 concluidas, 6 pendientes y 2 vencidas; periodos semanal e histórico aprobados |
| Productividad con datos reales | Día, mes, año, histórico y rango personalizado recalculan correctamente |
| Reporte de productividad | Vista previa y generación CSV ejecutadas; el navegador integrado no expone su descarga en `Downloads` |
| Integración | Estado conectado, aislamiento por usuario y fecha de última captura visibles |
| `git diff --check` | Sin errores; solo avisos de normalización LF/CRLF |

## 10. Problemas detectados y corregidos

- Sesión Acumatica singleton compartida: sustituida por almacén de sesiones aisladas.
- Datos del último usuario visibles globalmente: consultas particionadas mediante `UserKey`.
- Endpoints privados accesibles anónimamente: protegidos por validación de sesión backend.
- Tests raíz obsoletos y sin `HttpClient`/iconos: actualizados.
- Casos del periodo sin resultados útiles: ahora el seguimiento usa la actividad más reciente entre creación, correo recibido y respuesta enviada.
- KPI de tareas vencidas sin detalle visible inmediato: ahora abre un modal con tarea, caso relacionado, avance y vencimiento.
- Tareas concluidas mezcladas con fecha de inicio: ahora usan `CompletedAt` para periodos.
- Tareas canceladas consideradas vencidas: excluidas.
- Tiempo medio de cierre sin fechas mostrado como cero: ahora muestra “No disponible”.
- Productividad sin trimestre, comparación ni bitácora: implementados.

## 11. Pendientes o no verificados

- El login real, la lectura de la sesión, el snapshot personal y las pantallas con datos reales quedaron verificados. No se probó una nueva escritura de bitácora para evitar crear o eliminar registros profesionales durante la validación.
- La generación CSV se ejecutó desde la interfaz, pero el navegador integrado no publicó el archivo en la carpeta `Downloads`; debe confirmarse la descarga final en Chrome. El código ahora conserva temporalmente la URL del archivo y anexa el enlace al documento para maximizar compatibilidad.
- Las filas históricas anteriores a la migración conservan `UserKey=''` por seguridad. No se reasignan automáticamente a una persona.
- Acumatica no entrega actualmente una fecha fiable de cierre de caso en el modelo. No se muestra tiempo promedio de resolución ni cierres por periodo como dato inventado.
- La exportación de “Excel” es CSV UTF-8 compatible con Excel, no un archivo binario `.xlsx`.
- El PDF utiliza el diálogo nativo de impresión/Guardar como PDF; el usuario debe completar esa acción.
- La sesión se conserva en memoria del proceso. Un reinicio del backend exige iniciar sesión nuevamente, aunque los datos SQL permanecen.
- No se evaluaron dos cuentas reales simultáneas por falta de una segunda cuenta autorizada; el código y las consultas sí quedaron aislados por cookie y `UserKey`.
- El endpoint Acumatica mantiene límite de 1,000 filas y no procesa paginación OData todavía.
- No se implementaron chatbot, IA generativa, MCP ni panel administrativo, conforme al alcance.

## 12. Instrucciones para ejecutar

### Primera actualización de una base existente

Ejecutar con una cuenta SQL autorizada:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d ClandbusDashboard -E -b -i sql\001_kpi_user_isolation_and_professional_records.sql
```

Usar los valores reales de servidor y base configurados en `ConnectionStrings:Dashboard` si son distintos. No ejecutar el rollback salvo que exista respaldo y autorización.

### Backend

```powershell
dotnet run --project backend\ClandbusERPIntegration\ClandbusERPIntegration\ClandbusERPIntegration.csproj --launch-profile https
```

### Frontend

```powershell
npm --prefix frontend\clandbus-dashboard start
```

Abrir `http://127.0.0.1:4200`, iniciar sesión y realizar una sincronización/importación inicial para poblar la partición personal.

## 13. Pruebas manuales recomendadas

1. Confirmar en Chrome la descarga física del CSV y guardarlo como `.xlsx` si se requiere ese formato binario.
2. Crear, editar y eliminar un registro de bitácora de prueba con autorización para la eliminación.
3. Guardar el reporte mediante “Imprimir → Guardar como PDF”.
4. Cerrar sesión y confirmar que desaparezcan todos los datos privados.
5. Si existe otra cuenta de pruebas, abrirla en otro navegador y confirmar que no vea snapshots ni bitácora de la primera.

