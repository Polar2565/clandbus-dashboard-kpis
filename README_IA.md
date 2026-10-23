# Guía para configurar el proyecto con una IA

Este documento está dirigido a asistentes de programación que deban instalar, configurar, ejecutar, diagnosticar o modificar **ClandBus Dashboard de Gestión de Soporte** en la computadora de un colaborador.

Lee primero este archivo completo y después consulta los README específicos del backend y frontend. No asumas valores privados ni reutilices configuraciones pertenecientes a otra persona.

## Objetivo

Dejar funcionando localmente:

- frontend Angular en `http://127.0.0.1:4200`;
- backend ASP.NET Core en `https://localhost:7004`;
- Swagger en `https://localhost:7004/swagger`;
- base local SQL Server o LocalDB;
- autenticación interactiva contra una instancia autorizada de Acumatica;
- sincronización de casos y lectura del Excel de tareas.

## Reglas obligatorias para la IA

1. No publiques ni inventes URLs privadas, usuarios, contraseñas, cookies, tokens, cadenas de conexión o información de clientes.
2. No copies configuraciones locales de otra computadora.
3. No agregues a Git `appsettings.Development.json`, archivos `.xlsx`, bases locales, logs o exportaciones.
4. Utiliza `appsettings.Development.example.json` solamente como plantilla.
5. Las credenciales de Acumatica se introducen en la pantalla de inicio de sesión; no deben guardarse en código, SQL ni archivos versionados.
6. Trabaja normalmente en la rama `desarrollo`. No hagas push directo a `main`.
7. Antes de modificar archivos revisa `git status`. Conserva cualquier cambio ajeno que ya exista.
8. No elimines ni reemplaces archivos del usuario para solucionar un problema de configuración.
9. No desactives la validación HTTPS ni amplíes CORS sin justificarlo y probarlo.
10. Antes de un commit ejecuta las verificaciones indicadas en este documento.

## Estructura importante

```text
ClandbusDashboard/
├── backend/ClandbusERPIntegration/
│   ├── ClandbusERPIntegration.sln
│   ├── README.md
│   └── ClandbusERPIntegration/
│       ├── Controllers/
│       ├── Services/
│       ├── Data/
│       ├── Models/
│       ├── App_Data/TaskImports/
│       ├── appsettings.Development.example.json
│       └── ClandbusERPIntegration.csproj
├── frontend/clandbus-dashboard/
│   ├── src/app/
│   ├── package.json
│   └── README.md
├── sql/
├── README.md
├── README_IA.md
└── SECURITY.md
```

## Requisitos que debes comprobar

Ejecuta comprobaciones de solo lectura antes de instalar o cambiar algo:

```powershell
git --version
dotnet --info
node --version
npm --version
sqlcmd -?
```

Requisitos esperados:

| Componente | Requisito |
| --- | --- |
| Windows | Windows 10 u 11 de 64 bits |
| .NET | SDK 8.x |
| Node.js | 20 LTS o 22 LTS |
| npm | 10 o superior |
| Base de datos | SQL Server 2019+, SQL Server Express o LocalDB |
| Navegador | Chrome o Edge actualizado |
| Acumatica | Instancia accesible y cuenta autorizada |

No es necesario instalar Angular CLI globalmente. El proyecto utiliza la versión local declarada en `package.json`.

## Clonación recomendada

```powershell
git clone --branch desarrollo --single-branch https://gitlab.com/javier.solis.clandbus/clandbus-dashboard-kpis.git
cd clandbus-dashboard-kpis
git status --short --branch
```

El repositorio de GitLab es privado. La persona debe autenticarse con su propia cuenta autorizada; la IA no debe pedir, copiar ni guardar contraseñas o tokens personales. La salida debe indicar la rama `desarrollo` y no mostrar archivos modificados.

## Configuración local del backend

Desde la raíz del repositorio:

```powershell
Copy-Item `
  backend\ClandbusERPIntegration\ClandbusERPIntegration\appsettings.Development.example.json `
  backend\ClandbusERPIntegration\ClandbusERPIntegration\appsettings.Development.json
```

Solicita al usuario o al responsable técnico los valores correspondientes a su entorno. Nunca los adivines.

Configura localmente:

