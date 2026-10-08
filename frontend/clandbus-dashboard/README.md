# Frontend · ClandBus Dashboard

Cliente Angular 20 del tablero personal de soporte. Consume la API C# y nunca se conecta directamente con Acumatica o SQL Server.

## Pantallas

- `/resumen`: carga activa, pendientes, vencidas, categorías y prioridades.
- `/casos`: periodos, KPIs, estados, búsqueda y actividad de correo.
- `/tasks`: KPIs, categorías, tareas vencidas y detalle.
- `/productividad`: análisis, bitácora, reporte y rango personalizado.
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

`DashboardDataService` carga perfil, resumen, casos y tareas y administra carga/notificaciones. Casos usa la actividad más reciente entre creación, correo recibido y respuesta enviada. Una tarea es vencida si su compromiso pasó, tiene avance menor a 100% y no está cancelada. Productividad separa volumen de cumplimiento para evitar evaluaciones engañosas.

## Pruebas y consideraciones

La suite actual contiene 9 pruebas. Valida además navegación responsive, modales, filtros, estados vacíos y logout. No coloques secretos en TypeScript. “Excel” produce CSV UTF-8 y PDF usa `window.print()`.
