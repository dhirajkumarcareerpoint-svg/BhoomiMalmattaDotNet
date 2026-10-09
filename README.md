# SMC Bhoomi Malmatta Management System (.NET Framework 4.8)

Solapur Municipal Corporation - land and property demand applications. Citizen portal and Department portal.

**Stack (only):** .NET Framework 4.8, ASP.NET Web Forms, ASP.NET Web API 2, Entity Framework 6, SQL Server, C# 7.3, Visual Studio 2022, IIS / IIS Express.
Not used: ASP.NET Core, MVC, Razor, Blazor, EF Core, React/Next.js/Angular/Vue, Node.js, SQLite, Docker.

## Solution

| Project | Purpose |
|---|---|
| `SMC.BhoomiMalmatta.Web` | Web Forms pages (Marathi UI), Web API 2 under `/api/v1/`, Forms authentication, file handling, PDF, SMS gateway |
| `SMC.BhoomiMalmatta.Business` | Pricing, validation, workflow state machine, application numbering, history, SMS/payment/file rules |
| `SMC.BhoomiMalmatta.Data` | EF 6 `DbContext`, 28 mappings of the **existing** tables, repositories, database safety guard |
| `SMC.BhoomiMalmatta.Models` | Entities, DTOs, constants (vocabulary taken from the live database) |
| `SMC.BhoomiMalmatta.Tests` | 144 MSTest tests |

## Existing database (never modified)
Database `SMC_BhoomiMalmatta` on `localhost`, Windows Authentication. No migrations, no initializer, no new tables, no schema change.
The application only reads and writes ordinary **data rows** (new applications, workflow rows, audit rows, SMS rows, and it increments
`MR_CFC_ApplicationNumberConfigurations.NextNumber`). See `DATABASE-MAPPING.md`.

## Workflow
`Citizen -> JE -> OS -> Assistant Commissioner -> Final result`. New applications use `ProcessingRoute = Zone`.
Existing `Legacy` applications are read-only. Every transition is validated on the server (role, zone, stage, RowVersion concurrency).
History is stored append-only (hash-chained) in `ActionHistoryJson`; audit rows go to `TR_AST_AuditLogs`.

## Quick start
See `SETUP.md` (Visual Studio 2022) or run `run-local.ps1`. IIS deployment: `IIS-HOSTING.md`. Security notes: `SECURITY.md`.
Test results and exactly what was and was not verified: `TEST-REPORT.md`. Styling: `CSS-INTEGRATION.md`.

## Portals
* Citizen: home, new demand application (3-step wizard: १. सेवेची माहिती → २. पत्ता व जागा incl. zone and location → ३. कागदपत्रे व सादरीकरण; applicant name/mobile/e-mail only when `Apply.CollectApplicantDetails=true`; Aadhaar/PAN/GST/applicant type are not asked), status lookup with application number + access code, timeline, payment status, acknowledgement / receipt / certificate PDFs, correction resubmission.
  There is no citizen account table in the database, so "citizen login" = application number + the access code issued at submission (only its SHA-256 hash is stored).
* Department (roles Admin, JE, OS, AssistantCommissioner, Staff): dashboard, application list (search, filters, zone, paging), details, documents, workflow actions, audit log, reports + CSV, users (read-only), master data (read-only).
