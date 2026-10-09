using System;
using System.Collections.Generic;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Dto;
using SMC.BhoomiMalmatta.Models.Entities;

namespace SMC.BhoomiMalmatta.Business.Abstractions
{
    /// <summary>One MR_AST_MasterData row as read for the property dropdowns (only the needed columns). Labels may be damaged: see MasterLabelCleaner.</summary>
    public sealed class MasterOptionRow
    {
        public int Id { get; set; }
        public string GroupName { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string Key { get; set; }
        public string MarathiName { get; set; }
        public string EnglishName { get; set; }
        public int DisplayOrder { get; set; }
    }

    public enum PropertyColumn { Category, Status, Ward, Zone }

    /// <summary>Read side for the property dropdowns: master-data groups and the values actually stored in MR_AST_Properties.</summary>
    public interface IPropertyOptionSource
    {
        /// <summary>Active, not deleted rows of one MR_AST_MasterData group, ordered by DisplayOrder, Id.</summary>
        List<MasterOptionRow> MasterRows(string groupName);
        /// <summary>Active, not deleted rows of every group whose name starts with <paramref name="groupPrefix"/> (the per-zone prabhag groups).</summary>
        List<MasterOptionRow> MasterRowsWithPrefix(string groupPrefix);
        /// <summary>Distinct non-empty values of one column over the not deleted properties.</summary>
        List<string> DistinctInUse(PropertyColumn column);
    }

    /// <summary>Owner of a PropertyCode, including soft-deleted rows (the unique index covers every row).</summary>
    public sealed class PropertyCodeOwner { public int Id { get; set; } public bool IsDeleted { get; set; } }

    /// <summary>Data access for the dedicated Properties page (MR_AST_Properties). Writes audit rows in the same transaction.</summary>
    public interface IPropertyRegister
    {
        PagedResult<PropertyListRow> Search(PropertyListFilter filter);
        Property Get(int id);
        PropertyCodeOwner FindCodeOwner(string propertyCode);
        /// <summary>Codes of every row of this category, soft-deleted rows included.</summary>
        List<string> CodesOfCategory(string category);
        /// <summary>Codes starting with "prefix-", soft-deleted rows included.</summary>
        List<string> CodesWithPrefix(string prefix);
        Result<int> Create(Property property, ActorContext actor);
        /// <summary>Fails with CONFLICT when the stored UpdatedAt differs from <paramref name="expectedVersion"/>.</summary>
        Result Update(Property property, string expectedVersion, ActorContext actor);
        /// <summary>Not deleted rows per referencing table (the nine NO ACTION foreign keys), keyed by table name.</summary>
        Dictionary<string, int> DependentCounts(int id);
        /// <summary>IsDeleted = 1, DeletedAt, DeletedBy; never a DELETE statement. Re-checks the dependents inside its transaction.</summary>
        Result SoftDelete(int id, DateTime atUtc, ActorContext actor);
    }
}
