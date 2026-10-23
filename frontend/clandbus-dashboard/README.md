# Frontend · ClandBus Dashboard

Cliente Angular 20 del tablero personal de soporte. Consume la API C# y nunca se conecta directamente con Acumatica o SQL Server.

## Pantallas

- `/resumen`: carga activa, pendientes, vencidas, categorías y prioridades.
- `/casos`: periodos, KPIs, estados, búsqueda, actividad de correo y paginación de 5, 10, 25 o todos los registros.
- `/tasks`: KPIs, categorías, tareas vencidas y detalle.
- `/productividad`: análisis, bitácora, reporte y rango personalizado.
- Selector superior global: cambia Resumen, Casos, Tareas y Productividad al propietario elegido.
- `/integracion`: sesión Acumatica, última captura y flujo de datos.

## Organización

```text
src/app/
├── core/models/       Contratos
├── core/services/     HTTP y estado compartido
├── core/utils/        Cálculos de KPIs
├── features/          Pantallas
└── shared/            Toast y carga global
```

## Ejecutar

```powershell
cd frontend\clandbus-dashboard
npm ci
npm start
```

Abre `http://127.0.0.1:4200`. La API debe estar en `https://localhost:7004`. Las solicitudes usan `withCredentials` para la cookie HTTP-only.

## Comandos

```powershell
npm start
npm run build
npm test -- --watch=false
npm run watch
```

## Estado y métricas

`DashboardDataService` carga perfil y consume `/api/Dashboard/view` para obtener resumen, casos y tareas en una sola respuesta. Mantiene una caché independiente por propietario, cancela la solicitud anterior al cambiar rápidamente de persona e ignora respuestas atrasadas para evitar mezclar perfiles. Una actualización forzada reemplaza la caché con la captura más reciente.

Casos usa la actividad más reciente entre creación, correo recibido y respuesta enviada. La tabla muestra cinco registros por defecto y pagina en el navegador para no renderizar miles de filas al abrir Histórico. Una tarea es vencida si su compromiso pasó, tiene avance menor a 100% y no está cancelada. Productividad separa volumen de cumplimiento para evitar evaluaciones engañosas y guarda la bitácora por propietario.

## Pruebas y consideraciones

La suite actual contiene 9 pruebas. Valida además navegación responsive, modales de pantalla completa, filtros, paginación, cambio rápido entre varios propietarios, estados vacíos y logout. No coloques secretos en TypeScript. “Excel” produce CSV UTF-8 y PDF usa `window.print()`.
