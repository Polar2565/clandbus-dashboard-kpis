/* Version 001 - aislamiento por usuario y bitácora profesional.
   Ejecutar una sola vez sobre la base Dashboard existente antes de iniciar esta versión.
   No elimina ni modifica información previa. Las filas históricas quedan sin propietario
   y no se muestran a sesiones nuevas hasta que se asigne UserKey explícitamente. */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Cases', 'UserKey') IS NULL
    ALTER TABLE dbo.Cases ADD UserKey nvarchar(320) NOT NULL
        CONSTRAINT DF_Cases_UserKey DEFAULT('');

IF COL_LENGTH('dbo.Tasks', 'UserKey') IS NULL
    ALTER TABLE dbo.Tasks ADD UserKey nvarchar(320) NOT NULL
        CONSTRAINT DF_Tasks_UserKey DEFAULT('');

IF COL_LENGTH('dbo.SyncRuns', 'UserKey') IS NULL
    ALTER TABLE dbo.SyncRuns ADD UserKey nvarchar(320) NOT NULL
        CONSTRAINT DF_SyncRuns_UserKey DEFAULT('');

IF OBJECT_ID('dbo.ProfessionalRecords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfessionalRecords
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProfessionalRecords PRIMARY KEY,
        UserKey nvarchar(320) NOT NULL,
        ActivityType nvarchar(40) NOT NULL,
        RelatedActivityId nvarchar(200) NOT NULL,
        RelatedActivityName nvarchar(500) NOT NULL,
        Result nvarchar(4000) NOT NULL,
        Challenge nvarchar(4000) NOT NULL,
        Solution nvarchar(4000) NOT NULL,
        Learning nvarchar(4000) NOT NULL,
        ImprovementArea nvarchar(1000) NOT NULL,
        [Date] date NOT NULL,
        Notes nvarchar(4000) NOT NULL,
        CreatedAt datetimeoffset NOT NULL,
        UpdatedAt datetimeoffset NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Cases_UserKey_CaseNumber_CapturedAt')
    CREATE INDEX IX_Cases_UserKey_CaseNumber_CapturedAt
        ON dbo.Cases(UserKey, CaseNumber, CapturedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Tasks_UserKey_ExternalId_CapturedAt')
    CREATE INDEX IX_Tasks_UserKey_ExternalId_CapturedAt
        ON dbo.Tasks(UserKey, ExternalId, CapturedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SyncRuns_UserKey_FinishedAt')
    CREATE INDEX IX_SyncRuns_UserKey_FinishedAt
        ON dbo.SyncRuns(UserKey, FinishedAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ProfessionalRecords_UserKey_Date')
    CREATE INDEX IX_ProfessionalRecords_UserKey_Date
        ON dbo.ProfessionalRecords(UserKey, [Date]);

COMMIT TRANSACTION;
