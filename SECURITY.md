# Política de seguridad

Este repositorio es una implementación técnica en evolución. No debe conectarse a datos productivos del ERP sin realizar previamente una revisión de seguridad, arquitectura y permisos.

## Versiones compatibles

Actualmente solo se mantiene la versión más reciente disponible en la rama `main`.

| Versión | Recibe actualizaciones de seguridad |
| --- | --- |
| Rama `main` | Sí |
| Versiones o copias anteriores | No |

## Configuración segura

- Nunca publiques usuarios o contraseñas del ERP, cookies de sesión, datos de clientes, cadenas de conexión privadas ni direcciones internas de la instancia.
- Copia `appsettings.Development.example.json` como `appsettings.Development.json`. El archivo real está excluido de Git.
- Para datos sensibles, utiliza secretos de usuario de .NET o variables de entorno.
- Utiliza certificados HTTPS confiables. No desactives la validación de certificados.
- Emplea una cuenta de Acumatica dedicada, con los permisos mínimos necesarios, y un tenant de pruebas durante el desarrollo.
- No agregues al repositorio los archivos Excel importados ni exportaciones que contengan información operativa.

Ejemplo con secretos de usuario de .NET:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Acumatica:BaseUrl" "https://instancia-ejemplo/"
```

Las credenciales se introducen durante el inicio de sesión. No deben almacenarse en el frontend, la base de datos, el código fuente ni los registros de la aplicación.

## Sesiones y separación de datos

El backend mantiene las sesiones de Acumatica en memoria y las separa mediante una clave de sesión enviada por el navegador. Los datos sincronizados se relacionan con un `UserKey` para evitar que las consultas mezclen información entre usuarios.

Esta protección no sustituye un sistema completo de identidad y autorización. Antes de utilizar el proyecto con múltiples usuarios en producción deben incorporarse, como mínimo:

- autenticación formal de la aplicación;
- autorización por usuario y función;
- almacenamiento seguro y distribuido de sesiones;
- protección contra solicitudes abusivas y limitación de frecuencia;
- auditoría de accesos y operaciones;
- administración centralizada de secretos;
- revisión de los permisos concedidos en Acumatica.

Al reiniciar el backend se pierden las sesiones almacenadas en memoria y el usuario debe volver a iniciar sesión.

## Cómo informar una vulnerabilidad

Informa cualquier vulnerabilidad o posible exposición de datos de manera privada al propietario del repositorio mediante la opción **Security → Report a vulnerability** de GitHub, cuando esté habilitada, o mediante un canal privado acordado con el titular.

Incluye una descripción clara, los pasos para reproducir el problema y su posible impacto. No adjuntes contraseñas, cookies, archivos de clientes ni datos reales del ERP.

No publiques vulnerabilidades, credenciales o información privada en los Issues públicos del repositorio.

## Respuesta esperada

El propietario revisará el reporte, confirmará si el problema puede reproducirse y determinará su prioridad. La publicación de detalles técnicos deberá esperar hasta que exista una corrección o una medida de mitigación adecuada.
