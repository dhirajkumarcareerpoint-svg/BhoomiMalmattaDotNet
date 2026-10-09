# TEST-REPORT

Date: 3 October 2026. Environment: **Linux sandbox** (Mono 6.8 + Roslyn from the .NET SDK used only as a C# 7.3 compiler against the .NET Framework 4.8 *reference assemblies*). This is not Windows, not Visual Studio 2022, not IIS, and there was no SQL Server.

## 1. Automated tests - actual result
**144 tests, 144 passed, 0 failed** (final run on the delivered source tree).

| Test class | Tests | Covers |
|---|---:|---|
| SubmissionTests | 23 | required fields, date/mobile/e-mail/GST/PIN validation, locked state/district, zone 1-8, document requirements, server-side recalculation, client amount rejected |
| WorkflowServiceTests | 17 | JE->OS->AC path, role/zone enforcement, stale stage, duplicate payment, idempotent confirmation, concurrency conflict, history append |
| PricingTests | 16 | inclusive days, area, fixed, sq.ft., daily, officer-entered rate when master rate is NULL, rounding |
| WorkflowMachineTests | 15 | every valid transition, invalid transitions rejected, finalized applications locked, reopen rules |
| HistoryAndNumberingTests | 15 | hash-chained history, tamper detection, number formats, collision skipping |
| FileAndAccessTests | 15 | extension/MIME/signature/size, path traversal, double extensions, document access policy, citizen token |
| DatabaseSafetyTests | 9 | only `SMC_BhoomiMalmatta` accepted; attach/other names refused; secrets redacted |
| StorageAndCsvTests | 8 | storage path safety, CSV escaping |
| SmsTests | 7 | templates, suppression rules, failure does not break the transaction |
| AuthTests | 6 | login service with a **fake password verifier** (real bcrypt was not exercised): role/zone returned, generic failure message, lockout and expiry, inactive/unknown role refused, audit |
| ArchitectureTests | 6 | all 5 projects target v4.8 and are classic (non-SDK); no forbidden packages/technologies; no destructive DB API or SQL in source; initializer disabled, no migrations; no secrets in config |
| DocumentAndCertificateTests | 5 | document/certificate rules |
| MappingAccuracyTests | 2 | all 28 tables / 577-2 columns of `database-schema.json` mapped exactly (names, lengths, nullability, precision, identity, row version, keys) |

The MSTest tests were executed with a **local MSTest-compatible runner** (attributes `TestClass`, `TestMethod`, `DataTestMethod`, `DataRow`, `TestInitialize`; `Assert` and `CollectionAssert` methods). The test source is real MSTest 2.2.10 source; Visual Studio will use the real framework.

## 2. Compilation checks - actual result
All five projects were compiled **strictly from the `<Compile Include>` lists inside their own `.csproj` files**, with `-langversion:7.3`, against .NET Framework 4.8 reference assemblies: Models, Business, Data, Web, Tests -> **0 errors, 0 warnings**.
This check found and fixed a real defect: the Data project file had omitted its 8 repository files, no project included `AssemblyInfo.cs`, and the Web `.csproj` was missing. All were corrected.

## 3. Other validations - actual result
* Every project: `TargetFrameworkVersion v4.8`, `LangVersion 7.3`, classic project format. No `net5-9`, `netcoreapp`, `netstandard` target anywhere.
* Packages: EntityFramework 6.4.4; Microsoft.AspNet.WebApi.Core/WebHost 5.2.9; Newtonsoft.Json; BCrypt.Net-Next; QuestPDF; MSTest. A scan of all source/config files found no ASP.NET Core, MVC/Razor, EF Core, Blazor, React/Next.js/Node.js, SQLite/PostgreSQL.
* No `Database.SetInitializer` other than `null`, no `DbMigrator`, `EnsureCreated`, `DropCreate*`, `Database.Create/Delete`, no DDL strings. `Web.config` sets `disableDatabaseInitialization="true"`.
* No passwords, tokens, certificates or SMS credentials in any config, JSON or publish profile (also enforced by a test).
* `run-local.ps1`: PowerShell parse check passed; the database-name guard was run - it accepts `SMC_BhoomiMalmatta` and refuses `master`, `smc_bhoomimalmatta` and `SMC_BhoomiMalmatta_Copy` (a bug in the first version of the guard was found and fixed by this test).

## 4. What was verified using stand-ins (NOT the real packages)
NuGet was unreachable, so for **compile and page-rendering checks only** I wrote throw-away stand-ins that mirror the public signatures used: EntityFramework 6 (`DbContext`, `DbSet`, `EntityTypeConfiguration`, property configuration, transactions), Web API 2 types missing from Mono (`IHttpActionResult`, `[Route]`, `[RoutePrefix]`, `HttpConfiguration`), Newtonsoft.Json, BCrypt.Net and QuestPDF. They are **not part of the delivery** (they live outside the solution).
With these, the pages were rendered through the real Mono ASP.NET runtime in-process: Default, Login, Apply and Status returned HTTP 200 with one header, one form, `site.css` + `app.css` linked, 100+ Marathi characters and no mojibake; NotFound returned 404, AccessDenied 403; `/Department/*` and `/Citizen/*` redirected (302) when not signed in. The health endpoint's 200 in this setup came from the stand-in and is **not** evidence of database connectivity.
Mono's `System.Web` does not know `httpCookies sameSite` (valid on .NET 4.7.2+), so that attribute was removed from a *test copy* of `Web.config` only.

## 5. NOT verified here (needs Windows, Visual Studio 2022, real NuGet restore, IIS, SQL Server)
* A real `msbuild /restore` of the solution and the real package versions (including binding redirects).
* The EF6 model against the live database (the mapping test checks the mapping code against the schema JSON, not a connection).
* Login with the existing bcrypt hashes; the Citizen wizard through submission; JE -> OS -> Assistant Commissioner actions; uploads; payment confirmation; SMS; certificate generation - **none were exercised end to end**.
* Marathi rendering inside generated PDFs (QuestPDF + bundled Noto Sans Devanagari).
* IIS Express / IIS hosting, HTTPS cookies, the Release transform, application pool settings, folder permissions.
* The ACL SMS gateway parameter names (configurable; verify with your ACL manual) and QuestPDF licence eligibility.
* **The live database and the real packages were not tested.**

## 6. Suggested acceptance run on Windows
1. Restore + Release build; Test Explorer -> 144 tests.
2. F5 (IIS Express): open `/`, `/Apply.aspx`, `/Status.aspx`, `/Login.aspx`, `/Health.ashx`.
3. Submit a test application; download the acknowledgement PDF; sign in as JE, OS, Assistant Commissioner in turn; confirm history persists in `ActionHistoryJson` and the audit log.
4. Confirm in SSMS that only data rows changed and that no schema object changed.

## 7. Officer queue update (6 October 2026)
The officer application list was updated after the above report was written. `Applications.aspx` now shows the applicant mobile and submitted date, uses an eight-row server-paged list, and labels stages and payment statuses in Marathi. The list and detail view resolve stored service codes through the existing `MR_CFC_DemandServices` master and display `NameMr`; the service code remains available in `ApplicationDetail.ServiceType` for pricing and workflow logic. The officer list has a Marathi “पहा” detail link and no delete action. Actions remain in the role/stage-guarded detail workflow.

### Actual checks on this update
- Full solution Release build: **passed** using Visual Studio 2022 MSBuild 17.14.51, .NET Framework 4.8 projects, `/restore`, `/m:1`. Restore emitted NU1900 because NuGet vulnerability metadata could not be reached; package restore and compilation completed.
- MSTest: **185 passed, 0 failed** using Visual Studio Test Platform 17.14.0 against `SMC.BhoomiMalmatta.Tests.dll`.
- Browser/IIS page rendering and a live query against SQL Server: **not performed**. The supplied screenshots show `localhost:3000`, and no React/Node project exists in this workspace; this change is in the repository's WebForms department pages.

### Schema inspection and remaining scope
Read `database/database-schema.json` and `database/database-relationships.csv` before editing. The exported schema lists `TR_CFC_DemandApplicationZoneWorkflows` with `ZoneStage`, `PaymentStatus`, `PayableAmount`, `ReceiptNumber`, `ReceiptDate`, `PaymentMode`, `ActionHistoryJson` and `RowVersion`; the only workflow-table CHECK constraint listed is `Zone BETWEEN 1 AND 8`. No PaymentStatus/ZoneStage CHECK constraint appears in this export. Existing `TR_CFC_DemandApplicationZoneDocuments` and `TR_CFC_DemandApplicationDocuments` are linked to the workflow/application tables. This is a static schema export, not a live database inspection. No schema files or database rows were changed.

The separate screenshot-matched dashboard work (inline radio decisions and pending/processed tables) was outside the officer queue update in §7. Applicant payment submission and OS verification are implemented in §8. The schema has no cross-application UTR uniqueness constraint; §8 performs an application-level duplicate check, including a serializable transaction check, without claiming a database unique constraint. No schema changes were made.

## 8. Applicant payment submission and OS verification (6 October 2026)
Implemented the payment-only workflow slice using the existing zone workflow and document tables. The authenticated applicant payment form uses the amount and application identity loaded from the server, configured instructions, an optional QR image path, UTR, payment date, and a signed PDF/JPG/PNG proof up to the existing 5 MiB `FileValidator` limit. Submission uses `PaymentRequired` stage and sets `PaymentSubmitted`; the applicant date and UTR are stored in `ReceiptDate`/`ReceiptNumber`, and the generated relative path, display filename, MIME type and size are stored in a `PaymentProof` zone-document row. The generated storage basename is encoded in `FilePath` because the table has no separate stored-name column.

The workflow history records the payment submission; request-date validation uses the latest `PaymentRequestSent` event timestamp. OS verification and rejection are restricted to OS role and normal zone scoping; verification is unavailable until `PaymentSubmitted`, rejection requires a remark and returns status to `PaymentRejected` without leaving `PaymentRequired`, and a new applicant submission is accepted only from `PaymentRequired` or `PaymentRejected`. Database saves retain the existing RowVersion check. UTR duplicates are checked in the service and again inside a serializable DB transaction because the schema has no unique constraint. Rejected proofs remain as history; a replacement is stored as another document record. OS proof download/inline image preview is authenticated and scoped, and the detail page shows applicant date, verifier, and verification time. The receipt PDF now distinguishes applicant payment date from OS verification time.

### Actual checks
- Full solution Release build: **passed** with Visual Studio 2022 MSBuild 17.14.51, .NET Framework 4.8 projects.
- MSTest: **191 passed, 0 failed** with Visual Studio Test Platform 17.14.0. New cases cover the applicant-ticket binding, date bounds, duplicate UTR refusal, proof-save cleanup on optimistic concurrency conflict, rejection/resubmission, OS-only verification and zone scoping, plus the three-level flow including proof submission.
- No live SQL Server, IIS/browser, or live payment submission was used. The schema findings above are from the supplied JSON/CSV export only.
- At the time of that earlier verification no QR image was available. The follow-up verification below records the subsequently supplied file at `SMC.BhoomiMalmatta.Web/Content/payment/qr.png` and its actual JPEG/JFIF encoding.

## 9. Payment API and officer overview follow-up (6 October 2026)
This follow-up adds OS-only `POST api/v1/applications/{id}/payment/verify` and `/payment/reject`, plus Citizen-only `/payment/submit`. Submit takes the authenticated applicant's application id from the ticket, checks expected stage/RowVersion, caps decoded proof at 5 MiB, and passes proof to the existing extension/MIME/signature validator. Verify/reject are zone-scoped and check posted expected stage/RowVersion against the server copy. The previous `/payment/confirm` endpoint remains as a compatibility route but now also requires expected stage and RowVersion. `Payment.Instructions` now explains QR payment, UTR entry, and proof submission in Marathi. The page omits the QR image element when `Payment.QrImagePath` is blank or the configured file is absent; instructions remain visible.

Coverage inventory: existing tests covered payment date future/before-request, invalid UTR, duplicate UTR across applications, OS rejection remark, applicant ownership and post-rejection resubmission, OS-only/zone-scoped verification, proof cleanup on stale save, and general workflow RowVersion conflicts. This update adds explicit invalid proof-signature, second submission while pending, JE/AC/applicant verification denial, AC approval before payment, and payment-specific stale RowVersion tests. Duplicate submit is rejected by state precondition, while OS confirmation is idempotent for the same recorded reference. PaymentRequired SMS is emitted by the workflow notifier and is asserted by the complete workflow test. Applicant and officer actions are present in the append-only workflow history. `TR_AST_AuditLogs` rows are written for officer actions with a user id; applicant submissions do not produce an audit row because that table requires a numeric employee `UserId` and the applicant ticket has no such user. No audit-schema workaround was introduced.

The officer page now displays role-labelled title, role/zone-scoped KPI counts for JE/OS/AC, a server-paged role pending list for those roles, and a separately paged processed-by-user table; Admin and Staff stay read-only. New `QueryService` methods use `AsNoTracking`, server-derived zone scope, soft-delete filtering, SQL paging, and existing master service-name joins. Site.Master continues to supply the shared user chip/logout; the page does not duplicate them.

### Actual checks and incomplete requirements
- Release build: **passed, 0 errors** with Visual Studio 2022 MSBuild 17.14.51.
- MSTest: **194 passed, 0 failed** with Visual Studio Test Platform 17.14.0. Three new payment tests are included in the total.
- Not implemented in this follow-up: reusable `.ascx` user controls for KPI/filter/grid/pager; inline pending-row decision radios and remarks; server-computed action-option rendering and action postback confirmation/double-click guard; full role-action/filter/paging query test matrix. The existing guarded detail workflow remains the action surface. Consequently this is not the complete requested unified officer workflow.
- Not verified: browser/IIS rendering, all role-specific list results against SQL Server, live SQL execution of the added LINQ queries, QR rendering with a configured image, or real payment API requests. No live submission was made. The schema JSON/CSV was not changed; no database DDL or seed action was run.

## 10. Inline officer decisions and reusable controls (6 October 2026)
The role-aware Applications view now renders a server-derived radio set per eligible pending row, a remark box, optional OS rate entry for services whose master rate is unavailable, the “सादर करा” action, and the “पहा” link. Action options are derived from `ZoneWorkflowMachine.OfficerListActions(route, stage, role, paymentStatus)`. The postback reloads the application, checks Zone route, ticket-derived role and zone, compares the row's expected stage and RowVersion, checks the posted action against the freshly computed options, and then calls the existing workflow runner. Mandatory remarks are checked again by the workflow machine. AC actions require PaymentDone or NotRequired. Legacy, Admin, and Staff rows do not render radios. Client-side enablement, confirmation, and duplicate-click protection improve the interaction; server checks remain authoritative.

The page now uses registered `.ascx` controls for KPI cards, filter bar, pending grid, and reusable independent pagers. Pager summaries render the requested Marathi range and page format. The status/service/search/date filters are still applied server-side, and the authenticated zone scope is supplied by the page ticket.

### Actual checks
- Release solution build: **passed, 0 errors**, using Visual Studio 2022 MSBuild 17.14.51 with the existing .NET Framework 4.8 / C# 7.3 project settings.
- ASP.NET 4.8 precompile of the Web Forms application, including the new `.ascx` controls: **passed** with `aspnet_compiler.exe`.
- MSTest: **207 passed, 0 failed** with Visual Studio Test Platform 17.14.0. This includes payment tests documented in §9, allowed-action combinations for JE/OS/AC and payment states, no radios for Legacy/Admin/Staff, posted disallowed actions, pager boundaries (0, 8, 9, 27), date range/filter normalization, and role/stage/remark restrictions.
- Browser rendering and interactive double-click behavior: **not run**. Live SQL Server execution of the new role queues, counts, filter queries, and audit joins: **not run**. No live application/payment submissions were made.
- Applicant audit row: still unavailable without schema change. `TR_AST_AuditLogs.UserId` is required and an applicant ticket has no employee UserId; no synthetic UserId was supplied.


## Officer page layout and labels update

- Implemented shared Department shell without the sidebar, hamburger, or scrim; Admin/Staff receive only links to existing Department pages. JE/OS/AC receive the shared officer page with one page-level logout control.
- Reused the existing KPI, filter, pending-grid, and pager controls, and added a reusable processed-grid control. Decision options and workflow action codes continue to come from the existing server workflow rules.
- Added English action/payment labels, responsive control styling, an Admin-only advanced filter row, and configurable Devanagari date formatting (`UseDevanagariDigits`, default `true`).
- New regression tests cover Department master layout/link targets, single page logout/no delete action, and date formatting. Existing workflow tests include `JE_Forwards_To_OS`.
- Browser rendering and live SQL Server behavior were not verified.

- Current verification: Release solution build succeeded with 0 errors; ASP.NET 4.8 precompilation succeeded; MSTest result: **207 passed, 0 failed**. Browser rendering and live SQL remain unverified.


## Inline officer actions and list performance update

- Baseline diagnosis from source: decision selection had no `AutoPostBack`; the reload was the Web Forms submit button. The former initial JE/OS/AC bind invoked 11 SQL command-producing LINQ operations on a cold service master cache (pending count + page, service master, five KPI counts, and processed count + page + audit action lookup). This is a source-derived command estimate, not a measured SQL trace.
- Row binding had no per-row database loop: the service master was joined once per query, and processed audit actions were fetched in one batch. `GetDetail` did parse action history on the old fallback action path (not during list row binding); the workflow API zone-access check also called `GetDetail`.
- Baseline/after Stopwatch, SQL command count, and rendered ViewState KB could not be measured: IIS Express on port 58080 and SQL Server on port 1433 were not listening in this environment, and no authenticated browser ticket was available. Those measurements remain **not measured**.
- After changes, the normal officer bind is shaped as one pending page query, one processed page query, and one grouped KPI query; service master data is dictionary-cached for five minutes. Page-query row counts do not trigger per-row queries. EF SQL translation and timing remain unverified without SQL Server.
- Decision checkboxes are client-only single-choice controls. Submit posts the selected action asynchronously with the existing `X-SMC-Request` header and cookie credentials; the API requires application ID, expected stage and row version and returns new workflow status plus KPI counts. No-JavaScript submit fallback remains.
- Index suggestions (text only): consider indexes covering active application filtering/order by `IsDeleted`, `ProcessingRoute`, `Zone`, `SubmittedAt`, `Id`; zone-workflow joins/filtering by `DemandApplicationId`, `IsDeleted`, `ZoneStage`, `PaymentStatus`; and audit lookups by `UserId`, `EntityName`, `EntityId`, `Timestamp`. No indexes or DDL were created/run.
- New tests cover zero/multiple action rejection, cached master lookup with a counting loader, fixed list-query materialization shape, API response fields, read-only grid ViewState, and database-free action computation. Existing tests cover disallowed actions and required remarks.
- Browser behavior, live SQL query count/timing, EF SQL translation of aggregate projections, and rendered ViewState size remain unverified.

- Final verification for this update: Release solution build **0 errors**; ASP.NET 4.8 precompilation passed; MSTest **212 passed, 0 failed**. Browser behavior and live SQL timing/query counts/ViewState size were not verified.

## Unified officer application list (7 October 2026)

`Department/Applications.aspx` now renders one application table and one pager. The separate processed table, count heading, pager wiring, control registration, control files, and project entries have been removed. The list title is `अर्ज {n} अर्ज`. JE, OS, and AC default to `प्रलंबित (माझ्याकडे)`; Admin and Staff default to `सर्व अर्ज` and remain read-only. The status dropdown now selects among mine, all, processed-by-this-user, approved, rejected, and correction views. KPI cards link to their matching view. The single list query is SQL-paged at eight rows, uses the authenticated ticket's zone scope, and resolves service names from the cached master once per query. The processed view filters from audit actions by the signed-in user id. Action controls are still computed for each row through the existing workflow action rules; nonwaiting, legacy, Admin, and Staff rows have no decision controls. The existing AJAX action flow and postback fallback remain in the same grid.

### Actual verification
- Release solution build: **passed, 0 errors**, Visual Studio 2022 MSBuild 17.14.51, .NET Framework 4.8 / C# 7.3.
- MSTest: **214 passed, 0 failed** using Visual Studio Test Platform 17.14.0. Coverage includes role-specific action options, no Legacy/Admin/Staff actions, zero/one/multiple action selection, filter normalization, pager boundaries (0, 8, 9, 27), default view selection for JE/OS/AC versus Admin/Staff, and assertions that the page has one list and that its processed query is scoped to the acting user.
- ASP.NET Framework 4.8 Web Forms precompilation: **passed** with `aspnet_compiler.exe`.
- Project entries match the surviving controls; the removed `OfficerProcessedGrid` has no remaining source or project references.
- Browser rendering and live SQL Server query results/timing: **not run**. The query view behavior is covered by role/action and source/structure tests; no live database was available for result-set verification. No live application submissions were made.

## Officer-only demand application experience (7 October 2026)

JE, OS, and Assistant Commissioner logins now land on `Department/Applications.aspx`; only same-site ReturnUrls for Applications, ApplicationDetail, and Reports are accepted. `Department/Dashboard.aspx` immediately sends those roles to Applications before running any dashboard query. Admin and Staff retain the slim navigation and an application-only dashboard with eight application KPI links. Dashboard counts are grouped from the application/workflow tables. The separate Register dashboard DTO and query method were removed, but the underlying entity and EF mappings remain intact.

The report catalog and report builder return application reports to JE/OS/AC and pass ticket-derived zone scope into the report query. Register/property reports are Admin-only. Property, calculation, recovery, transfer, tender/allocation, and scheme pages are restricted to Admin at both the page guard and Web.config. Register document/certificate downloads (`Download.ashx?type=regdoc|xferdoc|xfercert`) return 403 for non-Admin users. Users and Master Data remain available to Admin/Staff and are now explicitly restricted to those roles in Web.config and page guards. An EndRequest guard converts Forms Authentication login redirects for authenticated users denied by those page rules into HTTP 403. The dashboard API returns only the application-count DTO. No underlying property, lease, recovery, transfer, calculation, scheme, complaint, or other register data is loaded from the officer dashboard/list path.

### Query-count estimate (source analysis; not measured)

A JE/OS/AC login followed by its Applications page is expected to issue one employee lookup during login, one list page query and one grouped KPI query. The service master query occurs once on a cold five-minute cache and is skipped on a warm cache. Thus the expected total is approximately **4 SQL commands cold / 3 warm**, excluding auth/audit infrastructure if it performs its own writes. The officer dashboard redirect performs no dashboard KPI query. The list page query includes its count and page projection in one query for a valid page; an out-of-range page may cause a corrective count/page query. This is a code-path estimate only.

### Actual verification

- Release solution build: **passed, 0 errors**, Visual Studio 2022 MSBuild 17.14.51, .NET Framework 4.8 / C# 7.3.
- MSTest: **220 passed, 0 failed**, Visual Studio Test Platform 17.14.0. Relevant assertions cover login landing and safe ReturnUrls, the officer dashboard redirect, application-only dashboard DTO/markup, role-specific report catalogs, Admin-only register page guards, role-scoped list/KPI query surface, and existing workflow/payment behavior.
- ASP.NET Framework 4.8 Web Forms precompilation: **passed** with `aspnet_compiler.exe` version 4.8.9221.0.
- The added Web Forms query helper and shared controls are listed in the Web project; the solution build and precompile both succeeded.
- Browser rendering, authenticated role-by-role navigation, API HTTP status codes against a running host, and live SQL query counts/results: **not verified**. No live SQL Server or live submissions were used.
- No schema, migration, seed, package, or project changes were made. No ZIP or Git actions were performed.


Follow-up check: Release rebuild, Web Forms precompilation, and all 220 tests passed after adding the EndRequest 403 preservation guard. The authenticated HTTP status remains source/test asserted rather than host-tested; browser and live SQL verification remain pending.

## EF6 officer KPI translation fix (7 October 2026)

### Root cause and query changes

The reported failure is reproducible from the source shape: `GetOfficerDashboardCounts` embedded an `IQueryable` of distinct audit/workflow IDs in a `GroupBy` projection containing several conditional `g.Count(...)` expressions. EF6 failed preparing that command with `System.Data.Entity.Core.EntityCommandCompilationException: "An error occurred while preparing the command definition."`; inner exception: `NotSupportedException: "The nested query is not supported. Operation1='GroupBy' Operation2='MultiStreamNest'"`.

`QueryService.GetOfficerDashboardCounts` now builds the pending predicate in C# for JE, OS, AC, or the general officer role, projects integer flags, and groups only `SUM` values for Total/Pending/Approved/Rejected. Processed IDs are loaded separately as distinct integers from active audit/application/workflow rows, zone scoped, then intersected with the base query in distinct-count queries in batches of 1,000 IDs. No `IQueryable` remains inside the KPI projection. This preserves the prior role, zone, soft-delete, Legacy, payment-submitted, and status counting rules.

The offline EF6 translator also checked the rest of the officer list query surface. `SearchOfficerApplications` previously used a nested `actedIds.Contains` in its processed filter and a total-count scalar in its page projection; both now live in shared query builders and translated successfully without changes to the selection logic. `GetDashboard` retains its conditional aggregate query; its SQL generation also passed offline translation. Report grouping is performed after materializing report rows, not in an EF projection. No other unsupported nested projection pattern was found in the inspected officer path.

### Generated KPI aggregate SQL (JE, zone scope 3)

Generated by the offline EF6 SQL Server 2012 provider test. The query has a fixed number of SELECT wrappers, per-row CASE flags, and SUM aggregates; it has no correlated COUNT subqueries or per-row nested query.

```sql
SELECT
    [GroupBy1].[K1] AS [C1],
    [GroupBy1].[A1] AS [C2],
    [GroupBy1].[A2] AS [C3],
    [GroupBy1].[A3] AS [C4],
    [GroupBy1].[A4] AS [C5]
FROM ( SELECT
        [Project1].[K1] AS [K1],
        SUM([Project1].[A1_0]) AS [A1],
        SUM([Project1].[A2_0]) AS [A2],
        SUM([Project1].[A3_0]) AS [A3],
        SUM([Project1].[A4_0]) AS [A4]
        FROM ( SELECT
                1 AS [K1],
                [Project1].[C1] AS [A1_0],
                [Project1].[C2] AS [A2_0],
                [Project1].[C3] AS [A3_0],
                [Project1].[C4] AS [A4_0]
                FROM ( SELECT
                        1 AS [C1],
                        CASE WHEN ((N'Zone' = [Extent1].[ProcessingRoute]) AND (N'JEPending' = (CASE WHEN ([Extent2].[Id] IS NOT NULL) THEN [Extent2].[ZoneStage] WHEN ([Extent3].[Id] IS NOT NULL) THEN [Extent3].[Stage] END))) THEN 1 ELSE 0 END AS [C2],
                        CASE WHEN (N'Approved' = [Extent1].[Status]) THEN 1 ELSE 0 END AS [C3],
                        CASE WHEN (N'Rejected' = [Extent1].[Status]) THEN 1 ELSE 0 END AS [C4]
                        FROM [dbo].[TR_CFC_DemandApplications] AS [Extent1]
                        LEFT OUTER JOIN [dbo].[TR_CFC_DemandApplicationZoneWorkflows] AS [Extent2]
                          ON ([Extent2].[IsDeleted] <> 1) AND ([Extent1].[Id] = [Extent2].[DemandApplicationId])
                        LEFT OUTER JOIN [dbo].[TR_CFC_DemandApplicationWorkflows] AS [Extent3]
                          ON ([Extent3].[IsDeleted] <> 1) AND ([Extent1].[Id] = [Extent3].[DemandApplicationId])
                        WHERE ([Extent1].[IsDeleted] <> 1) AND ([Extent2].[Zone] = @p__linq__0)
                ) AS [Project1]
        ) AS [Project1]
        GROUP BY [K1]
) AS [GroupBy1]
```

The same translation assertion runs for JE, OS, and AC predicates. Additional generated-query assertions cover the dashboard aggregate, processed-ID selection and count, pending list, filtered list, processed list, and paged list.

### Failure handling and login query count

The application list loads first. A KPI exception is caught once for that request, logged through `ErrorLogger` with the correlation ID, and shown as zero KPI counts with the Marathi refresh notice; the already-loaded list remains available. A fake-query test confirms one KPI attempt and one failure callback.

The successful login path no longer reads the same user a second time to update `LastLoginAt`; `UserStore.RecordLogin` performs a parameterized update by the authenticated user ID. The existing lockout and login audit calls remain. Source-estimated SQL commands for login plus the Applications first page: 3 login commands (user lookup, LastLoginAt update, success audit insert), plus 1 list query, 1 KPI aggregate, 1 processed-ID query, and one processed-count query per 1,000 distinct processed IDs. A cold service-master cache adds one query; a warm cache adds none. Estimate: **7 + processed-ID batches cold; 6 + batches warm**. This is code analysis, not a live SQL measurement.

### Actual verification

- Release solution build: **passed, 0 errors**, Visual Studio 2022 MSBuild 17.14.51, .NET Framework 4.8 / C# 7.3.
- MSTest: **223 passed, 0 failed**, Visual Studio Test Platform 17.14.0. This includes the new offline SQL translation test and KPI-failure/list-retention test.
- Offline SQL generation: **passed** using `DbModelBuilder` with the existing `SmcDbContext` mappings and SQL Server 2012 provider metadata. The test used an unopened `SqlConnection`; it did not create a database or open a SQL connection.
- ASP.NET Framework 4.8 Web Forms precompilation: **passed** with `aspnet_compiler.exe` 4.8.9221.0.
- Project entries: the new translation test is included in the test `.csproj`; compiled/content project paths were checked for existing files.
- Live SQL Server execution, SQL command timing/count, IIS/browser behavior, and rendered list/KPI UI: **not verified**.

### Index suggestions (text only; no DDL)

Consider indexes for demand applications by active/deleted filtering and page ordering (`IsDeleted`, `Id`, with search/status/service/date columns based on measured filters); zone workflows by `DemandApplicationId`, `IsDeleted`, `Zone`, `ZoneStage`, and `PaymentStatus`; audit rows by `UserId`, `EntityName`, and `EntityId`. Validate candidate order and included columns with the live SQL execution plan before any index change. No indexes or DDL were created or run.

## Applicant status and QR projection update

### Changes covered

- Added a pure applicant status projection for the three level cards, current stage, payment summary, certificate state, safe history events and citizen-visible document metadata. Internal user identity, IP, correlation and hash fields are not part of the projection. AC return remarks and internal-only history events are omitted.
- Updated the citizen dashboard to render the level stepper, correction/payment state, submitted payment details, local history dates and an approved-only certificate link. Existing correction and payment submission handlers remain in place.
- `Status.aspx` now accepts only the trimmed application number and issues a Citizen ticket bound to the matched application. Unknown/invalid failures share the generic Marathi message; lookups are limited to 30 per IP per 10 minutes, with a 200 ms delay for failed lookups. Confirmation displays the application number and acknowledgement download only.
- Added a status API route that returns the same applicant DTO using only the application identity in the encrypted citizen ticket.
- QR configuration supports an existing virtual path and an absolute physical path. Blank/missing settings hide the image. The physical-path handler ignores request parameters and only serves the configured PNG/JPEG file.

### Verification performed

- Debug solution build: **passed, 0 errors**, .NET Framework 4.8 / C# 7.3.
- MSTest: **231 passed, 0 failed, 0 skipped**. The suite retains applicant workflow projection assertions for workflow states, correction history, payment form vs read-only records, certificate gating, AC return privacy, Legacy read-only projection, DTO privacy, QR path cases, and date digit formatting. Number-only lookup, ticket binding, masking, rate limiting and confirmation/PDF markup checks are also covered.
- ASP.NET Framework 4.8 Web Forms precompilation: **passed** using `aspnet_compiler.exe` 4.8.9221.0.
- Checked Compile/Content paths in the Business, Web and Tests project files; no missing entries were reported. Added new Business/test/Web files to their project lists.
- No schema, migration, package or project was added; no database or live submission was used.

### Not verified

- Browser rendering and the access-code-to-ticket flow were not exercised in a browser.
- Live SQL Server lookups, rate-limit persistence, certificate download and real payment submission were not run.
- No QR file had been supplied at the time of this earlier test run; QR behavior was tested using path-resolution assertions rather than an actual image request. The default setting was blank at that point.
- The anonymous `POST /api/v1/applications/status` accepts `ApplicationNumber` only, applies the per-IP throttle, issues the bound Citizen ticket on success and returns the same applicant-safe DTO. Authenticated citizen forms and downloads still require the ticket and compare its application id with the requested record.


## Application-number-only applicant tracking (2026-10-07)

- Removed the access-code request from the status page/API and application confirmation. The acknowledgement PDF now directs applicants to use the application number. `ApplicantAccessTokenHash` continues to be populated for existing schema compatibility; the value is no longer requested by applicants.
- Existing recovery/OTP implementation and the `AccessCodeRecovery` SMS event/template path remain dormant: `RecoverAccess.aspx` immediately redirects to the number-only status page. Submission SMS sends application number and applicant details only; no access code/token is inserted into the submission SMS. SMS templates were not modified.
- Status page and API apply 30 lookups per IP per 10 minutes. Failures use the same generic Marathi response and failed lookups incur a 200 ms delay. Citizen tickets are bound to the matched application id.
- Applicant DTO mapping masks mobile and email and excludes PAN/identity, officer identity, IP, hashes, correlation ids and physical file paths. Acknowledgement, receipt and certificate are linked through `Download.ashx`; applicant downloads require the matching Citizen ticket.
- Debug solution build: **passed, 0 errors** (.NET Framework 4.8 / C# 7.3). MSTest: **231 passed, 0 failed, 0 skipped**.
- ASP.NET Framework 4.8 Web Forms precompilation: **passed** with `aspnet_compiler.exe` 4.8.9221.0.
- Browser rendering, browser ticket issuance, live SQL Server lookups and actual download requests were **not verified**.


## Officer application detail modal and link repair (2026-10-07)

- Fixed both list links (application number and View) to use `ResolveUrl("~/Department/ApplicationDetail.aspx?id=" + UrlEncode(id))`; the resolved page is `/Department/ApplicationDetail.aspx`, not the nested `Department/Controls/` path. No other `.ascx`, `.aspx`, or `.cs` relative `ApplicationDetail.aspx` links were found by the source scan test.
- Added `GET /api/v1/applications/{id}/officer-view`, authorized for the existing Admin/JE/OS/AssistantCommissioner/Staff set. It checks the signed-in ticket role and zone, retrieves only non-deleted applications, and returns an explicit DTO without PAN/identity, hashes or storage paths. Staff/Admin and Legacy are read-only. Modal content is inserted with DOM `textContent`; document preview/download links use the authorized/audited `Download.ashx` handler.
- The page opens the accessible modal without navigation when JavaScript is available. It has focus restoration/trapping, Escape/X/backdrop close, loading/error states and a mobile layout. Without JavaScript, the same links lead to the full detail page.
- Code-analysis SQL estimate per modal GET: 1 joined command with a warm service cache (application, active workflows, application and zone documents, and Legacy audit rows); the result is deduplicated and workflow history is parsed in memory for that application only. A cold five-minute service-master cache adds one master query. The join can return repeated rows across related document/history records, which are deduplicated in memory. No live SQL command count/timing was measured. Opening the detail does not change list row binding/query work.
- Build: **passed, 0 errors** (.NET Framework 4.8 / C# 7.3). MSTest: **235 passed, 0 failed, 0 skipped**. ASP.NET Framework 4.8 precompilation: **passed** with `aspnet_compiler.exe` 4.8.9221.0. All Compile/Content entries in the five project files resolve to files on disk.
- Added mapper/privacy/read-only/history/document-handler/link-scan/modal-markup tests. Endpoint response tests use an in-memory fake query: an eligible JE receives a DTO and another-zone JE receives 404. Real browser ticket-cookie handling and SQL data execution were not exercised.

### Not verified

- Browser rendering, modal focus/keyboard behavior, fallback navigation, and actual document previews/downloads were **not verified in a browser**.
- Live database endpoint execution, query plan, and SQL timing/count were **not verified**; the SQL count above is from code analysis.

## Applicant service/sub-service display and payment QR update (2026-10-07)

### Step 0: existing vocabulary and mapping

- `TR_CFC_DemandApplications.BusinessType` is nullable `nvarchar(40)` and `OtherBusinessType` is nullable `nvarchar(max)`. `BusinessType` stores the selected master `Code`; `OtherBusinessType` stores applicant free text for the `Other` choice. `database/database-relationships.csv` declares no FK from these fields to the business-type master.
- `MR_CFC_DemandBusinessTypes` contains `Id`, `Value`, `Code`, `Name`, `NameMr`, `SortOrder`, `IsActive`, and `IsDeleted`. The existing reader is `MasterDataRepository.GetBusinessTypes`; the added `GetBusinessTypeMap` uses the same five-minute `ServiceMasterCache` as the service dictionary.
- `MiniGaala` and `MajorGaala` are in `MR_CFC_DemandServices` (service names are `NameMr` values for Mini Gaala and Major Gaala). They are services, not business-type rows.
- `GanpatiIdolStall` is in `MR_CFC_DemandBusinessTypes`. Diwali choices in that same master are `DiwaliFaralStall` and `DiwaliFirecrackerStall`; there is no generic `DiwaliStall` code. The generic `FirecrackerStall` row also exists in the business-type master.
- The vocabulary has no `MR_AST_MasterData` value section containing these categories. No values were invented or added.

### Changes and verification

- Added `ServiceDisplay.Get(serviceCode, businessTypeCode, otherText)` backed by cached active service and business-type dictionaries, plus `ApplicantStatusViewMapper` for the applicant DTO. Resolution prefers `NameMr`, then `Name`, applicant text for `Other`, and finally the saved code. The resolver is used for applicant status summary/API DTO, history heading, officer list sub-service line, officer detail business type, and acknowledgement/application PDF Core rows. Stored workflow/pricing codes are unchanged.
- The payment form puts the configured QR and download link beside the form on wide screens and above it on mobile. It renders only for `PaymentRequired` and `PaymentRejected` resubmission. Blank, missing, or invalid-type files show an amber notice and log a reason once per hour with a correlation id only. The handler serves only the configured PNG/JPEG, ignores request path parameters, supports an attachment query flag, and sends no-cache headers. Applicant instructions are a numbered `ApplicantText` list; default `Payment.Instructions` was updated to the same four steps.
- Corrected the applicant page's Marathi payment-status caption, applied the digit formatter to every displayed date and day count, and removed the duplicated Current stage prefix from its value. Shared DTO date/pager formatter encoding defects were fixed because existing tests asserted Devanagari output.
- Release solution build with Visual Studio 2022 MSBuild: **passed, 0 errors** (.NET Framework 4.8 / C# 7.3).
- MSTest: **237 passed, 0 failed, 0 skipped**. Added real `ServiceDisplay.Resolve` and applicant DTO assertions for Mini/Major Gaale, Ganpati, both Diwali entries, `Other`, unknown code and null business type, plus assertions for QR states, PDF/list use, instructions and date formatting. Existing QR path tests exercise virtual and physical paths, blank/missing paths, and traversal rejection.
- ASP.NET Framework 4.8 Web Forms precompilation: **passed** with `aspnet_compiler.exe` 4.8.9221.0. `ServiceDisplay.cs` is listed in the Web project; test changes are in a listed test file.
- No schema, project, package, migration, seeding, database record or QR image was added or changed.

### Not verified

- Browser rendering, responsive layout, and the actual QR download request were **not verified in a browser**.
- No real QR image was configured (the default setting is blank), so actual image bytes/content type were not exercised.
- Live SQL Server execution/cache refresh and existing saved applications were **not verified**. Master resolution tests used in-memory dictionaries.

## Configured applicant payment QR (2026-10-07)

- Step 0 found `SMC.BhoomiMalmatta.Web\Content\payment\qr.png` (76,352 bytes). Its extension is `.png`, but the file begins `FF D8 FF E0 00 10 4A 46` (JPEG/JFIF), not the 8-byte PNG signature, so it has no PNG IHDR dimensions. The image was not changed.
- Added the existing file as a project Content item and configured the single `Payment.QrImagePath` value as `~/Content/payment/qr.png`. The release transform and IIS filesystem publish profile do not exclude this content folder; the profile excludes only `App_Data\secrets.config`. Web.config already allows same-origin images through `img-src 'self' data:`.
- The QR handler remains path-config-only, ignores request path input, is reachable as a same-origin public image, returns 404 for missing/unsupported files, and sends no-cache headers. It detects PNG/JPEG bytes and returns the matching MIME type; for the current JPEG bytes at `qr.png`, it returns `image/jpeg`. The payment UI renders the handler with `ResolveUrl`, accessible alt text, and a missing-file notice. Admin-only health JSON includes only one of the three non-path QR states; anonymous output remains unchanged.
- Updated setup instructions and troubleshooting for `Content\payment\qr.png`, publishing and cache refresh. No image was moved, renamed, created or overwritten.
- Added assertions for the actual asset and project/config mappings, MIME signature handling, extension rejection, diagnostic states and Admin-only health-field wiring.

### Not verified

- Release solution build: **passed, 0 errors** (.NET Framework 4.8 / C# 7.3, Visual Studio 2022 MSBuild 17.14.51).
- MSTest: **238 passed, 0 failed, 0 skipped**. The new QR test checks the real repository image and project/config entries; actual JPEG/JFIF MIME detection for its `.png` path; valid temporary PNG responses for virtual and physical configured paths; blank/missing/unsupported cases; all three diagnostic states; and Admin-only health-field wiring.
- ASP.NET Framework 4.8 Web Forms precompilation: **passed** (`aspnet_compiler.exe` 4.8.9221.0). The generated precompile directory was removed after verification so it does not affect the project-file architecture test.

### Not verified

- Browser rendering and the actual HTTP behavior of the handler and health API were **not verified in a browser**. Handler behavior was exercised through its shared in-memory/file-backed response helper; health role visibility was checked by source assertions, not an HTTP host.
- IIS publish output, live SQL Server behavior, and scanning the code with a phone were **not verified**.

## Payment verification → automatic AC forward → AC approve → मंजुरी पत्र (2026-10-08)

### Changes covered
- `ZoneWorkflowMachine`: `OSConfirmPayment` (only from `PaymentRequired` + `PaymentSubmitted`) now chains `OSForwardToAC`; the outcome carries two steps ("Payment Done" PaymentRequired→OSPending, "Forwarded to AC" OSPending→AssistantCommissionerApprovalPending). Manual `OSForwardToAC` is unchanged in rule terms: only `OSPending` with `NotRequired` or (legacy-state) `PaymentDone`. `ACApprove` remark is optional. New `WorkflowPolicy` (`Workflow.AcAllowReturnReject`, default `false`) hides and server-rejects `ACReturn` / `ACReject` with `WORKFLOW_FORBIDDEN` and a Marathi message.
- `ZoneWorkflowService`: one `Save` per command with one hash-chained history entry and one audit row per step (same timestamp, actor, IP, correlation id). After commit: SMS `PaymentDone` + `AssistantCommissionerNotification` (verify), `ApplicationApproved` (approve), then letter generation; SMS / letter failures are logged through the new `IErrorLog` port and never undo the transition. Repeated verify with the same UTR → `WasAlreadyApplied`; a different UTR → `PAYMENT_DUPLICATE`.
- `ZoneWorkflowUpdate.Audit` became `Audits` (list); `ZoneWorkflowStore.Save` adds every row in the same transaction.
- `CertificateService`: approver name from the workflow row, sanitized relative path, new `OpenForDownload` (citizen ticket of that application or an officer role; generates the letter lazily when an approved application has none or the file is missing). `Download.ashx?type=cert` uses it. The applicant dashboard also retries a missing letter on page load.
- `QueryService`: one `OfficerPendingPredicate` / `WherePendingFor` used by the KPI, the "mine" list and `SearchPendingForRole`. OS: `OSPending` or `PaymentRequired`+`PaymentSubmitted`. AC: AC-pending with `PaymentDone`/`NotRequired`.
- Applicant view-model: after verify L2 "Approved / Payment Done", Payment Status "Payment Done", Next Stage "Assistant Commissioner Approval", L3 "Pending" (the "Awaiting forwarding by OS" text was removed). Approved: "Application Status: Approved" + "मंजुरी पत्र डाउनलोड करा", or "मंजुरी पत्र तयार होत आहे" with a refresh hint.
- Officer UI: OS pills "पेमेंट पडताळणी प्रलंबित" / "अर्जदाराकडून पेमेंट प्रलंबित"; modal "Preview proof" label and certificate link; list certificate link for approved rows; AC single purple "Approve" button with optional remark behind "Add remark", confirm/success texts from `OfficerText` rendered on the row (AJAX path unchanged: row removed, KPI counters updated from the API response).

### Existing tests changed (expected behaviour intentionally changed)
- `WorkflowServiceTests.Full_Three_Level_Flow_Keeps_Complete_Immutable_History_And_Notifies`: no separate `OSForwardToAC` call; ACApprove without remark; SMS sequence now `PaymentRequired, PaymentDone, AssistantCommissionerNotification, ApplicationApproved`.
- `WorkflowServiceTests.Payment_Confirmation_Is_Idempotent_And_Detects_Duplicates`: a different UTR after `PaymentDone` now returns `PAYMENT_DUPLICATE` (was `WasAlreadyApplied`).
- `WorkflowMachineTests.Officer_List_Action_Options_Are_Stage_Role_And_Payment_Specific`: AC options are `[ACApprove]` by default; the three previous options only with `WorkflowPolicy(true)`.
- `WorkflowMachineTests.AC_FinalApproval_RequiresRemark_And_Finalizes` → `AC_FinalApproval_Remark_Is_Optional_And_Finalizes`.
- `CitizenStatusMapperTests.PaymentStates_ShowFormOnlyWhenPaymentIsRequiredOrRejected`: the legacy OSPending+PaymentDone card no longer carries "Awaiting forwarding by OS".
- `CitizenStatusMapperTests.ApplicantStatusAndPdfSource_UseSubServiceQrStateAndMarathiDateFixes`: **pre-existing failure, not caused by this change.** The saved run `TestResults\HP_LAPTOP-O3GR2KBV_2026-10-07_17_57_37.trx` shows 237/238 with this test failing, although the previous section of this report states 238 passed. It searched for `onerror="this.style…` while the C# source contains the escaped literal `onerror=\"this.style…`; the expected literal was corrected. No product code changed for it.
- `OfficerQueryTranslationTests`: extended (not relaxed) with OS/AC "mine", KPI and paged SQL for the changed predicates.
- New file `PaymentVerificationAndApprovalTests.cs` (listed in the test project): 42 executed test cases including data rows (29 + 9 + 4 across three classes) covering verify/forward atomicity and hash chain, unpaid/role/zone/stale/concurrency rejections, idempotency and UTR conflict, SMS failure, manual-forward matrix, AC policy on/off, optional approve remark, letter generation/failure/lazy download/access, OS option matrix, officer modal texts, single-button markup, predicate semantics and KPI counts, applicant view states and applicant-safe history.

### Actual verification
- Release solution build (`MSBuild.exe SMC.BhoomiMalmatta.sln /restore /p:Configuration=Release /m:1`, Visual Studio 2022): **passed, 0 errors**, .NET Framework 4.8 / LangVersion 7.3 in all five projects.
- MSTest (`vstest.console.exe`): **280 passed, 0 failed, 0 skipped** (`TestResults\HP_LAPTOP-O3GR2KBV_2026-10-08_11_03_01.trx`).
- ASP.NET Framework 4.8 Web Forms precompilation: **passed** (`aspnet_compiler.exe` 4.8.9221.0, output written to a temporary folder outside the repository and deleted).
- Every `Compile`/`Content` entry in the five project files exists on disk, and no `.cs/.aspx/.ascx/.ashx/.js/.css/.master` file is unlisted.
- Offline EF6 SQL generation for the changed predicates: **passed** (no connection opened).
- No schema, table, migration, package or project was added.

### Not verified
- **Browser:** the single Approve button, "Add remark" toggle, confirm dialog, row removal / KPI update and the modal certificate link were **not run in a browser**; they are covered only by source assertions and the server-side unit tests.
- **Live SQL Server:** the two-audit-row save, RowVersion conflict on the real table and the new predicates against real data were **not executed against SQL Server** (fake store and offline SQL generation only).
- **Real PDF rendering:** `PdfGenerator.Certificate` with the real Marathi fonts was **not executed**; tests use a fake PDF generator. The Marathi layout of the letter is unchecked.
- **Real SMS:** no gateway call was made; whether `PaymentDone` / `CertificateAvailable` templates exist in `MR_CFC_SmsTemplates` was not checked (without a template the SMS is recorded as Suppressed).
- IIS publish output was not checked.

## Citizen wizard reduced to three steps (2026-10-08)

### Step 0 findings (before the change)
| Field (removed step "अर्जदाराची माहिती") | Column (schema JSON) | Used downstream | Required for the app to work? |
| --- | --- | --- | --- |
| अर्जदाराचे पूर्ण नाव | `ApplicantName` nvarchar(200) **NOT NULL** | validator (required), SMS variable, history actor name, PDFs, officer list/search/modal, applicant view, CSV report | **Yes** |
| मोबाईल क्रमांक | `Mobile` nvarchar(10) **NOT NULL** | validator `^[6-9]\d{9}$`, SMS recipient (`ApplicationSubmitted` etc.), access-recovery OTP (`GetRegisteredMobile`), officer list/search/modal, PDFs, masked in applicant view | **Yes** |
| ई-मेल आयडी | `Email` nvarchar(200) **NOT NULL** | validator (format only when present), PDFs, officer modal, masked in applicant view; stored `""` when empty | No (column needs `""`) |
| अर्जदाराचा प्रकार | `ApplicantType` nvarchar(max) NULL | PDFs, officer modal/detail page | No |
| आधार/ओळखपत्र क्रमांक | `IdentityNumber` nvarchar(max) NULL | validator (12 digits when present), PDFs, officer detail page (not modal/applicant view) | No |
| PAN क्रमांक | `PanNumber` nvarchar(max) NULL | validator (format when present), PDFs, officer detail page | No |
| GST क्रमांक | `GstNumber` nvarchar(15) **NOT NULL** | validator (format when present), PDFs, officer modal/detail page; stored `""` when empty | No (column needs `""`) |

No duplicate/lookup logic uses e-mail, applicant type, identity, PAN or GST; the CSV report uses only `ApplicantName`. The public JSON API (`POST api/v1/applications`) still accepts the optional fields. It was **not changed** because it is not part of the wizard.

### Changes
- `Apply.aspx`: 3 views/steps, 3 circles, headings `१./२./३.`, bar starts at 33.33%. Name, mobile and optional e-mail moved to an "अर्जदार" block at the top of step 2, with the same validators and messages. Applicant type, Aadhaar/identity, PAN and GST inputs were removed. Validation groups are now g1 (service), g2 (applicant + address + dates/area), g3 (zone, location, declaration).
- New `Services\ApplyWizard.cs` holds the step titles/count, validation-group and progress mapping, field→step mapping and the server "Next" check (`BlockingErrors`). `Apply.aspx.cs` uses it. Focus moves to the first field of the new step. The review lists name, mobile and e-mail only.
- Collected values for removed fields: `ApplicantType = "वैयक्तिक"`, `IdentityNumber = PanNumber = GstNumber = null`, so the store writes NULL / NULL / `""`. The validator is unchanged: none of these were required, and formats are checked only when a value is present. `SubmissionData` keeps the properties because the API and an existing test still use them.
- `DemandApplicationStore`: the entity mapping moved unchanged into `ToApplicationEntity` (internal) so it can be tested against the schema.
- `PdfGenerator` acknowledgement/application rows: Aadhaar/PAN/GST are printed only when a value exists, so older applications still show their stored values.
- Zone and location stay on step 3, as before ("do not move anything else"), and are validated there.

### Tests
- Existing tests changed: **none**. All 280 previous tests pass unchanged, including pricing, numbering and workflow.
- New `ApplyWizardTests.cs` (listed in the test project): 36 executed cases. They cover:
  - exactly 3 views, headings and circles in order, and no removed heading, `४.`, `st4`/`v4`/`g4` or old step arithmetic;
  - the applicant block at the top of step 2, with all step-2 validators in g2;
  - field→step mapping for 1..3, plus validation group and progress for each step;
  - server "Next" on step 2 blocked by invalid name, mobile, e-mail, address or PIN, allowed when valid, and not blocked by step-3 fields;
  - a wizard-shaped submission without e-mail, Aadhaar, PAN, GST or applicant type, checked against all 35 NOT NULL non-identity columns from `database-schema.json` (none null) and the schema-safe values;
  - server rejection of a missing name, an invalid mobile and an invalid e-mail;
  - no PAN/Aadhaar/GST/applicant-type markup or code;
  - conditional PDF rows.

### Actual verification
- Release solution build (Visual Studio 2022 MSBuild): **passed, 0 errors**, .NET Framework 4.8 / LangVersion 7.3.
- MSTest: **316 passed, 0 failed, 0 skipped** (`TestResults\HP_LAPTOP-O3GR2KBV_2026-10-08_11_18_21.trx`).
- Web Forms precompilation (`aspnet_compiler.exe` 4.8.9221.0): **passed** (output in a temporary folder, deleted).
- All five project files: every `Compile`/`Content` entry exists, and no source file is unlisted.

### Not verified
- **Browser rendering** of the three-step wizard: layout, progress bar, focus movement and client-side validators were **not run in a browser**. They are covered only by markup assertions.
- **A real submission against SQL Server** was **not executed**. NOT NULL safety was checked against the schema JSON and the entity mapping, not by an INSERT.
- Real PDF output with the new conditional rows was not generated.

## Applicant details behind `Apply.CollectApplicantDetails` + zone/location on step 2 (2026-10-08)

### Step 0: what an empty ApplicantName / Mobile / Email did before this change
| Place | Behaviour with empty values (before) |
| --- | --- |
| `SubmissionValidator` | **Rejected**: name required, mobile had to match `^[6-9]\d{9}$`. E-mail was already optional. |
| `SubmissionData` / `Normalize` / `DemandApplicationStore` | `Normalize` trims to `""`, and the store wrote `Email ?? ""`. `ApplicantName`/`Mobile` were written as given, so a NULL would only fail if data bypassed `Normalize` (EF `IsRequired` → exception). |
| `POST api/v1/applications` | Same validator → 422 without name/mobile. |
| SMS (`DemandSubmissionService`, workflow, certificate) | `SmsService` already records **Suppressed, "No recipient mobile number"** and sends nothing for an empty mobile. Template variable `ApplicantName` renders blank. AC role SMS uses user mobiles and is unaffected. No crash. |
| History (`ActionByUserName` for "Application Submitted") | Would be **blank** (`""`). |
| Acknowledgement / application / certificate PDFs (`Core`) | Name/mobile/e-mail rows printed **blank** (`Row` printed `v ?? "—"`, so `""` stayed empty). |
| Receipt PDF | "अर्जदार" row **blank**. |
| Officer list | Name and mobile cells **blank**. Search by number still worked; name/mobile `Contains` simply doesn't match. |
| Officer modal DTO | `ApplicantName`/`Mobile` `""` (the JS showed `-`). Detail page rows **blank**. |
| Applicant status page | Name/mobile rows shown **blank**; `MaskMobile`/`MaskEmail` returned `""` (no crash). The e-mail row was already hidden. |
| CSV reports | `CsvWriter.Cell(null/"")` → empty cell, no exception. |
| Duplicate checks | None use name/mobile/e-mail (only the payment UTR is unique-checked). |
| Access recovery by OTP (`RecoverAccess.aspx`) | Would have no mobile to send to, but the page already redirects to `Status.aspx` (disabled). Status lookup uses the application number only. |

Nothing would have crashed after validation. The blockers were the validator and API (rejection); the rest showed blanks.

### Changes
- `Web.config` `Apply.CollectApplicantDetails` (default `false`, added once).
  - `AppServices` passes it to `DemandSubmissionService`, so the wizard and `POST api/v1/applications` follow the same rule.
  - `SubmissionValidator.Validate(…, applicantDetailsRequired)`: when false, name and mobile are not required. A supplied mobile or e-mail is still format-checked.
  - The old 2-argument overload keeps the required behaviour.
- `Apply.aspx`:
  - The "अर्जदार" block is wrapped in `phApplicant` (`Visible` set in `OnInit` from the setting), so with the switch off its inputs, labels and validators are neither rendered nor validated.
  - **झोन** and **जागेचे ठिकाण** moved to step 2 below the PIN (validation group g2). Step 3 contains only review, documents and declaration.
  - Focus on step 2 goes to the name field or the permanent address; on step 3 to the identity file input.
  - Review omits empty applicant lines.
- Storage: `ApplicantName`/`Mobile`/`Email` are always `""` when empty (`?? ""` in the store as well); applicant type `वैयक्तिक`, Aadhaar/PAN NULL, GST `""`.
- History actor falls back to `Citizen` (`DemandSubmissionService.DefaultActorName`).
- Officer list/modal/detail show "—" (`OfficerText.OrDash`).
- Applicant page hides empty name/mobile rows. `MaskMobile` now fully masks values of 4 digits or fewer (before, it revealed them).
- PDFs:
  - `PdfGenerator.ApplicantRows` omits empty name, mobile and e-mail (and Aadhaar/PAN/GST) rows.
  - The receipt omits an empty "अर्जदार" row.
  - Every other blank PDF value now prints "—" (`PdfGenerator.Cell`).
- SMS code unchanged: an empty mobile is suppressed as before.
- SETUP.md documents the setting and the hook for the separate applicant form. No integration was built.

### Existing tests changed (expected behaviour intentionally changed)
All four are in `ApplyWizardTests.cs`, written in the previous change:
- `Applicant_Block_Opens_Step_Two_And_Runs_With_The_Address_Validation_Group` → `Applicant_Block_Opens_Step_Two_Behind_The_Switch_And_Runs_With_The_Address_Validation_Group`. It now also asserts that the block and all its validators sit inside the hidden `phApplicant`, and the new focus targets.
- `Every_Validation_Field_Belongs_To_Step_1_To_3`: rows `(Zone, 2)` / `(Location, 2)` → `(Zone, 1)` / `(Location, 1)`, because the fields moved to step 2.
- `Next_On_Step_Two_Is_Allowed_When_Valid_And_Step_Three_Fields_Do_Not_Block_It`: missing zone/location now **block** step 2 (before: only step 3).
- `Review_And_Pdf_No_Longer_Show_Empty_Identity_Pan_Or_Gst_Rows`: the source-text checks of the old `if (…) Row(…)` lines were replaced by behavioural checks of `PdfGenerator.ApplicantRows`. The rows were refactored into that function.
- No other existing test changed (validator, pricing, numbering, workflow and SMS tests are untouched and pass).

### New tests
`ApplicantDetailsSwitchTests.cs` (listed in the test project) adds 22 executed cases:
- markup with the switch off: no applicant inputs or validators outside `phApplicant`; the first field after it is "कायमचा पत्ता"; the setting is configured once and applied in `OnInit`;
- switch on: name and mobile are required, e-mail stays optional;
- a submission without name, mobile or e-mail satisfies all 35 NOT NULL columns from `database-schema.json`, storing `""` and never NULL;
- a malformed mobile or e-mail is still rejected;
- SMS with an empty mobile: Suppressed with "No recipient mobile number", no gateway call, submission succeeds; the AC role SMS is still sent;
- officer, applicant, PDF-row and CSV builders handle empty values with "—" or by omitting the row;
- the certificate still generates (fake PDF engine);
- history actor is `Citizen`;
- `MaskMobile`/`MaskEmail` on empty and short values (13 data rows);
- zone and location on step 2, validated there, absent from step 3;
- `POST api/v1/applications` (controller called directly with fake services): 201 with the switch off, 422 with `ApplicantName`/`Mobile` errors with it on.

### Actual verification
- Release solution build (Visual Studio 2022 MSBuild): **passed, 0 errors**, .NET Framework 4.8 / LangVersion 7.3.
- MSTest: **338 passed, 0 failed, 0 skipped** (`TestResults\HP_LAPTOP-O3GR2KBV_2026-10-08_11_51_00.trx`).
- Web Forms precompilation (`aspnet_compiler.exe` 4.8.9221.0): **passed** (temporary output deleted).
- All five project files match the files on disk.

### Not verified
- **Browser rendering** of step 2 with the switch off and on (hidden block, zone/location layout, focus, client validators) was **not run**. The switch-off check is a markup/source assertion plus ASP.NET's rule that invisible controls are neither rendered nor validated.
- **A real submission against SQL Server** was **not executed**. NOT NULL safety was checked against `database-schema.json` and the entity mapping.
- **Real PDFs** (QuestPDF with Marathi fonts) were **not generated**. Only the row builders were tested, with a fake PDF engine for the certificate.
- The public API test calls the controller in-process (no HTTP host, no JSON formatter).

## Apply step 3: submit with selected but not yet added files (2026-10-09)

### Problem and change
Pressing "अर्ज सादर करा" with files chosen in the identity/address boxes, but without first pressing "जोडा / बदला", reported both documents as missing, because only files already staged in Session were used.
`btnSubmit_Click` now stages every file currently selected before anything else. It uses the same validation as the buttons: extension, MIME, signature, the 20 MB application-attachment limit, a generated storage name and the session owner key.
- A selected file replaces the staged file of the same type.
- If a selected file is invalid, its labelled validation message is shown instead of the generic one, and the staged file of that type is kept.
- A type with nothing staged and nothing selected gets "<label> जोडणे आवश्यक आहे. कृपया फाईल निवडा.".
- Files are staged even when the declaration checkbox or a later step fails. They stay staged, and "जोडलेली फाईल: <name>" is shown, until a successful submission clears them.

The decision logic is in `SMC.BhoomiMalmatta.Web\Services\ApplyDocumentSelection.cs`, which has no Page or Session dependency and does no logging. The two-step buttons, file validation rules, size limits, storage layout and the submission service are unchanged.

### Changed message (A -> B)
- Missing document at submit: "<label> जोडणे आवश्यक आहे." -> "<label> जोडणे आवश्यक आहे. कृपया फाईल निवडा.". No existing test asserted the old text, so no existing test expectation changed.

### New tests
`ApplyDocumentSelectionTests.cs` adds 11 executed cases:
- nothing staged + both selected: both staged, no "required" error;
- one staged + the other selected: only the selected one staged;
- both staged + none selected: nothing changes;
- an invalid selected file gives the specific message, not the generic one (4 data rows: bad extension, oversize — refused before the content is read, bad signature, MIME mismatch);
- an invalid selection keeps the earlier staged file, while a valid file in the other box is still staged;
- nothing staged + nothing selected: two "required" messages with the new wording;
- a selected file replaces the staged file of the same type;
- source wiring: staging runs before the staged files are read and before the declaration check returns; staged files are cleared only after a successful submission; the button handlers and the "जोडलेली फाईल" line are unchanged.

### Actual verification
- Release solution build (Visual Studio 2022 MSBuild): **passed, 0 errors, 0 warnings**, .NET Framework 4.8 / LangVersion 7.3.
- MSTest: **358 passed, 0 failed, 0 skipped** (this total also includes the 9 payment-proof resubmission tests added on 2026-10-08).
- Web Forms precompilation (`aspnet_compiler.exe` 4.8.9221.0): **passed**; output went to a temporary folder outside the project.
- All five project files match the files on disk.

### Not verified
- **Browser behaviour** was **not run**: choosing files, pressing "अर्ज सादर करा", the error text, the "जोडलेली फाईल" line after a failed later step, and re-showing the step.
- **A real upload** (multipart post through IIS / IIS Express, `FileUpload.HasFile`, request size limits) was **not performed**. The page code that reads the upload controls is covered only by the source-wiring assertions; the decision and validation logic is covered by unit tests.
- **A real submission against SQL Server** was **not executed** for this change.

## Properties page rebuilt (Department/Properties.aspx, 2026-10-09)

### Change
- `Department/Properties.aspx` is now a dedicated Admin-only page and no longer uses `ModulePage`. It has the title "मालमत्ता" and the subtitle. A filter card holds the search box, the category and status dropdowns and "+ नवीन नोंद".
- The table has 7 columns, right-aligned through one CSS variable (`.property-main{--prop-align:right}`), with status pills and a pager of 10 rows. It has loading, empty and error states and no ViewState.
- New, edit and view open in a dialog, driven by `Scripts/properties.js`. The script holds no option values.
- The page talks to a new API, `api/v1/properties`: list, `{id}`, POST, PUT `{id}`, DELETE `{id}` (soft delete), `next-code` and `options`.
  - Access is Admin-only, through `AdminOnlyApiAttribute` plus a re-check in every action.
  - Writes need the existing CSRF header.
  - Create, edit and delete write audit rows with codes and field names only.
- All dropdowns come from the database through one provider, `PropertyMasterOptions`. It reads the MR_AST_MasterData groups `PropertyCategory` and `PropertyStatus` plus the values in use, and caches them for 5 minutes. On failure it returns empty lists plus a warning.
- Wards and zones come from the stored values in natural order. Zone1–Zone8 are added only when the stored zones already use that format.
- Reports and the transfer-certificate PDF now take their labels from the same provider.
- The dark site header is hidden on this page. The single admin link row carries the user name, with the role printed once through `DisplayName.WithRole`, and the only "बाहेर पडा". `DisplayName.WithRole` is also used on the Dashboard.
- Proposed master data: `database\proposed-changes\property-master-data.sql`. It is data-only and idempotent, and it was **not run** against any database.

### Actual verification
- Release solution rebuild (VS 2022 MSBuild): **0 errors**. There were 2 MSB3061 warnings because the native `libSkiaSharp.dll` / `libHarfBuzzSharp.dll` files in `Web\bin\x64` were locked by a running IIS Express worker; there were no compiler warnings.
- MSTest (`vstest.console.exe`): **392 passed, 0 failed, 0 skipped** (`TestResults\HP_LAPTOP-O3GR2KBV_2026-10-09_11_15_51.trx`). Of these, 34 are new: `PropertyRegisterTests` (22) and `PropertiesPageTests` (12).
- Web Forms precompilation (`aspnet_compiler.exe` 4.8.9221.0): **passed**. The output went to a temporary folder outside the project.
- All five project files match the files on disk.

### What the new tests cover
- Labels: category and status labels come from the master rows, falling back to the English name, then the key. An unknown code is shown raw.
- Rupee format: 0 → ₹0; 1200 → ₹1,200; 120000 → ₹1,20,000; 1234567.5 → ₹12,34,568.
- Next code: gaps are not reused, soft-deleted codes count, a category with no prefix gives an empty suggestion or learns the prefix from existing codes, and width is kept.
- Unique code: soft-deleted rows are included, with both messages.
- Validation: field-level Marathi messages for required fields, numbers, negatives, more than 2 decimals, too-large values and lengths. Input is trimmed.
- Categories: every category of the SQL script is accepted and an unknown one is rejected. A legacy category or status is kept on edit.
- List: category, status and search filters (including Zone), paging, page-size limits and label mapping.
- Edit conflict: a stale version returns CONFLICT with the 409 message.
- Soft delete: the IsDeleted, DeletedAt and DeletedBy fields are set and the row is kept. Delete is refused for each of the 9 dependent tables.
- Audit rows for create, update and delete contain no occupant or address values. An audit without a user is refused.
- Provider:
  - master rows come first in display order, followed by the values in use;
  - Zone1–Zone8 are added only when the stored zones use that format;
  - optional ward and zone master groups are used when configured;
  - the cache lasts 5 minutes and Invalidate clears it;
  - a failure gives empty lists plus warnings and is not cached.
- Offline EF6 SQL translation:
  - the list query searches all 8 columns, selects only the displayed columns and pages with OFFSET;
  - the master-data query filters on IsActive and IsDeleted and orders by DisplayOrder;
  - the distinct-values queries translate;
  - all 9 dependent-count queries translate.
- `SoftDeleteSql` is an UPDATE, and the store has no DELETE or Remove call.
- The SQL file has 11 exact rows, uses NOT EXISTS on GroupName + Key, and contains no DDL, DELETE, UPDATE or DB_NAME guard. It has the purpose, ordering and rollback header.
- Access:
  - every API action returns 403 for JE, OS, AC and Staff, both through the attribute and through the in-action re-check;
  - Admin passes and an anonymous request gets 401;
  - the page's RequiredRoles is Admin only, alongside the Web.config and Global.asax rules.
- Markup and script:
  - no category or status codes or labels appear in the markup, the JS or the code-behind;
  - the table header order and the dialog field order match the reference;
  - the dialog has role="dialog" and aria-modal, closes on Esc, traps Tab, returns focus, asks the dirty-form question and has no nested form.
- Header: the header is hidden only on this page and exactly one logout is visible there. The officerHome lines are unchanged.
- Name and role: the role is printed once.
- The other five register pages still use ModulePage.

### Not verified
- **Browser rendering and behaviour** was **not run**. This covers the layout against the reference screenshots, the dialog, the focus trap, Esc, the debounce, the pager, "+ नवीन मूल्य टाका", the responsive layout and the pill colours.
- **Real database reads, writes and deletes** were **not executed**:
  - inserts and updates in the serializable transactions;
  - the unique index on PropertyCode;
  - the soft-delete UPDATE;
  - the dependent counts against real foreign keys;
  - the audit rows in the audit-log table.
  The data-access code is covered only by the offline SQL translation and by fakes.
- **The live master data** was **not inspected**. The SSMS results block of the request was left as "<paste>". Until `property-master-data.sql` is reviewed and run, the page shows the stored codes (for example "MajorGaale", "Rikamy") as labels and offers only the categories already in use.
- **The HTTP pipeline** was **not exercised**: routing, the JSON formatter, the CSRF handler on a real request and IIS 403 handling. The controller and the attribute were called in-process.
