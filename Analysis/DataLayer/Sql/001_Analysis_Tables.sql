-- 001_Analysis_Tables.sql
IF OBJECT_ID('dbo.AnalysisParameterDefinition', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.AnalysisParameterDefinition ( ... );
END
GO
IF OBJECT_ID('dbo.AnalysisRun', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.AnalysisRun ( ... );
END
GO
IF NOT EXISTS (
  SELECT 1 FROM sys.indexes
  WHERE name = 'IX_AnalysisRun_Week'
    AND object_id = OBJECT_ID('dbo.AnalysisRun')
)
BEGIN
  CREATE UNIQUE INDEX IX_AnalysisRun_Week ON dbo.AnalysisRun(WeekStartUtc, WeekEndUtc);
END
GO
IF OBJECT_ID('dbo.AnalysisObservation', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.AnalysisObservation ( ... );
END
GO
IF OBJECT_ID('dbo.AnalysisSummary', 'U') IS NULL
BEGIN
  CREATE TABLE dbo.AnalysisSummary ( ... );
END
GO

