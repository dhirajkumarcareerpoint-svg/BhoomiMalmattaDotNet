# DATABASE-MAPPING

Entity Framework 6 mappings are generated 1:1 from `database/database-schema.json` (the authoritative discovery output).
Each entity is a plain POCO in `SMC.BhoomiMalmatta.Models/Entities`; each mapping is an `EntityTypeConfiguration<T>` in `SMC.BhoomiMalmatta.Data/Mappings`.

| Entity | Table | Columns | Primary key | Identity | Soft delete | Row version | Computed | Check constraint |
|---|---|---|---|---|---|---|---|---|
| GalaTransferRate | `MR_AST_GalaTransferRates` | 13 | Id | yes | yes |  |  |  |
| MasterData | `MR_AST_MasterData` | 16 | Id | yes | yes |  |  |  |
| Property | `MR_AST_Properties` | 22 | Id | yes | yes |  |  |  |
| User | `MR_AST_Users` | 19 | Id | yes | yes |  |  |  |
| ApplicationNumberConfiguration | `MR_CFC_ApplicationNumberConfigurations` | 14 | Id | yes | yes |  |  |  |
| DemandBusinessType | `MR_CFC_DemandBusinessTypes` | 15 | Id | yes | yes |  |  |  |
| DemandServiceRate | `MR_CFC_DemandServiceRates` | 13 | Id | yes | yes |  |  |  |
| DemandService | `MR_CFC_DemandServices` | 15 | Id | yes | yes |  |  |  |
| PermitConfiguration | `MR_CFC_PermitConfigurations` | 15 | Id | yes | yes |  |  |  |
| PermitTerm | `MR_CFC_PermitTerms` | 12 | Id | yes | yes |  |  |  |
| SmsTemplate | `MR_CFC_SmsTemplates` | 20 | Id | yes | yes |  |  |  |
| AllocationProcess | `TR_AST_AllocationProcesses` | 21 | Id | yes | yes |  |  |  |
| AuditLog | `TR_AST_AuditLogs` | 11 | Id | yes |  |  |  |  |
| Calculation | `TR_AST_Calculations` | 18 | Id | yes | yes |  |  |  |
| Document | `TR_AST_Documents` | 24 | Id | yes | yes |  |  |  |
| GalaTransferDocument | `TR_AST_GalaTransferDocuments` | 15 | Id | yes | yes |  |  |  |
| GalaTransfer | `TR_AST_GalaTransfers` | 26 | Id | yes | yes | Version |  |  |
| LandRegistration | `TR_AST_LandRegistrations` | 21 | Id | yes | yes |  | RecordNumber |  |
| Lease | `TR_AST_Leases` | 22 | Id | yes | yes |  |  |  |
| RecoveryCase | `TR_AST_RecoveryCases` | 21 | Id | yes | yes |  |  |  |
| SchemeApplication | `TR_AST_SchemeApplications` | 21 | Id | yes | yes |  |  |  |
| Complaint | `TR_CFC_Complaints` | 19 | Id | yes | yes |  | RecordNumber |  |
| DemandApplicationDocument | `TR_CFC_DemandApplicationDocuments` | 27 | Id | yes | yes |  |  |  |
| DemandApplication | `TR_CFC_DemandApplications` | 54 | Id | yes | yes |  |  |  |
| DemandApplicationWorkflow | `TR_CFC_DemandApplicationWorkflows` | 34 | Id | yes | yes |  |  |  |
| DemandApplicationZoneDocument | `TR_CFC_DemandApplicationZoneDocuments` | 15 | Id | yes | yes |  |  |  |
| DemandApplicationZoneWorkflow | `TR_CFC_DemandApplicationZoneWorkflows` | 34 | Id | yes | yes | RowVersion |  | CK_ZoneWorkflow_Zone |
| SmsEvent | `TR_CFC_SmsEvents` | 18 | Id | yes | yes |  |  |  |

## Design notes
* **No migrations, no initializer.** `Database.SetInitializer<SmcDbContext>(null)` runs before any use; nothing creates, alters or drops schema.
* **Foreign keys are enforced by SQL Server** (24 FKs, see `database/database-relationships.csv`). EF6 fluent mapping cannot declare a relationship without a navigation property, so relationships are intentionally not re-declared; entities carry the FK scalar columns exactly as in the table.
* **Soft delete:** EF6 has no global query filters; repositories add `!IsDeleted` explicitly.
* **Indexes / unique constraints** exist in the database (unique `ApplicationNumber`, filtered unique `(ApplicationNumber, EventType)` on SMS events, ...) and are relied upon, not re-declared.
* `TR_AST_AuditLogs.UserId` is NOT NULL with an FK to users, so only authenticated-officer events are written there; citizen events live in `ActionHistoryJson`.
* `__EFMigrationsHistory` (left by earlier EF Core tooling) is not mapped and never touched.


## Runtime behaviour on the existing tables (data only, never schema)
* Reads: masters (`MR_CFC_DemandServices`, `DemandServiceRates`, `DemandBusinessTypes`, `SmsTemplates`, `PermitConfigurations`, `PermitTerms`), users, applications, workflow, documents, audit logs.
* Inserts: `TR_CFC_DemandApplications`, `TR_CFC_DemandApplicationZoneWorkflows`, `TR_CFC_DemandApplicationDocuments`, `TR_AST_AuditLogs`, `TR_CFC_SmsEvents`.
* Updates: zone workflow row (guarded by `RowVersion`), the application's `Status`/`PaymentStatus`/`FeeAmount`/`CalculatedRate`, certificate path, `MR_CFC_ApplicationNumberConfigurations.NextNumber` (serializable transaction with `UPDLOCK, HOLDLOCK`).
* Never touched: `ProcessingRoute = Legacy` applications (read-only), `__EFMigrationsHistory`, any DDL.
* Foreign keys are enforced by the database; EF mappings use the scalar key columns. Workflow history lives in `ActionHistoryJson` (no history table was added).
