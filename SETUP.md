# SETUP (Visual Studio 2022)

## Prerequisites
* Windows 10/11 or Server, Visual Studio 2022 with workloads **ASP.NET and web development** and **.NET desktop development**, .NET Framework 4.8 developer pack
* SQL Server (local `localhost`) containing the existing `SMC_BhoomiMalmatta` database; your Windows account needs read/write on data (no DDL rights are needed or used)
* Internet access to nuget.org for the first restore

## Steps
1. Extract the ZIP, open `SMC.BhoomiMalmatta.sln`.
2. Right-click the solution -> **Restore NuGet Packages** (EntityFramework 6.4.4, Microsoft.AspNet.WebApi 5.2.9, Newtonsoft.Json 13.0.3, BCrypt.Net-Next 4.0.3, QuestPDF 2022.12.15, MSTest 2.2.10).
3. Set **SMC.BhoomiMalmatta.Web** as startup project; press F5 (IIS Express, `http://localhost:58080/`).
4. Test Explorer -> Run All (current suite: 316 tests at the latest verification recorded in `TEST-REPORT.md`).

## Citizen application wizard (`Apply.aspx`)
Three steps: **१. सेवेची माहिती** → **२. पत्ता व जागा** (address, taluka, PIN, **झोन**, **जागेचे ठिकाण**, dates and area; preceded by the "अर्जदार" block only when `Apply.CollectApplicantDetails=true`) → **३. कागदपत्रे व सादरीकरण** (review, documents, declaration). "Next" on step 2 validates every step-2 field on the server, so documents can only be uploaded after steps 1–2 are valid; the final submission re-validates everything. Applicant type, Aadhaar/identity, PAN and GST are not asked: new rows store applicant type `वैयक्तिक`, `NULL` identity/PAN and an empty GST. Existing rows are not changed.

### `Apply.CollectApplicantDetails` (Web.config, default `false`)
* `false`: the "अर्जदार" block (name, mobile, e-mail) is not rendered and name/mobile are not required, in the wizard **and** in `POST api/v1/applications`. A mobile or e-mail that is posted anyway is still format-checked and stored. Empty values are stored as `""` in the NOT NULL columns `ApplicantName`, `Mobile`, `Email` (never NULL, never placeholder text). Downstream: officer list/modal/detail show "—"; the applicant page hides the rows; PDFs omit the rows; the history actor is `Citizen`; applicant SMS are not sent and are recorded as Suppressed ("No recipient mobile number"); officer-role SMS are unaffected.
* `true`: previous behaviour (block shown at the top of step 2, name and mobile required, e-mail optional).

### Hook for the separate applicant form
Applicant details are not collected here when `Apply.CollectApplicantDetails=false`; the attached form must supply `ApplicantName`/`Mobile`/`Email` before submission. No integration exists yet. Until then, applications submitted this way have no applicant name or mobile, so the applicant receives no SMS.

## Configuration (`SMC.BhoomiMalmatta.Web\Web.config`)
* `SmcDatabase` connection string: `Server=localhost;Database=SMC_BhoomiMalmatta;Integrated Security=SSPI`. The application **refuses to start** if the database name is not exactly `SMC_BhoomiMalmatta`.
* `Storage.Root` (uploaded documents, outside the web root), `Log.Root` (error log without personal data), `Auth.TimeoutMinutes`, `Numbering.Separator`.
* SMS: `Sms.Enabled=false` by default. Credentials only via environment variables `SMC_SMS_USERNAME` / `SMC_SMS_PASSWORD` or the git-ignored `App_Data\secrets.config` (copy `secrets.config.example`). ACL query-parameter names are configurable (`Sms.Param.*`) - **confirm them against your ACL manual**.
* Payment: manual confirmation by OS (UTR/receipt number). No gateway is enabled.
* `Workflow.AcAllowReturnReject` (default `false`): when `false` the Assistant Commissioner can only **Approve**; "Return to OS" and "Reject" are hidden and the server refuses them (`WORKFLOW_FORBIDDEN`). Set `true` to bring both options back. Restart the app pool after changing it (it is read at start-up).

