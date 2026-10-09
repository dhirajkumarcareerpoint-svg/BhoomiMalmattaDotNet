/* =====================================================================
   SMC_BhoomiMalmatta - READ-ONLY schema metadata discovery
   ---------------------------------------------------------------------
   * Reads ONLY SQL Server catalog views (sys.*). No business tables are
     queried, so no business records are ever read.
   * Contains SELECT and SET statements only. It changes nothing.
   * Returns 13 result sets in a fixed order (the PowerShell exporter
     depends on this order):
       1 databaseInfo        8 defaultConstraints
       2 schemas             9 indexes
       3 tables             10 computedColumns
       4 columns            11 checkConstraints
       5 primaryKeys        12 triggers
       6 foreignKeys        13 views
       7 uniqueConstraints
   * May be run in SSMS as-is (connect to the database first).
   ===================================================================== */

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

-- Safety guard: refuse to run against any other database.
IF DB_NAME() <> N'SMC_BhoomiMalmatta'
BEGIN
    RAISERROR(N'Wrong database context. Connect to SMC_BhoomiMalmatta and run again. Nothing was executed.', 16, 1);
    SET NOEXEC ON;
END;

-- 1) databaseInfo
SELECT
    DB_NAME()                                              AS database_name,
    @@SERVERNAME                                           AS server_name,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS sql_server_version,
    CONVERT(nvarchar(128), SERVERPROPERTY('Edition'))        AS sql_server_edition,
    d.compatibility_level,
    d.collation_name,
    SYSUTCDATETIME()                                       AS exported_at_utc
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

-- 2) schemas (user schemas only)
SELECT
    s.schema_id,
    s.name AS schema_name,
    USER_NAME(s.principal_id) AS owner_name
FROM sys.schemas AS s
WHERE s.schema_id NOT IN (3, 4)      -- INFORMATION_SCHEMA, sys
  AND s.schema_id < 16384            -- fixed database roles
ORDER BY s.name;

-- 3) tables (name/category/approx row count from catalog metadata, not from table scans)
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    CASE WHEN t.name LIKE 'MR[_]%' THEN 'MR'
         WHEN t.name LIKE 'TR[_]%' THEN 'TR'
         ELSE 'OTHER' END AS table_category,
    t.create_date,
    t.modify_date,
    ISNULL((SELECT SUM(p.rows) FROM sys.partitions AS p
            WHERE p.object_id = t.object_id AND p.index_id IN (0, 1)), 0) AS approx_row_count,
    t.is_memory_optimized,
    t.temporal_type_desc
FROM sys.tables AS t
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
ORDER BY sc.name, t.name;

-- 4) columns (one row per column; flat view used for database-schema.csv)
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    CASE WHEN t.name LIKE 'MR[_]%' THEN 'MR'
         WHEN t.name LIKE 'TR[_]%' THEN 'TR'
         ELSE 'OTHER' END AS table_category,
    c.column_id AS ordinal_position,
    c.name      AS column_name,
    TYPE_NAME(c.user_type_id) AS data_type,
    c.max_length AS max_length_bytes,
    CASE WHEN c.max_length = -1 THEN -1
         WHEN TYPE_NAME(c.system_type_id) IN ('nchar', 'nvarchar') THEN c.max_length / 2
         ELSE c.max_length END AS character_max_length,
    c.precision AS numeric_precision,
    c.scale     AS numeric_scale,
    c.is_nullable,
    c.is_identity,
    CONVERT(bigint, idc.seed_value)      AS identity_seed,
    CONVERT(bigint, idc.increment_value) AS identity_increment,
    c.is_computed,
    cc.definition   AS computed_definition,
    cc.is_persisted AS computed_is_persisted,
    dc.name         AS default_constraint_name,
    dc.definition   AS default_definition,
    c.collation_name,
    CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS is_primary_key,
    pk.key_ordinal AS pk_ordinal,
    CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_key_columns AS fkc
                      WHERE fkc.parent_object_id = c.object_id
                        AND fkc.parent_column_id = c.column_id) THEN 1 ELSE 0 END AS is_foreign_key
FROM sys.tables AS t
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
JOIN sys.columns AS c  ON c.object_id = t.object_id
LEFT JOIN sys.identity_columns AS idc ON idc.object_id = c.object_id AND idc.column_id = c.column_id
LEFT JOIN sys.computed_columns AS cc  ON cc.object_id  = c.object_id AND cc.column_id  = c.column_id
LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN (SELECT i.object_id, ic.column_id, ic.key_ordinal
           FROM sys.indexes AS i
           JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
           WHERE i.is_primary_key = 1) AS pk
       ON pk.object_id = c.object_id AND pk.column_id = c.column_id
WHERE t.is_ms_shipped = 0
ORDER BY sc.name, t.name, c.column_id;

