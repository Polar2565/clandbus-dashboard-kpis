# Guía de contribución

Este repositorio utiliza un flujo sencillo para mantener estable la rama principal y evitar la exposición de información sensible.

## Ramas

- `main`: versión estable. No debe recibir pushes directos.
- `desarrollo`: integración y validación previa de cambios.
- Ramas de trabajo: créalas desde `desarrollo`, por ejemplo `feature/filtros-casos`, `fix/importacion-excel` o `docs/instalacion`.

## Flujo recomendado

```powershell
git switch desarrollo
git pull origin desarrollo
git switch -c feature/nombre-del-cambio
```

Realiza cambios pequeños y ejecuta:

```powershell
dotnet build backend\ClandbusERPIntegration\ClandbusERPIntegration\ClandbusERPIntegration.csproj -c Release
npm --prefix frontend\clandbus-dashboard run build
npm --prefix frontend\clandbus-dashboard test -- --watch=false
```

Publica tu rama y abre un Pull Request hacia `desarrollo`. Cuando el conjunto esté validado, abre otro Pull Request de `desarrollo` hacia `main`.

## Revisión obligatoria de seguridad

Antes de cada commit confirma que no agregaste:

- URLs privadas del ERP, CRM, VPN o servicios internos;
- usuarios, contraseñas, tokens, cookies o secretos de API;
- cadenas de conexión reales;
- nombres, correos, identificadores o información de clientes y empleados;
- archivos `.xlsx` exportados desde Acumatica;
- `appsettings.Development.json`, archivos `.env`, logs, bases de datos o respaldos;
- rutas absolutas del equipo de una persona.

Usa únicamente valores ficticios, como `https://tu-instancia-acumatica.example/` y `TU_TENANT`.

## Pull Requests

Todo Pull Request debe explicar el objetivo, describir los cambios, indicar las pruebas realizadas y confirmar que no contiene datos sensibles. Actualiza la documentación cuando cambien la instalación o el comportamiento.

No publiques capturas con datos reales. Si encuentras una vulnerabilidad, sigue [SECURITY.md](SECURITY.md) y no abras un Issue público.