| Campo | Significado |
| --- | --- |
| `Acumatica:BaseUrl` | URL base de la instancia autorizada; debe terminar en `/` |
| `Acumatica:EndpointVersion` | Versión del endpoint REST publicado en esa instancia |
| `Acumatica:Company` | Tenant o compañía exacta |
| `Acumatica:Branch` | Sucursal; puede quedar vacía si no aplica |
| `Acumatica:OwnerMappings` | Correspondencia entre identificadores de Acumatica y nombres visibles |
| `ConnectionStrings:Dashboard` | Cadena local de SQL Server o LocalDB |
| `TeamDashboard:ExecutiveUsers` | Cuentas autorizadas para consultar otros integrantes |
| `TeamDashboard:Members` | Nombres canónicos que aparecerán en el selector |

`24.200.001` en el archivo de ejemplo no significa que todas las instalaciones utilicen esa versión. Confirma el endpoint publicado en la instancia correspondiente.

Ejemplo seguro de estructura:

```json
{
  "Acumatica": {
    "BaseUrl": "https://instancia-autorizada.example/",
    "EndpointVersion": "VERSION_PUBLICADA",
    "Company": "TENANT_AUTORIZADO",
    "Branch": "",
    "OwnerId": "",
    "OwnerName": "",
    "OwnerMappings": {
      "IDENTIFICADOR_DE_ACUMATICA": "Nombre del integrante"
    }
  },
  "ConnectionStrings": {
    "Dashboard": "Server=(localdb)\\MSSQLLocalDB;Database=ClandbusProductivity;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "TeamDashboard": {
    "ExecutiveUsers": ["usuario.autorizado@empresa.example"],
    "Members": ["Nombre del integrante"]
  },
  "AllowedHosts": "*"
}
```

## Cómo identificar propietarios

Los casos pueden contener un identificador interno de propietario y las tareas pueden contener un nombre visible. Para que el selector sea consistente:

1. Obtén los identificadores desde una fuente autorizada de Acumatica.
2. Agrega cada correspondencia a `OwnerMappings` en la configuración local.
3. Usa en `Members` un único nombre canónico por persona.
4. No agregues simultáneamente variantes como `Javier` y `Javier Solis`.
5. Después de sincronizar, comprueba que cada integrante muestre exclusivamente sus registros.

Si no existe información suficiente para construir el mapeo, detén la configuración y solicita los identificadores al responsable. No infieras identidades por coincidencias ambiguas.

## Preparación de la base de datos

La aplicación crea las tablas necesarias al iniciar. Para una base existente puede ser necesaria la migración incluida:

```powershell
sqlcmd `
  -S "(localdb)\MSSQLLocalDB" `
  -d ClandbusProductivity `
  -E -b `
  -i sql\001_kpi_user_isolation_and_professional_records.sql
```

No ejecutes el archivo de rollback sin una solicitud explícita y un respaldo comprobado.

## Instalación de dependencias

Backend:

```powershell
dotnet restore backend\ClandbusERPIntegration\ClandbusERPIntegration\ClandbusERPIntegration.csproj
```

Frontend:

```powershell
npm --prefix frontend\clandbus-dashboard ci
```

Si `npm ci` falla, informa el error antes de sustituirlo por otra operación. No actualices paquetes automáticamente durante la configuración inicial.

## Ejecución

Inicia una sola instancia del backend:

```powershell
dotnet run `
  --project backend\ClandbusERPIntegration\ClandbusERPIntegration\ClandbusERPIntegration.csproj `
  --launch-profile https
```

En otra terminal inicia Angular:

```powershell
npm --prefix frontend\clandbus-dashboard start
```

Abre:

```text
http://127.0.0.1:4200
```

No inicies el backend simultáneamente desde una terminal y Visual Studio. Si aparece `address already in use` para el puerto `7004`, localiza la instancia existente y permite que el usuario elija cuál conservar.

## Excel de tareas

Los casos se obtienen mediante la integración con Acumatica. Las tareas se importan desde un archivo `.xlsx` exportado de la pantalla Tareas.

Ruta local esperada:

```text
backend/ClandbusERPIntegration/ClandbusERPIntegration/App_Data/TaskImports/
```

El archivo debe conservar los encabezados originales utilizados por el importador, entre ellos:

- `Resumen`
- `Estado`
- `(%) Terminado`
- `Fecha de Inicio`
- `Fecha Vencimiento`
- `Categoría`
- `Propietario`
- `Entidad Relacionada`

La API utiliza el `.xlsx` con fecha de modificación más reciente. Los archivos reales están ignorados por Git y no deben incorporarse al repositorio.

## Primera ejecución funcional