-- 5) primaryKeys
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    kc.name AS constraint_name,
    i.type_desc AS index_type,
    ic.key_ordinal,
    c.name  AS column_name,
    ic.is_descending_key
FROM sys.key_constraints AS kc
JOIN sys.tables  AS t  ON t.object_id  = kc.parent_object_id
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
JOIN sys.indexes AS i  ON i.object_id  = kc.parent_object_id AND i.index_id = kc.unique_index_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.type = 'PK' AND t.is_ms_shipped = 0
ORDER BY sc.name, t.name, ic.key_ordinal;

-- 6) foreignKeys (one row per column pair; used for database-relationships.csv)
SELECT
    fk.name AS foreign_key_name,
    ps.name AS parent_schema,
    pt.name AS parent_table,
    pc.name AS parent_column,
    rs.name AS referenced_schema,
    rt.name AS referenced_table,
    rc.name AS referenced_column,
    fkc.constraint_column_id,
    fk.delete_referential_action_desc AS on_delete,
    fk.update_referential_action_desc AS on_update,
    fk.is_disabled,
    fk.is_not_trusted
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables  AS pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
JOIN sys.columns AS pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables  AS rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
JOIN sys.columns AS rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
WHERE pt.is_ms_shipped = 0
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;

-- 7) uniqueConstraints
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    kc.name AS constraint_name,
    i.type_desc AS index_type,
    ic.key_ordinal,
    c.name  AS column_name
FROM sys.key_constraints AS kc
JOIN sys.tables  AS t  ON t.object_id  = kc.parent_object_id
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
JOIN sys.indexes AS i  ON i.object_id  = kc.parent_object_id AND i.index_id = kc.unique_index_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.type = 'UQ' AND t.is_ms_shipped = 0
ORDER BY sc.name, t.name, kc.name, ic.key_ordinal;

-- 8) defaultConstraints
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    c.name  AS column_name,
    dc.name AS constraint_name,
    dc.definition,
    dc.is_system_named
FROM sys.default_constraints AS dc
JOIN sys.tables  AS t  ON t.object_id  = dc.parent_object_id
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
JOIN sys.columns AS c  ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE t.is_ms_shipped = 0
ORDER BY sc.name, t.name, c.column_id;

-- 9) indexes (one row per index column; includes PK/unique-backing indexes)
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    i.name  AS index_name,
    i.type_desc AS index_type,
    i.is_unique,
    i.is_primary_key,
    i.is_unique_constraint,
    i.is_disabled,
    i.filter_definition,
    ic.key_ordinal,
    ic.index_column_id,
    c.name  AS column_name,
    ic.is_descending_key,
    ic.is_included_column
FROM sys.indexes AS i
JOIN sys.tables  AS t  ON t.object_id  = i.object_id
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
LEFT JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
LEFT JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE t.is_ms_shipped = 0 AND i.type > 0
ORDER BY sc.name, t.name, i.name, ic.is_included_column, ic.key_ordinal, ic.index_column_id;

-- 10) computedColumns
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    c.name  AS column_name,
    cc.definition,
    cc.is_persisted
FROM sys.computed_columns AS cc
JOIN sys.tables  AS t  ON t.object_id  = cc.object_id
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
JOIN sys.columns AS c  ON c.object_id = cc.object_id AND c.column_id = cc.column_id
WHERE t.is_ms_shipped = 0
ORDER BY sc.name, t.name, c.column_id;

-- 11) checkConstraints
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    ck.name AS constraint_name,
    c.name  AS column_name,
    ck.definition,
    ck.is_disabled,
    ck.is_not_trusted
FROM sys.check_constraints AS ck
JOIN sys.tables  AS t  ON t.object_id  = ck.parent_object_id
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
LEFT JOIN sys.columns AS c ON c.object_id = ck.parent_object_id AND c.column_id = ck.parent_column_id
WHERE t.is_ms_shipped = 0
ORDER BY sc.name, t.name, ck.name;

-- 12) triggers (names only; definitions are NOT exported)
SELECT
    sc.name AS schema_name,
    t.name  AS table_name,
    tr.name AS trigger_name,
    tr.is_disabled,
    tr.is_instead_of_trigger
FROM sys.triggers AS tr
JOIN sys.tables  AS t  ON t.object_id  = tr.parent_id
JOIN sys.schemas AS sc ON sc.schema_id = t.schema_id
WHERE tr.parent_class = 1 AND t.is_ms_shipped = 0
ORDER BY sc.name, t.name, tr.name;

-- 13) views (names only; definitions are NOT exported)
SELECT
    sc.name AS schema_name,
    v.name  AS view_name,
    v.create_date,
    v.modify_date
FROM sys.views AS v
JOIN sys.schemas AS sc ON sc.schema_id = v.schema_id
WHERE v.is_ms_shipped = 0
ORDER BY sc.name, v.name;

SET NOEXEC OFF;
