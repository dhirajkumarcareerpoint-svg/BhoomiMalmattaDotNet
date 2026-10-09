using System;
using System.Collections.Generic;

namespace SMC.BhoomiMalmatta.Models.Dto
{
    /// <summary>One dropdown option for the Properties page (code = the value stored in MR_AST_Properties, label = the master-data label).</summary>
    public sealed class PropertyOption
    {
        public string Code { get; set; }
        public string Label { get; set; }
    }

    /// <summary>GET api/v1/properties/options: every list the Properties page needs, read from the database only.</summary>
    public sealed class PropertyOptionsDto
    {
        public PropertyOptionsDto() { Categories = new List<PropertyOption>(); Statuses = new List<PropertyOption>(); Wards = new List<PropertyOption>(); Zones = new List<PropertyOption>(); Warnings = new List<string>(); }
        public List<PropertyOption> Categories { get; set; }
        public List<PropertyOption> Statuses { get; set; }
        public List<PropertyOption> Wards { get; set; }
        public List<PropertyOption> Zones { get; set; }
        public List<string> Warnings { get; set; }
        /// <summary>Status code preselected in the "नवीन नोंद" dialog.</summary>
        public string DefaultStatus { get; set; }
    }

    /// <summary>The columns of the list query (only what the table shows).</summary>
    public sealed class PropertyListRow
    {
        public int Id { get; set; }
        public string PropertyCode { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Ward { get; set; }
        public decimal AnnualDemand { get; set; }
        public string Status { get; set; }
    }

    public sealed class PropertyListFilter
    {
        public string Search { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>One table row as the page renders it (labels and the rupee text are prepared on the server).</summary>
    public sealed class PropertyListItemDto
    {
        public int Id { get; set; }
        public string PropertyCode { get; set; }
        public string Name { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryLabel { get; set; }
        public string Ward { get; set; }
        public string AnnualDemandText { get; set; }
        public string StatusCode { get; set; }
        public string StatusLabel { get; set; }
    }

    public sealed class PropertyLinkDto { public string Href { get; set; } public string Icon { get; set; } public string Label { get; set; } }
    public sealed class PropertyDocumentDto { public int Id { get; set; } public string FileName { get; set; } public string SizeText { get; set; } public string DateText { get; set; } public string Url { get; set; } }

    /// <summary>GET api/v1/properties/{id}: the dialog fields plus the existing related-record tiles and document list for "पाहा".</summary>
    public sealed class PropertyDetailDto
    {
        public PropertyDetailDto() { RelatedLinks = new List<PropertyLinkDto>(); Documents = new List<PropertyDocumentDto>(); }
        public int Id { get; set; }
        public string Category { get; set; }
        public string CategoryLabel { get; set; }
        public string PropertyCode { get; set; }
        public string Name { get; set; }
        public string Ward { get; set; }
        public string Zone { get; set; }
        public string Address { get; set; }
        public string AreaSqFt { get; set; }
        public string MonthlyRent { get; set; }
        public string AnnualDemand { get; set; }
        public string SurveyNumber { get; set; }
        public string TpNumber { get; set; }
        public string Status { get; set; }
        public string StatusLabel { get; set; }
        public string CurrentOccupant { get; set; }
        public string Shera { get; set; }
        /// <summary>UpdatedAt as ticks ("" when never updated); sent back with PUT for the edit-conflict check.</summary>
        public string Version { get; set; }
        public string DocumentsUrl { get; set; }
        public List<PropertyLinkDto> RelatedLinks { get; set; }
        public List<PropertyDocumentDto> Documents { get; set; }
    }

    /// <summary>POST / PUT body. Numbers arrive as text so the server can answer with field-level Marathi messages.</summary>
    public sealed class PropertySaveRequest
    {
        public string Category { get; set; }
        public string PropertyCode { get; set; }
        public string Name { get; set; }
        public string Ward { get; set; }
        public string Zone { get; set; }
        public string Address { get; set; }
        public string AreaSqFt { get; set; }
        public string MonthlyRent { get; set; }
        public string AnnualDemand { get; set; }
        public string SurveyNumber { get; set; }
        public string TpNumber { get; set; }
        public string Status { get; set; }
        public string CurrentOccupant { get; set; }
        public string Shera { get; set; }
        public string Version { get; set; }
    }

    public sealed class PropertyFieldErrorDto { public string Field { get; set; } public string Message { get; set; } }
}
