/* Reversión explícita de versión 001. Es destructiva para ProfessionalRecords.
   NO ejecutar sin respaldo y autorización formal. Se entrega como documentación;
   la aplicación no la ejecuta automáticamente. */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.ProfessionalRecords', 'U') IS NOT NULL
    DROP TABLE dbo.ProfessionalRecords;

DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.'
    + QUOTENAME(OBJECT_NAME(parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
FROM sys.default_constraints
WHERE parent_object_id IN (OBJECT_ID('dbo.Cases'), OBJECT_ID('dbo.Tasks'), OBJECT_ID('dbo.SyncRuns'))
  AND COL_NAME(parent_object_id, parent_column_id) = 'UserKey';
EXEC sys.sp_executesql @sql;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Cases_UserKey_CaseNumber_CapturedAt')
    DROP INDEX IX_Cases_UserKey_CaseNumber_CapturedAt ON dbo.Cases;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Tasks_UserKey_ExternalId_CapturedAt')
    DROP INDEX IX_Tasks_UserKey_ExternalId_CapturedAt ON dbo.Tasks;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SyncRuns_UserKey_FinishedAt')
    DROP INDEX IX_SyncRuns_UserKey_FinishedAt ON dbo.SyncRuns;

IF COL_LENGTH('dbo.Cases', 'UserKey') IS NOT NULL
    ALTER TABLE dbo.Cases DROP COLUMN UserKey;
IF COL_LENGTH('dbo.Tasks', 'UserKey') IS NOT NULL
    ALTER TABLE dbo.Tasks DROP COLUMN UserKey;
IF COL_LENGTH('dbo.SyncRuns', 'UserKey') IS NOT NULL
    ALTER TABLE dbo.SyncRuns DROP COLUMN UserKey;

COMMIT TRANSACTION;
