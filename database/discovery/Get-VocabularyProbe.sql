/* SMC_BhoomiMalmatta - READ-ONLY lookup-vocabulary probe.
   Reads ONLY: (a) distinct values + counts of code/status-type columns, and
   (b) master/configuration rows (service rates, services, business types,
   application-number configuration). It never selects names, mobile numbers,
   e-mails, addresses, PAN/identity numbers, remarks, files or password hashes.
   Password hashes are reduced to a format category such as bcrypt-like. */
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
IF DB_NAME() <> N'SMC_BhoomiMalmatta'
BEGIN
    RAISERROR(N'Wrong database context. Nothing was executed.', 16, 1);
    SET NOEXEC ON;
END;

-- 1) vocabulary
SELECT N'Users.Role' AS source, CAST(x.[Role] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[MR_AST_Users] AS x GROUP BY x.[Role]
UNION ALL
SELECT N'Users.ZoneNumber' AS source, CAST(x.[ZoneNumber] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[MR_AST_Users] AS x GROUP BY x.[ZoneNumber]
UNION ALL
SELECT N'Users.IsActive' AS source, CAST(x.[IsActive] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[MR_AST_Users] AS x GROUP BY x.[IsActive]
UNION ALL
SELECT N'Apps.Status' AS source, CAST(x.[Status] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[Status]
UNION ALL
SELECT N'Apps.ServiceType' AS source, CAST(x.[ServiceType] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[ServiceType]
UNION ALL
SELECT N'Apps.BusinessType' AS source, CAST(x.[BusinessType] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[BusinessType]
UNION ALL
SELECT N'Apps.PaymentStatus' AS source, CAST(x.[PaymentStatus] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[PaymentStatus]
UNION ALL
SELECT N'Apps.ProcessingRoute' AS source, CAST(x.[ProcessingRoute] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[ProcessingRoute]
UNION ALL
SELECT N'Apps.State' AS source, CAST(x.[State] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[State]
UNION ALL
SELECT N'Apps.District' AS source, CAST(x.[District] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[District]
UNION ALL
SELECT N'Apps.City' AS source, CAST(x.[City] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[City]
UNION ALL
SELECT N'Apps.Taluka' AS source, CAST(x.[Taluka] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[Taluka]
UNION ALL
SELECT N'Apps.Zone' AS source, CAST(x.[Zone] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplications] AS x GROUP BY x.[Zone]
UNION ALL
SELECT N'Workflow.Stage' AS source, CAST(x.[Stage] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplicationWorkflows] AS x GROUP BY x.[Stage]
UNION ALL
SELECT N'Workflow.PaymentStatus' AS source, CAST(x.[PaymentStatus] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplicationWorkflows] AS x GROUP BY x.[PaymentStatus]
UNION ALL
SELECT N'ZoneWorkflow.ZoneStage' AS source, CAST(x.[ZoneStage] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplicationZoneWorkflows] AS x GROUP BY x.[ZoneStage]
UNION ALL
SELECT N'ZoneWorkflow.PaymentStatus' AS source, CAST(x.[PaymentStatus] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplicationZoneWorkflows] AS x GROUP BY x.[PaymentStatus]
UNION ALL
SELECT N'ZoneWorkflow.PaymentMode' AS source, CAST(x.[PaymentMode] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplicationZoneWorkflows] AS x GROUP BY x.[PaymentMode]
UNION ALL
SELECT N'AppDocs.DocumentType' AS source, CAST(x.[DocumentType] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplicationDocuments] AS x GROUP BY x.[DocumentType]
UNION ALL
SELECT N'AppDocs.VerificationStatus' AS source, CAST(x.[VerificationStatus] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_DemandApplicationDocuments] AS x GROUP BY x.[VerificationStatus]
UNION ALL
SELECT N'Sms.EventType' AS source, CAST(x.[EventType] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_SmsEvents] AS x GROUP BY x.[EventType]
UNION ALL
SELECT N'Sms.Status' AS source, CAST(x.[Status] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_CFC_SmsEvents] AS x GROUP BY x.[Status]
UNION ALL
SELECT N'SmsTemplates.EventType' AS source, CAST(x.[EventType] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[MR_CFC_SmsTemplates] AS x GROUP BY x.[EventType]
UNION ALL
SELECT N'SmsTemplates.Language' AS source, CAST(x.[Language] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[MR_CFC_SmsTemplates] AS x GROUP BY x.[Language]
UNION ALL
SELECT N'SmsTemplates.ApprovalStatus' AS source, CAST(x.[ApprovalStatus] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[MR_CFC_SmsTemplates] AS x GROUP BY x.[ApprovalStatus]
UNION ALL
SELECT N'Audit.Action' AS source, CAST(x.[Action] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_AST_AuditLogs] AS x GROUP BY x.[Action]
UNION ALL
SELECT N'Audit.EntityName' AS source, CAST(x.[EntityName] AS nvarchar(400)) AS value, COUNT_BIG(*) AS n FROM [dbo].[TR_AST_AuditLogs] AS x GROUP BY x.[EntityName]
UNION ALL
SELECT N'Users.PasswordHashShape' AS source, CASE WHEN u.[PasswordHash] LIKE N'$2_$%' THEN N'bcrypt-like' WHEN u.[PasswordHash] LIKE N'AQAAAA%' THEN N'aspnet-identity-v3-like' ELSE CONCAT(N'other(len=', LEN(u.[PasswordHash]), N';colon=', CASE WHEN u.[PasswordHash] LIKE N'%:%' THEN 1 ELSE 0 END, N';dot=', CASE WHEN u.[PasswordHash] LIKE N'%.%' THEN 1 ELSE 0 END, N';dollar=', CASE WHEN u.[PasswordHash] LIKE N'%$%' THEN 1 ELSE 0 END, N';eq=', CASE WHEN u.[PasswordHash] LIKE N'%=%' THEN 1 ELSE 0 END, N')') END AS value, COUNT_BIG(*) AS n FROM [dbo].[MR_AST_Users] AS u GROUP BY CASE WHEN u.[PasswordHash] LIKE N'$2_$%' THEN N'bcrypt-like' WHEN u.[PasswordHash] LIKE N'AQAAAA%' THEN N'aspnet-identity-v3-like' ELSE CONCAT(N'other(len=', LEN(u.[PasswordHash]), N';colon=', CASE WHEN u.[PasswordHash] LIKE N'%:%' THEN 1 ELSE 0 END, N';dot=', CASE WHEN u.[PasswordHash] LIKE N'%.%' THEN 1 ELSE 0 END, N';dollar=', CASE WHEN u.[PasswordHash] LIKE N'%$%' THEN 1 ELSE 0 END, N';eq=', CASE WHEN u.[PasswordHash] LIKE N'%=%' THEN 1 ELSE 0 END, N')') END
ORDER BY source, value;

-- 2) serviceRates
SELECT r.[Id], r.[ServiceCode], r.[Rate], r.[Unit], r.[IsActive], r.[IsDeleted] FROM [dbo].[MR_CFC_DemandServiceRates] AS r ORDER BY r.[ServiceCode], r.[Id];
-- 3) services
SELECT s.[Id], s.[Value], s.[Code], s.[Name], s.[NameMr], s.[SortOrder], s.[IsActive], s.[IsDeleted] FROM [dbo].[MR_CFC_DemandServices] AS s ORDER BY s.[SortOrder], s.[Id];
-- 4) businessTypes
SELECT b.[Id], b.[Value], b.[Code], b.[Name], b.[NameMr], b.[SortOrder], b.[IsActive], b.[IsDeleted] FROM [dbo].[MR_CFC_DemandBusinessTypes] AS b ORDER BY b.[SortOrder], b.[Id];
-- 5) numberConfig
SELECT c.[Id], c.[Prefix], c.[YearStrategy], c.[SequenceLength], c.[NextNumber], c.[IsActive], c.[IsDeleted] FROM [dbo].[MR_CFC_ApplicationNumberConfigurations] AS c ORDER BY c.[Id];

SET NOEXEC OFF;
