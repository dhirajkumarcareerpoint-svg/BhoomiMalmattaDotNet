using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Reflection;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    /// <summary>EF6 implementation of the property-linked registers. Only inserts/updates rows of existing tables; each change writes an audit row in the same transaction.</summary>
    public sealed class RegisterStore : IRegisterStore
    {
        private static readonly string[] PropertyFields = { "Name", "Ward", "Zone", "Address", "AreaSqFt", "MonthlyRent", "AnnualDemand", "SurveyNumber", "TpNumber", "Status", "CurrentOccupant", "Shera" };
        private static readonly string[] AllocationFields = { "PropertyId", "Method", "NoticeNumber", "PublishDate", "LastDateToApply", "AuctionDate", "ReserveAmount", "HighestBidAmount", "HighestBidderName", "HighestBidderMobile", "Status", "Shera", "PropertyCategory" };
        private static readonly string[] RecoveryFields = { "PropertyId", "LeaseId", "MonthsOverdue", "OutstandingAmount", "Stage", "NoticeNumber", "NoticeDate", "RecoveredAmount", "RecoveryDate", "SealDate", "ReAuctionDate", "Shera", "PropertyCategory" };
        private static readonly string[] SchemeFields = { "PropertyId", "SchemeType", "ApplicantName", "ApplicantMobile", "ApplicationDate", "OriginalOutstanding", "WaivedAmount", "PayableAmount", "Status", "DecisionDate", "ApprovedBy", "Shera", "PropertyCategory" };

        private static Error Err(string code, string msg) { return new Error(code, msg); }
        private static string Cut(string s, int max) { return s == null ? null : (s.Length > max ? s.Substring(0, max) : s); }
        private static string Who(ActorContext a) { return Cut(a.UserName, 100); }

        private static string Val(object v)
        {
            if (v == null) return "";
            if (v is DateTime) return ((DateTime)v).ToString("yyyy-MM-dd");
            if (v is decimal) return ((decimal)v).ToString("0.##");
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, string> Snapshot(object o, string[] props)
        {
            var d = new Dictionary<string, string>();
            foreach (var p in props) d[p] = Val(o.GetType().GetProperty(p).GetValue(o, null));
            return d;
        }

        private static void Copy(object to, object from, string[] props)
        {
            foreach (var p in props) { var pi = to.GetType().GetProperty(p); pi.SetValue(to, pi.GetValue(from, null), null); }
        }

        private static void AddAudit(SmcDbContext db, ActorContext a, string action, string entity, int id, string oldValue, string newValue)
        {
            if (!a.UserId.HasValue) throw new InvalidOperationException("Audit requires an authenticated officer.");
            db.AuditLogs.Add(AuditWriter.ToEntity(new AuditEntry { Action = action, EntityName = entity, EntityId = id, OldValue = oldValue, NewValue = newValue, UserId = a.UserId, UserName = a.UserName, IpAddress = a.IpAddress, TimestampUtc = DateTime.UtcNow }));
        }

        private static string Join(Dictionary<string, string> d, IEnumerable<string> keys) { return string.Join("; ", keys.Select(k => k + "=" + d[k])); }

        /// <summary>Copies editable fields from src to the tracked entity and writes one Update audit row listing only the changed fields.</summary>
        private static void UpdateWithAudit(SmcDbContext db, ActorContext a, object tracked, object src, string[] fields, string entity, int id)
        {
            var before = Snapshot(tracked, fields);
            Copy(tracked, src, fields);
            var after = Snapshot(tracked, fields);
            var changed = fields.Where(f => before[f] != after[f]).ToList();
            if (changed.Count == 0) return;
            AddAudit(db, a, "Update", entity, id, Join(before, changed), Join(after, changed));
        }

        private static void Stamp(object e, ActorContext a, bool isNew)
        {
            var t = e.GetType();
            if (isNew) { t.GetProperty("CreatedBy").SetValue(e, Who(a), null); t.GetProperty("CreatedAt").SetValue(e, DateTime.UtcNow, null); }
            else { t.GetProperty("UpdatedBy").SetValue(e, Who(a), null); t.GetProperty("UpdatedAt").SetValue(e, DateTime.UtcNow, null); }
        }

        private static PagedResult<T> Page<T>(IQueryable<T> ordered, int page, int size)
        {
            page = Math.Max(1, page); size = Math.Min(200, Math.Max(1, size));
            return new PagedResult<T> { Page = page, PageSize = size, Total = ordered.Count(), Items = ordered.Skip((page - 1) * size).Take(size).ToList() };
        }

        // ---- Properties ----
        public PagedResult<Property> SearchProperties(string search, string category, string status, int page, int size)
        {
            using (var db = new SmcDbContext())
            {
                var q = db.Properties.AsNoTracking().Where(x => !x.IsDeleted);
                if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.PropertyCode.Contains(s) || x.Name.Contains(s) || x.CurrentOccupant.Contains(s) || x.TpNumber.Contains(s) || x.Ward.Contains(s) || x.Address.Contains(s) || x.SurveyNumber.Contains(s)); }
                if (!string.IsNullOrWhiteSpace(category)) { var c = category; q = q.Where(x => x.Category == c); }
                if (!string.IsNullOrWhiteSpace(status)) { var st = status; q = q.Where(x => x.Status == st); }
                return Page(q.OrderBy(x => x.PropertyCode), page, size);
            }
        }

        public Property GetProperty(int id) { using (var db = new SmcDbContext()) return db.Properties.AsNoTracking().FirstOrDefault(x => x.Id == id && !x.IsDeleted); }

        public List<PropertyRef> PropertyRefs()
        {
            using (var db = new SmcDbContext())
                return db.Properties.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.PropertyCode).Select(x => new PropertyRef { Id = x.Id, Code = x.PropertyCode, Name = x.Name, Category = x.Category, Holder = x.CurrentOccupant }).ToList();
        }

        public List<string> PropertyCategories()
        {
            using (var db = new SmcDbContext()) return db.Properties.AsNoTracking().Where(x => !x.IsDeleted).Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
        }

        public Result<int> SaveProperty(Property p, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (p.Id == 0)
                {
                    var prefix = PropertyCategories_Prefix(p.Category);
                    var codes = db.Properties.Where(x => x.PropertyCode.StartsWith(prefix + "-")).Select(x => x.PropertyCode).ToList();
                    var max = 0;
                    foreach (var c in codes) { int n; if (int.TryParse(c.Substring(prefix.Length + 1), out n) && n > max) max = n; }
                    p.PropertyCode = prefix + "-" + (max + 1).ToString("000");
                    p.CreatedAt = DateTime.UtcNow; p.CreatedBy = Who(actor); p.IsDeleted = false;
                    db.Properties.Add(p);
                    db.SaveChanges();
                    AddAudit(db, actor, "Create", "Property", p.Id, null, Cut("Code=" + p.PropertyCode + "; Category=" + p.Category + "; Name=" + p.Name, 2000));
                    db.SaveChanges(); tx.Commit();
                    return Result.Success(p.Id);
                }
                var cur = db.Properties.FirstOrDefault(x => x.Id == p.Id && !x.IsDeleted);
                if (cur == null) return Result.Failure<int>(Err("NOT_FOUND", "मालमत्ता आढळली नाही."));
                UpdateWithAudit(db, actor, cur, p, PropertyFields, "Property", cur.Id);
                Stamp(cur, actor, false);
                db.SaveChanges(); tx.Commit();
                return Result.Success(cur.Id);
            }
        }

        private static string PropertyCategories_Prefix(string category)
        {
            var known = PropertyCategoriesPrefix(category);
            if (known != null) return known;
            var letters = new string((category ?? "XX").Where(char.IsLetter).Take(2).ToArray()).ToUpperInvariant();
            return letters.Length == 2 ? letters : "XX";
        }
        private static string PropertyCategoriesPrefix(string category) { return SMC.BhoomiMalmatta.Models.Constants.PropertyCategories.Prefix(category); }

        // ---- Allocations ----
        public PagedResult<AllocationProcess> SearchAllocations(string search, string status, int? propertyId, int page, int size)
        {
            using (var db = new SmcDbContext())
            {
                var q = db.AllocationProcesses.AsNoTracking().Where(x => !x.IsDeleted);
                if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.NoticeNumber.Contains(s) || x.HighestBidderName.Contains(s) || x.HighestBidderMobile.Contains(s)); }
                if (!string.IsNullOrWhiteSpace(status)) { var st = status; q = q.Where(x => x.Status == st); }
                if (propertyId.HasValue) { var pid = propertyId.Value; q = q.Where(x => x.PropertyId == pid); }
                return Page(q.OrderByDescending(x => x.Id), page, size);
            }
        }
        public AllocationProcess GetAllocation(int id) { using (var db = new SmcDbContext()) return db.AllocationProcesses.AsNoTracking().FirstOrDefault(x => x.Id == id && !x.IsDeleted); }

        public Result<int> SaveAllocation(AllocationProcess a, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            {
                var prop = db.Properties.AsNoTracking().FirstOrDefault(x => x.Id == a.PropertyId && !x.IsDeleted);
                if (prop == null) return Result.Failure<int>(Err("NO_PROPERTY", "निवडलेली मालमत्ता आढळली नाही."));
                a.PropertyCategory = Cut(prop.Category, 40);
                if (a.Id == 0)
                {
                    a.CreatedBy = Who(actor); a.CreatedAt = DateTime.UtcNow; a.IsDeleted = false;
                    db.AllocationProcesses.Add(a); db.SaveChanges();
                    AddAudit(db, actor, "Create", "AllocationProcess", a.Id, null, Cut("Method=" + a.Method + "; Notice=" + a.NoticeNumber + "; PropertyId=" + a.PropertyId, 2000));
                    db.SaveChanges(); return Result.Success(a.Id);
                }
                var cur = db.AllocationProcesses.FirstOrDefault(x => x.Id == a.Id && !x.IsDeleted);
                if (cur == null) return Result.Failure<int>(Err("NOT_FOUND", "नोंद आढळली नाही."));
                UpdateWithAudit(db, actor, cur, a, AllocationFields, "AllocationProcess", cur.Id);
                Stamp(cur, actor, false); db.SaveChanges();
                return Result.Success(cur.Id);
            }
        }

        // ---- Transfers ----
        public PagedResult<GalaTransfer> SearchTransfers(string search, string stage, int? propertyId, int page, int size)
        {
            using (var db = new SmcDbContext())
            {
                var q = db.GalaTransfers.AsNoTracking().Where(x => !x.IsDeleted);
                if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.ExistingHolder.Contains(s) || x.NewHolder.Contains(s) || x.PaymentReference.Contains(s)); }
                if (!string.IsNullOrWhiteSpace(stage)) { var st = stage; q = q.Where(x => x.Stage == st); }
                if (propertyId.HasValue) { var pid = propertyId.Value; q = q.Where(x => x.PropertyId == pid); }
                return Page(q.OrderByDescending(x => x.Id), page, size);
            }
        }
        public GalaTransfer GetTransfer(int id) { using (var db = new SmcDbContext()) return db.GalaTransfers.AsNoTracking().FirstOrDefault(x => x.Id == id && !x.IsDeleted); }
        public GalaTransferRate GetTransferRate(string category) { using (var db = new SmcDbContext()) return db.GalaTransferRates.AsNoTracking().FirstOrDefault(x => x.Category == category && x.IsActive && !x.IsDeleted); }

        public Result<int> CreateTransfer(int propertyId, string transferType, string existingHolder, string newHolder, string remark, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var prop = db.Properties.FirstOrDefault(x => x.Id == propertyId && !x.IsDeleted);
                if (prop == null) return Result.Failure<int>(Err("NO_PROPERTY", "मालमत्ता आढळली नाही."));
                var holder = string.IsNullOrWhiteSpace(prop.CurrentOccupant) ? (string.IsNullOrWhiteSpace(existingHolder) ? null : existingHolder.Trim()) : prop.CurrentOccupant;
                if (holder == null) return Result.Failure<int>(Err("NO_HOLDER", "या मालमत्तेवर सध्याचा धारक नोंदलेला नाही; कृपया पूर्वीचा धारक नोंदवा."));
                var open = db.GalaTransfers.Any(x => x.PropertyId == propertyId && !x.IsDeleted && x.Stage != TransferStages.Approved && x.Stage != TransferStages.Rejected);
                if (open) return Result.Failure<int>(Err("DUPLICATE", "या मालमत्तेसाठी हस्तांतरण प्रक्रिया आधीच सुरू आहे."));
                if (string.Equals(holder, newHolder.Trim(), StringComparison.OrdinalIgnoreCase)) return Result.Failure<int>(Err("SAME_HOLDER", "नवीन धारक सध्याच्या धारकापेक्षा वेगळा असावा."));
                var breach = transferType == TransferTypes.Breach;
                var fees = TransferRules.Fees(db.GalaTransferRates.AsNoTracking().FirstOrDefault(x => x.Category == prop.Category && x.IsActive && !x.IsDeleted), breach);
                if (fees.IsFailure) return Result.Failure<int>(fees.Error);
                var t = new GalaTransfer
                {
                    PropertyId = propertyId, Category = Cut(prop.Category, 30), ExistingHolder = Cut(holder, 200), CurrentOccupant = Cut(holder, 200), NewHolder = newHolder.Trim(), TransferType = transferType,
                    IsBreach = breach, TransferFee = fees.Value[0], Penalty = fees.Value[1], Stage = TransferStages.Draft, Shera = remark, PropertyHolderAtRequest = prop.CurrentOccupant,
                    CreatedBy = Who(actor), CreatedAt = DateTime.UtcNow
                };
                db.GalaTransfers.Add(t); db.SaveChanges();
                AddAudit(db, actor, "Transfer Request", "GalaTransfer", t.Id, null, Cut("PropertyId=" + propertyId + "; " + t.ExistingHolder + " -> " + t.NewHolder + "; Fee=" + t.TransferFee.ToString("0.##") + "; Penalty=" + t.Penalty.ToString("0.##"), 2000));
                db.SaveChanges(); tx.Commit();
                return Result.Success(t.Id);
            }
        }

        public Result TransitionTransfer(int id, string action, byte[] version, string paymentReference, string remark, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var t = db.GalaTransfers.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
                if (t == null) return Result.Failure(Err("NOT_FOUND", "हस्तांतरण नोंद आढळली नाही."));
                if (version != null && t.Version != null && !t.Version.SequenceEqual(version)) return Result.Failure(Err("CONCURRENCY_CONFLICT", "ही नोंद दुसऱ्या वापरकर्त्याने बदलली आहे. पृष्ठ पुन्हा उघडा."));
                var step = TransferRules.Find(t.Stage, action);
                if (step == null) return Result.Failure(Err("INVALID_TRANSITION", "या टप्प्यावर ही कृती करता येत नाही."));
                if (Array.IndexOf(step.Roles, actor.Role) < 0) return Result.Failure(Err("FORBIDDEN", "ही कृती करण्याची आपल्याला परवानगी नाही."));
                if (action == "reject" && string.IsNullOrWhiteSpace(remark)) return Result.Failure(Err("REMARK", "नाकारण्याचे कारण (शेरा) आवश्यक आहे."));
                if (action == "pay" && string.IsNullOrWhiteSpace(paymentReference)) return Result.Failure(Err("PAYMENT_REF", "भरणा संदर्भ (UTR / पावती क्रमांक) आवश्यक आहे."));
                var from = t.Stage;
                if (action == "pay") { t.PaymentReference = Cut(paymentReference.Trim(), 100); t.PaymentVerifiedAt = DateTime.UtcNow; }
                if (action == "approve")
                {
                    var prop = db.Properties.FirstOrDefault(x => x.Id == t.PropertyId && !x.IsDeleted);
                    if (prop == null) return Result.Failure(Err("NO_PROPERTY", "मालमत्ता आढळली नाही."));
                    if ((prop.CurrentOccupant ?? "") != (t.PropertyHolderAtRequest ?? "")) return Result.Failure(Err("HOLDER_CHANGED", "विनंतीनंतर मालमत्तेचा धारक बदलला आहे; हे हस्तांतरण मंजूर करता येत नाही."));
                    AddAudit(db, actor, "Update", "Property", prop.Id, "CurrentOccupant=" + (prop.CurrentOccupant ?? ""), "CurrentOccupant=" + t.NewHolder);
                    prop.CurrentOccupant = Cut(t.NewHolder, 200); prop.UpdatedBy = Who(actor); prop.UpdatedAt = DateTime.UtcNow;
                    t.ApprovedAt = DateTime.UtcNow; t.ApprovedBy = actor.UserName;
                }
                if (action == "approve" || action == "reject") t.DecisionRemark = Cut(string.IsNullOrWhiteSpace(remark) ? t.DecisionRemark : remark.Trim(), 2000);
                t.Stage = step.To; t.UpdatedBy = Who(actor); t.UpdatedAt = DateTime.UtcNow;
                AddAudit(db, actor, step.AuditLabel, "GalaTransfer", t.Id, "Stage=" + from, Cut("Stage=" + step.To + (string.IsNullOrWhiteSpace(remark) ? "" : "; Remark=" + remark.Trim()), 2000));
                try { db.SaveChanges(); }
                catch (DbUpdateConcurrencyException) { return Result.Failure(Err("CONCURRENCY_CONFLICT", "ही नोंद दुसऱ्या वापरकर्त्याने बदलली आहे. पृष्ठ पुन्हा उघडा.")); }
                tx.Commit();
                return Result.Success();
            }
        }

        public List<RegisterDocument> TransferDocuments(int transferId)
        {
            using (var db = new SmcDbContext())
                return db.GalaTransferDocuments.AsNoTracking().Where(x => x.GalaTransferId == transferId && !x.IsDeleted).OrderBy(x => x.Id)
                    .Select(x => new RegisterDocument { Id = x.Id, FileName = x.FileName, Kind = x.Kind, ContentType = x.ContentType, RelativePath = x.FilePath, Size = x.Size, CreatedAt = x.CreatedAt }).ToList();
        }

        public Result AddTransferDocument(int transferId, string kind, StoredDocument file, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            {
                var t = db.GalaTransfers.AsNoTracking().FirstOrDefault(x => x.Id == transferId && !x.IsDeleted);
                if (t == null) return Result.Failure(Err("NOT_FOUND", "हस्तांतरण नोंद आढळली नाही."));
                if (TransferStages.IsFinal(t.Stage)) return Result.Failure(Err("FINAL", "अंतिम झालेल्या प्रकरणात कागदपत्र जोडता येत नाही."));
                db.GalaTransferDocuments.Add(new GalaTransferDocument { GalaTransferId = transferId, Kind = Cut(kind, 20), FileName = Cut(file.OriginalFileName, 255), FilePath = Cut(file.RelativePath, 500), ContentType = Cut(file.ContentType, 100), Size = file.SizeBytes, CreatedBy = Who(actor), CreatedAt = DateTime.UtcNow });
                AddAudit(db, actor, "Create", "GalaTransferDocument", transferId, null, Cut("Kind=" + kind + "; File=" + file.OriginalFileName, 2000));
                db.SaveChanges(); return Result.Success();
            }
        }

        public RegisterDocument GetTransferDocument(int documentId, out int transferId)
        {
            using (var db = new SmcDbContext())
            {
                var x = db.GalaTransferDocuments.AsNoTracking().FirstOrDefault(d => d.Id == documentId && !d.IsDeleted);
                if (x == null) { transferId = 0; return null; }
                transferId = x.GalaTransferId;
                return new RegisterDocument { Id = x.Id, FileName = x.FileName, Kind = x.Kind, ContentType = x.ContentType, RelativePath = x.FilePath, Size = x.Size, CreatedAt = x.CreatedAt };
            }
        }

        // ---- Calculations ----
        public PagedResult<Calculation> SearchCalculations(int? propertyId, int page, int size)
        {
            using (var db = new SmcDbContext())
            {
                var q = db.Calculations.AsNoTracking().Where(x => !x.IsDeleted);
                if (propertyId.HasValue) { var pid = propertyId.Value; q = q.Where(x => x.PropertyId == pid); }
                return Page(q.OrderByDescending(x => x.Id), page, size);
            }
        }
        public Calculation GetCalculation(int id) { using (var db = new SmcDbContext()) return db.Calculations.AsNoTracking().FirstOrDefault(x => x.Id == id && !x.IsDeleted); }

        public Result<int> SaveCalculation(Calculation c, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            {
                if (!db.Properties.Any(x => x.Id == c.PropertyId && !x.IsDeleted)) return Result.Failure<int>(Err("NO_PROPERTY", "मालमत्ता आढळली नाही."));
                c.CreatedBy = Who(actor); c.CreatedAt = DateTime.UtcNow; c.IsDeleted = false;
                db.Calculations.Add(c); db.SaveChanges();
                AddAudit(db, actor, "Create", "Calculation", c.Id, null, Cut("PropertyId=" + c.PropertyId + "; Rate=" + Val(c.Rate) + "; Months=" + c.PeriodMonths + "; Total=" + Val(c.TotalAmount), 2000));
                db.SaveChanges(); return Result.Success(c.Id);
            }
        }

        // ---- Recovery ----
        public PagedResult<RecoveryCase> SearchRecovery(string search, string stage, int? propertyId, int page, int size)
        {
            using (var db = new SmcDbContext())
            {
                var q = db.RecoveryCases.AsNoTracking().Where(x => !x.IsDeleted);
                if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.NoticeNumber.Contains(s)); }
                if (!string.IsNullOrWhiteSpace(stage)) { var st = stage; q = q.Where(x => x.Stage == st); }
                if (propertyId.HasValue) { var pid = propertyId.Value; q = q.Where(x => x.PropertyId == pid); }
                return Page(q.OrderByDescending(x => x.Id), page, size);
            }
        }
        public RecoveryCase GetRecovery(int id) { using (var db = new SmcDbContext()) return db.RecoveryCases.AsNoTracking().FirstOrDefault(x => x.Id == id && !x.IsDeleted); }
        public List<LeaseRef> LeaseRefs()
        {
            using (var db = new SmcDbContext())
                return db.Leases.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.DeedNumber).Select(x => new LeaseRef { Id = x.Id, PropertyId = x.PropertyId, Label = x.DeedNumber + " — " + x.LesseeName }).ToList();
        }

        public Result<int> SaveRecovery(RecoveryCase r, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            {
                var prop = db.Properties.AsNoTracking().FirstOrDefault(x => x.Id == r.PropertyId && !x.IsDeleted);
                if (prop == null) return Result.Failure<int>(Err("NO_PROPERTY", "निवडलेली मालमत्ता आढळली नाही."));
                if (r.LeaseId.HasValue && !db.Leases.Any(x => x.Id == r.LeaseId && !x.IsDeleted && x.PropertyId == r.PropertyId)) return Result.Failure<int>(Err("LEASE", "निवडलेला भाडेकरार या मालमत्तेचा नाही."));
                r.PropertyCategory = Cut(prop.Category, 40);
                if (r.Id == 0)
                {
                    r.CreatedBy = Who(actor); r.CreatedAt = DateTime.UtcNow; r.IsDeleted = false;
                    db.RecoveryCases.Add(r); db.SaveChanges();
                    AddAudit(db, actor, "Create", "RecoveryCase", r.Id, null, Cut("PropertyId=" + r.PropertyId + "; Outstanding=" + Val(r.OutstandingAmount) + "; Stage=" + r.Stage, 2000));
                    db.SaveChanges(); return Result.Success(r.Id);
                }
                var cur = db.RecoveryCases.FirstOrDefault(x => x.Id == r.Id && !x.IsDeleted);
                if (cur == null) return Result.Failure<int>(Err("NOT_FOUND", "नोंद आढळली नाही."));
                var rules = RegisterRules.ValidateRecovery(r, cur);
                if (rules.Count > 0) return Result.Failure<int>(Err("INVALID", rules[0]));
                UpdateWithAudit(db, actor, cur, r, RecoveryFields, "RecoveryCase", cur.Id);
                Stamp(cur, actor, false); db.SaveChanges();
                return Result.Success(cur.Id);
            }
        }

        // ---- Schemes ----
        public PagedResult<SchemeApplication> SearchSchemes(string search, string status, int? propertyId, int page, int size)
        {
            using (var db = new SmcDbContext())
            {
                var q = db.SchemeApplications.AsNoTracking().Where(x => !x.IsDeleted);
                if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.SchemeType.Contains(s) || x.ApplicantName.Contains(s) || x.ApplicantMobile.Contains(s)); }
                if (!string.IsNullOrWhiteSpace(status)) { var st = status; q = q.Where(x => x.Status == st); }
                if (propertyId.HasValue) { var pid = propertyId.Value; q = q.Where(x => x.PropertyId == pid); }
                return Page(q.OrderByDescending(x => x.Id), page, size);
            }
        }
        public SchemeApplication GetScheme(int id) { using (var db = new SmcDbContext()) return db.SchemeApplications.AsNoTracking().FirstOrDefault(x => x.Id == id && !x.IsDeleted); }

        public Result<int> SaveScheme(SchemeApplication s, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            {
                var prop = db.Properties.AsNoTracking().FirstOrDefault(x => x.Id == s.PropertyId && !x.IsDeleted);
                if (prop == null) return Result.Failure<int>(Err("NO_PROPERTY", "निवडलेली मालमत्ता आढळली नाही."));
                s.PropertyCategory = Cut(prop.Category, 40);
                s.PayableAmount = s.OriginalOutstanding - s.WaivedAmount;
                if (s.Id == 0)
                {
                    s.CreatedBy = Who(actor); s.CreatedAt = DateTime.UtcNow; s.IsDeleted = false;
                    db.SchemeApplications.Add(s); db.SaveChanges();
                    AddAudit(db, actor, "Create", "SchemeApplication", s.Id, null, Cut("Scheme=" + s.SchemeType + "; Applicant=" + s.ApplicantName + "; PropertyId=" + s.PropertyId, 2000));
                    db.SaveChanges(); return Result.Success(s.Id);
                }
                var cur = db.SchemeApplications.FirstOrDefault(x => x.Id == s.Id && !x.IsDeleted);
                if (cur == null) return Result.Failure<int>(Err("NOT_FOUND", "नोंद आढळली नाही."));
                UpdateWithAudit(db, actor, cur, s, SchemeFields, "SchemeApplication", cur.Id);
                Stamp(cur, actor, false); db.SaveChanges();
                return Result.Success(cur.Id);
            }
        }

        // ---- Shared documents / audit / dashboard ----
        private static IQueryable<Document> DocsFor(SmcDbContext db, string entityType, int entityId) { return db.Documents.Where(x => !x.IsDeleted && x.EntityType == entityType && x.EntityId == entityId); }

        public List<RegisterDocument> EntityDocuments(string entityType, int entityId)
        {
            using (var db = new SmcDbContext())
                return DocsFor(db, entityType, entityId).AsNoTracking().OrderBy(x => x.Id)
                    .Select(x => new RegisterDocument { Id = x.Id, FileName = x.FileName, Kind = x.EntityType, ContentType = x.ContentType, RelativePath = x.FilePath, Size = x.FileSizeBytes, CreatedAt = x.CreatedAt }).ToList();
        }

        public Result AddEntityDocument(string entityType, int entityId, StoredDocument file, ActorContext actor)
        {
            using (var db = new SmcDbContext())
            {
                var d = new Document { EntityType = Cut(entityType, 30), EntityId = entityId, FileName = Cut(file.OriginalFileName, 300), StoredFileName = Cut(file.StoredFileName, 300), FilePath = Cut(file.RelativePath, 500), ContentType = Cut(file.ContentType, 100), FileSizeBytes = file.SizeBytes, CreatedBy = Who(actor), CreatedAt = DateTime.UtcNow };
                switch (entityType)
                {
                    case "Property": d.PropertyId = entityId; break;
                    case "SchemeApplication": d.SchemeApplicationId = entityId; break;
                    case "AllocationProcess": d.AllocationProcessId = entityId; break;
                    case "RecoveryCase": d.RecoveryCaseId = entityId; break;
                    case "Calculation": d.CalculationId = entityId; break;
                    default: return Result.Failure(Err("ENTITY", "अवैध नोंद प्रकार."));
                }
                db.Documents.Add(d);
                AddAudit(db, actor, "Create", "Document", entityId, null, Cut(entityType + "; File=" + file.OriginalFileName, 2000));
                db.SaveChanges(); return Result.Success();
            }
        }

        public RegisterDocument GetEntityDocument(int documentId)
        {
            using (var db = new SmcDbContext())
            {
                var x = db.Documents.AsNoTracking().FirstOrDefault(d => d.Id == documentId && !d.IsDeleted);
                return x == null ? null : new RegisterDocument { Id = x.Id, FileName = x.FileName, Kind = x.EntityType + ":" + x.EntityId, ContentType = x.ContentType, RelativePath = x.FilePath, Size = x.FileSizeBytes, CreatedAt = x.CreatedAt };
            }
        }

        public List<AuditLogItem> EntityAudit(string entityName, int entityId, int max)
        {
            using (var db = new SmcDbContext())
                return db.AuditLogs.AsNoTracking().Where(x => x.EntityName == entityName && x.EntityId == entityId).OrderByDescending(x => x.Id).Take(max).ToList()
                    .Select(x => new AuditLogItem { Id = x.Id, Timestamp = x.Timestamp, UserName = x.UserName, Action = x.Action, EntityName = x.EntityName, EntityId = x.EntityId, OldValue = x.OldValue, NewValue = x.NewValue, IpAddress = x.IpAddress }).ToList();
        }

    }
}
