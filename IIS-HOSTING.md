# IIS HOSTING (Windows Server)

1. Install IIS with ASP.NET 4.8 and .NET Framework 4.8. Install the **Visual C++ 2015-2022 x64 redistributable** (needed by QuestPDF/SkiaSharp native code).
2. Publish: Visual Studio -> Publish -> profile `IIS-Release-FileSystem` (no credentials), or `msbuild SMC.BhoomiMalmatta.Web\SMC.BhoomiMalmatta.Web.csproj /restore /p:Configuration=Release /p:DeployOnBuild=true /p:PublishProfile=IIS-Release-FileSystem`.
3. Copy the published folder to e.g. `D:\inetpub\SMC`.
4. **Application pool:** .NET CLR version **v4.0**, Managed pipeline **Integrated**, **Enable 32-Bit Applications = False**, Identity = a dedicated domain/gMSA account (`ApplicationPoolIdentity` also works with Windows Authentication to a local SQL Server only if granted a login).
5. **Site:** create a site/application bound to HTTPS with a valid certificate (certificates are never part of the source). Anonymous Authentication enabled (the application does its own Forms authentication).
6. **SQL Server:** grant the pool identity a login mapped as a user in `SMC_BhoomiMalmatta` with `db_datareader` + `db_datawriter` only (no `db_owner`, no DDL).
7. **Folders:** create `D:\SMC_Data\Uploads` and `D:\SMC_Data\Logs` outside the site folder. Grant the pool identity **Modify** on them only. Set `Storage.Root` and `Log.Root` in IIS Manager -> Configuration Editor -> appSettings (or via the Release transform).
8. **Secrets:** SMS credentials as machine environment variables (`SMC_SMS_USERNAME`, `SMC_SMS_PASSWORD`) or `App_Data\secrets.config` with NTFS rights only for the pool identity. Never publish `secrets.config`.
9. **Release transform:** forms/session cookies `requireSSL=true`, `debug` removed, custom errors On, `httpErrors` Custom. The site will not start in release mode without HTTPS cookies.
10. **Health check:** `https://<host>/Health.ashx` and `GET /api/v1/health` (returns database name only, no secrets; 503 when the database is unreachable).
11. **Custom error pages:** `Error.aspx`, `NotFound.aspx`, `AccessDenied.aspx`. Errors are logged under `Log.Root` with a correlation id and no personal data.

Production checklist: HTTPS only, pool = v4.0 Integrated 64-bit, least-privilege SQL login, uploads outside web root, backups of `Storage.Root`, log folder monitored, QuestPDF licence confirmed, ACL SMS parameters verified, Web.config `Sms.Enabled` set deliberately.