1. Confirma que `https://localhost:7004/swagger` responda.
2. Confirma que `http://127.0.0.1:4200` abra el frontend.
3. Pide al usuario que introduzca personalmente sus credenciales en la pantalla de Acumatica.
4. Ejecuta **Sincronizar ahora** desde Integración.
5. Ejecuta **Actualizar tareas** después de colocar el Excel.
6. Revisa Resumen, Casos, Tareas y Productividad.
7. Comprueba que la fecha de última captura sea reciente.
8. Si la cuenta es ejecutiva, prueba al menos dos integrantes con cantidades conocidas y verifica que no se mezclen datos.

## Verificaciones obligatorias

Desde la raíz:

```powershell
dotnet build backend\ClandbusERPIntegration\ClandbusERPIntegration\ClandbusERPIntegration.csproj -c Release
npm --prefix frontend\clandbus-dashboard run build
npm --prefix frontend\clandbus-dashboard test -- --watch=false
```

Verificación manual mínima:

- sin sesión no se muestran datos privados;
- iniciar y cerrar sesión funciona;
- Resumen muestra información del usuario seleccionado;
- Casos y Tareas corresponden al mismo integrante;
- Productividad cambia completamente al cambiar de persona;
- las respuestas atrasadas no sustituyen al último integrante seleccionado;
- las tareas vencidas excluyen tareas completadas o canceladas;
- reiniciar el backend solicita iniciar sesión de nuevo;
- ningún secreto aparece en `git diff` ni `git status`.

## Diagnóstico rápido

### El puerto 7004 está ocupado

Existe otra instancia del backend. No cambies el puerto como primera solución porque el frontend utiliza `https://localhost:7004`. Detén la instancia duplicada y conserva una sola.

### El frontend muestra ceros

Comprueba, en este orden:

1. sesión activa;
2. backend disponible;
3. última sincronización correcta;
4. versión del endpoint de Acumatica;
5. compañía y permisos;
6. `OwnerMappings` y nombre seleccionado;
7. existencia del Excel más reciente para tareas.

### Error 401

La sesión en memoria expiró o el backend fue reiniciado. Inicia sesión nuevamente.

### Error 500 al cambiar de integrante

Revisa la salida del backend y determina qué endpoint falló. No reutilices datos del perfil anterior como respaldo. La interfaz debe limpiar el perfil anterior mientras consulta el nuevo.

### Certificado HTTPS local

Comprueba el certificado de desarrollo:

```powershell
dotnet dev-certs https --check
```

Si falta, solicita autorización antes de confiar o modificar certificados de la computadora.

### Las tareas no aparecen

Comprueba que el archivo sea `.xlsx`, esté en `App_Data/TaskImports`, tenga los encabezados esperados y sea el archivo más reciente por fecha de modificación.

## Flujo recomendado para cambios

```powershell
git switch desarrollo
git pull origin desarrollo
git status --short
```

Primero consulta `git remote -v`. En una clonación hecha desde GitLab, `origin` será GitLab. En la copia de trabajo del propietario pueden existir dos remotos: `origin` para GitHub y `gitlab` para GitLab. No cambies, renombres ni agregues remotos sin autorización explícita y sin confirmar antes la URL completa.

Después:

1. reproduce el problema;
2. modifica únicamente los archivos necesarios;
3. compila frontend y backend;
4. ejecuta las pruebas relacionadas;
5. valida manualmente el flujo afectado;
6. revisa que no haya secretos o datos operativos;
7. crea una rama de trabajo cuando el cambio no sea trivial;
8. integra mediante Merge Request en GitLab o Pull Request en GitHub, según el remoto acordado.

No promociones cambios a `main` sin revisión.

## Archivos que debes consultar

- [`README.md`](README.md): descripción, instalación y uso general.
- [`backend/ClandbusERPIntegration/README.md`](backend/ClandbusERPIntegration/README.md): API y endpoints.
- [`frontend/clandbus-dashboard/README.md`](frontend/clandbus-dashboard/README.md): pantallas y comandos Angular.
- [`SECURITY.md`](SECURITY.md): tratamiento de secretos, sesiones y datos.

## Criterio de finalización

La configuración está terminada solamente cuando:

- backend y frontend compilan;
- ambos servicios están disponibles;
- el usuario puede iniciar sesión;
- una sincronización finaliza correctamente;
- casos y tareas contienen información esperada;
- el selector no mezcla integrantes;
- no existen secretos ni archivos operativos preparados para commit;
- se entrega al usuario un resumen claro de rutas, procesos activos y cualquier limitación restante.

