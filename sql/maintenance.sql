-- =========================================================
-- Clandbus Technical Test
-- SQL Maintenance Script
-- Author: Javier Solís
-- =========================================================

/*
    CONTEXTO

    La tabla LoginTrace almacena registros históricos
    de auditoría relacionados con accesos al sistema.

    Debido al crecimiento de la tabla (+5 millones de registros),
    se requiere mantenimiento para:

    1. Eliminar información histórica innecesaria
    2. Optimizar búsquedas frecuentes por:
       - rango de fechas
       - usuario
*/

-- =========================================================
-- DELETE HISTORICAL RECORDS
-- =========================================================

/*
    Eliminación de registros del año 2024
    hacia atrás utilizando el campo [Date].

    Se conservan únicamente registros desde:
    2025-01-01
*/

DELETE FROM LoginTrace
WHERE [Date] < '2025-01-01';


-- =========================================================
-- INDEX OPTIMIZATION
-- =========================================================

/*
    Optimización de búsquedas frecuentes sobre:

    - Username
    - Date

    Escenario esperado:

    SELECT *
    FROM LoginTrace
    WHERE [Username] = @Username
      AND [Date] BETWEEN @StartDate AND @EndDate

    El índice compuesto permite:

    - Reducir scans completos
    - Mejorar búsquedas por usuario
    - Optimizar filtros por rango de fechas
    - Mejorar rendimiento de reportes de auditoría
*/

CREATE NONCLUSTERED INDEX IX_LoginTrace_Username_Date
ON LoginTrace (
    [Username],
    [Date]
);