## Zone workflow (payment → approval → मंजुरी पत्र)
1. JE accepts → OS. OS **Request payment** (amount computed on the server).
2. Applicant pays, then submits UTR, payment date and proof on `Citizen/Dashboard.aspx`. The application returns to the OS list ("mine" view, KPI *pending*) with the pill **पेमेंट पडताळणी प्रलंबित** and only two options: **Verify payment** / **Reject payment** (remark required). Unpaid rows (`PaymentRequired`) show **अर्जदाराकडून पेमेंट प्रलंबित** and are view-only for the OS.
3. **Verify payment** sets `PaymentDone` and, in the same RowVersion-guarded save, moves the application to `AssistantCommissionerApprovalPending` (Status `ApprovalPending`). Two hash-chained history entries ("Payment Done", "Forwarded to AC") and two audit rows are written. There is no separate "Forward" click. Repeating Verify with the same UTR is a no-op success; a different UTR is a conflict.
4. A manual **Forward to Assistant Commissioner** remains only for `NotRequired` payments and for older rows already at `OSPending` with `PaymentDone`.
5. The AC row shows one purple **Approve** button (remark optional via "Add remark"). Approval needs payment `PaymentDone` or `NotRequired`.
6. After the approval commits, the permission letter is generated (see below) and the SMS events `ApplicationApproved` and `CertificateAvailable` are queued. SMS events after verification: `PaymentDone` (applicant) and `AssistantCommissionerNotification` (AC users). Each SMS is sent only if an active template for that event type exists in `MR_CFC_SmsTemplates` (e.g. add a `PaymentDone` template if you want that SMS); SMS problems never undo the workflow.

## Permission letter (मंजुरी पत्र) generation
* Generated by `CertificateService` with the existing `PdfGenerator.Certificate` (permit configuration + permit terms from the database; approver name from the workflow row) right after the AC approval commits.
* Stored under `Storage.Root` (outside the web root) at `certificates/<application number>/certificate.pdf`; the relative path and file name are recorded with the existing `SetCertificate` (`CertificatePath` / `CertificateFileName`).
* If generation fails, the approval is **not** rolled back: the error is written to the error log (`Log.Root`, area `CertificateGeneration`, with the request correlation id) and the letter is generated on the next applicant status-page visit or the first download request (`Download.ashx?type=cert`), so an approved application always leads to a letter.
* Download: applicants only through their own application-number ticket; officers through the same handler (zone-scoped). Always `application/pdf` as an attachment. AC/Admin can regenerate it from `Department/ApplicationDetail.aspx`.
* The PDF uses the bundled Noto Sans / Noto Sans Devanagari fonts from `App_Data\Fonts` (QuestPDF); make sure that folder is published. Marathi rendering of the letter has not been checked visually and should be reviewed on the target server.

## Roles / users
Users already exist in `MR_AST_Users` (bcrypt hashes). The application has no default or hard-coded credentials and does not create users.
Roles: `Admin`, `JE`, `OS`, `AssistantCommissioner`, `Staff` (Staff has no workflow rights).

## Command line
`powershell -NoProfile -ExecutionPolicy Bypass -File .\run-local.ps1 -Build` (verifies the database name, builds with Visual Studio MSBuild, starts IIS Express, prints the URL).
Manual: `msbuild SMC.BhoomiMalmatta.sln /restore /p:Configuration=Release` then run the tests from Test Explorer or `vstest.console.exe`.

## Local HTTP note
`Web.config` has `requireSSL=false` so login works over `http://localhost`. `Web.Release.config` sets `requireSSL=true`, and the application refuses to start with debug off unless it is true.

## Applicant payment QR image

1. In Visual Studio, use **Add > Existing Item** to include the supplied file at `SMC.BhoomiMalmatta.Web\Content\payment\qr.png`; set its **Build Action** to **Content**. The file must be a real PNG, at least 600x600 pixels. This workspace's file has a `.png` extension but JPEG/JFIF bytes; the handler detects the actual format and serves it as `image/jpeg` without modifying the file.
2. In `SMC.BhoomiMalmatta.Web\Web.config`, set the single `Payment.QrImagePath` key to `~/Content/payment/qr.png`.
3. Build and verify the published output contains `Content\payment\qr.png`. On IIS, replace that file in the published `Content\payment` folder without rebuilding the application. Use **Ctrl+F5** after replacing it to bypass browser cache. The QR is served only through `PaymentQr.ashx`; blank/missing/invalid image configuration hides it and shows an amber notice. A static QR does not carry the amount, so the payable amount is displayed beside it.

| Symptom | Check |
| --- | --- |
| Amber notice shown | Confirm `Payment.QrImagePath` and that the configured file exists in the deployed folder. |
| Broken image | Check the handler response and that Content-Security-Policy allows same-origin images (`img-src 'self' data:`). |
| Old QR still shown | Replace the deployed file, then reload with Ctrl+F5. |
| Handler returns 404 | Confirm `Content\payment\qr.png` was included in the published output and its extension/content are a supported PNG or JPEG image. |

## Applicant status access

Applicants track applications using the application number only. Application numbers are sequential and guessable; the status page and status API allow 30 lookups per IP per 10 minutes and use a generic delayed not-found response. A successful lookup issues a short-lived Citizen ticket tied to that application. Reconsider adding a second factor before production exposure.

