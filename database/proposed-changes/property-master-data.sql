/* ============================================================================================================
   property-master-data.sql  -  PROPOSED master data for the Properties page (Department/Properties.aspx)
   ------------------------------------------------------------------------------------------------------------
   PURPOSE
     The Properties page reads every category and status dropdown, label and validation rule from
     dbo.MR_AST_MasterData (groups "PropertyCategory" and "PropertyStatus"), merged with the values already
     stored in dbo.MR_AST_Properties. Without these rows the page still works, but it shows the raw stored codes
     (e.g. "MajorGaale", "Rikamy") and offers only the categories already in use.
     This script adds the 9 property categories and 2 property statuses with their Marathi / English labels.

   WHAT IT DOES
     * Data only: INSERT rows into dbo.MR_AST_MasterData. No DDL, no schema change, no UPDATE, no DELETE.
     * Idempotent: a row is inserted only when no row with the same GroupName + Key exists (active, inactive or
       soft-deleted). Running it again inserts nothing. A row an administrator has edited or deleted is left as is.
     * Key = the code stored in MR_AST_Properties.Category / .Status. MajorGaale, MiniGaale, Rikamy and Bhadyane
       are the codes already present in the live data; the other seven category keys are new codes.
     * All rows: IsActive = 1, IsDeleted = 0, CreatedBy = N'master-data-script', CreatedAt = SYSUTCDATETIME().

   ORDER / HOW TO RUN
     1. Review this file. It is NOT run by the application, the build or any test.
     2. Take a backup (or at least note the current MAX(Id) of dbo.MR_AST_MasterData).
     3. Connect SSMS to the intended database and run the whole script once. It runs in one transaction.
     4. The Properties page caches its dropdown lists for 5 minutes; new labels appear after that
        (or after an application recycle).

   ROLLBACK
     Removes only the rows this script inserted (and only if nothing has changed them since):
       DELETE FROM dbo.MR_AST_MasterData
        WHERE CreatedBy = N'master-data-script'
          AND GroupName IN (N'PropertyCategory', N'PropertyStatus')
          AND UpdatedAt IS NULL;
     Check first that no MR_AST_Properties row uses one of the seven NEW category keys
     (Abhyasika, AdhikrutKhoke, Gaale256, LandFee, OtherMunicipalProperty, SamajMandir, TP3_23); such rows would
     still be shown (with the raw code as the label), so removing the master row only loses the label.
   ============================================================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @rows TABLE
(
    GroupName    nvarchar(100) NOT NULL,
    [Key]        nvarchar(100) NOT NULL,
    MarathiName  nvarchar(200) NOT NULL,
    EnglishName  nvarchar(200) NOT NULL,
    DisplayOrder int           NOT NULL
);

INSERT INTO @rows (GroupName, [Key], MarathiName, EnglishName, DisplayOrder) VALUES
    (N'PropertyCategory', N'MajorGaale',             N'Major गाळे',                          N'Major Gaale',              1),
    (N'PropertyCategory', N'MiniGaale',              N'Mini गाळे',                           N'Mini Gaale',               2),
    (N'PropertyCategory', N'LandFee',                N'Land Fee (भुई भाडे)',                  N'Land Fee',                 3),
    (N'PropertyCategory', N'SamajMandir',            N'समाज मंदिर',                          N'Samaj Mandir',             4),
    (N'PropertyCategory', N'Abhyasika',              N'अभ्यासिका',                           N'Abhyasika',                5),
    (N'PropertyCategory', N'Gaale256',               N'256 गाळे',                            N'256 Gaale',                6),
    (N'PropertyCategory', N'TP3_23',                 N'TP-3/23',                             N'TP-3/23',                  7),
    (N'PropertyCategory', N'AdhikrutKhoke',          N'अधिकृत खोके',                         N'Authorised kiosks',        8),
    (N'PropertyCategory', N'OtherMunicipalProperty', N'इतर भाडेतत्वावरील मनपा मालमत्ता',       N'Other municipal property', 9),
    (N'PropertyStatus',   N'Rikamy',                 N'रिक्त',                               N'Vacant',                   1),
    (N'PropertyStatus',   N'Bhadyane',               N'भाडेतत्वावर दिलेली',                   N'Leased',                   2);

INSERT INTO dbo.MR_AST_MasterData (GroupName, [Key], MarathiName, EnglishName, IsActive, DisplayOrder, CreatedBy, CreatedAt, IsDeleted)
SELECT r.GroupName, r.[Key], r.MarathiName, r.EnglishName, 1, r.DisplayOrder, N'master-data-script', SYSUTCDATETIME(), 0
  FROM @rows AS r
 WHERE NOT EXISTS (SELECT 1 FROM dbo.MR_AST_MasterData AS m WHERE m.GroupName = r.GroupName AND m.[Key] = r.[Key]);

PRINT CONCAT(N'Rows inserted: ', @@ROWCOUNT, N' (0 when the script has already been run).');

COMMIT TRANSACTION;

/* Verification (read-only) */
SELECT GroupName, [Key], MarathiName, EnglishName, DisplayOrder, IsActive, IsDeleted
  FROM dbo.MR_AST_MasterData
 WHERE GroupName IN (N'PropertyCategory', N'PropertyStatus')
 ORDER BY GroupName, DisplayOrder, Id;
