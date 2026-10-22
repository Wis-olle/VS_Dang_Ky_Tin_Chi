namespace QuanLyGiangVien.Domain.Catalogs;

public enum OrganizationKind { Faculty = 0, Major = 1, Class = 2 }

/// <summary>Immutable editable catalog snapshot; codes remain stable after creation.</summary>
public sealed record OrganizationRecord(OrganizationKind Kind, string Code, string Name, string? ParentCode,
    bool Active, short? Cohort = null, string? AcademicYears = null, string? Description = null, string? Version = null);
