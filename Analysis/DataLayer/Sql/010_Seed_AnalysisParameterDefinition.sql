-- 010_Seed_AnalysisParameterDefinition.sql
MERGE dbo.AnalysisParameterDefinition AS T
USING (VALUES
 ('MISUNDERSTOOD_PCT','% consultas mal interpretadas',0,100,1),
 ('EMPATHY_AI','Empatía IA',1,10,1),
 ('EMPATHY_MENTOR','Empatía Mentor',1,10,1),
 ('SENTIMENT_STUDENT_START','Sentimiento inicio',1,10,1),
 ('SENTIMENT_STUDENT_END','Sentimiento fin',1,10,1),
 ('EMOTION_AVG','Promedio emocional',1,10,1),
 ('WARMTH_AI','Calidez IA',1,10,1),
 ('WARMTH_MENTOR','Calidez Mentor',1,10,1)
) AS S(Code,Name,MinScore,MaxScore,IsActive)
ON (T.Code = S.Code)
WHEN MATCHED THEN UPDATE SET
  T.Name=S.Name, T.MinScore=S.MinScore, T.MaxScore=S.MaxScore, T.IsActive=S.IsActive, T.UpdatedAt=SYSUTCDATETIME(), T.UpdatedBy='seed'
WHEN NOT MATCHED THEN
  INSERT (Code,Name,MinScore,MaxScore,IsActive,CreatedAt,CreatedBy)
  VALUES (S.Code,S.Name,S.MinScore,S.MaxScore,S.IsActive,SYSUTCDATETIME(),'seed');
