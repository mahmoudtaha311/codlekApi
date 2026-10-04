using Codlek.Application.Contracts.Technicians;
using Codlek.Core.Entities;
using Codlek.Core.Enums;

namespace Codlek.Application.Features.TechnicianAccounts;

/// <summary>
/// تحويل صف الفني للعقد — <b>مكان واحد للسبع نقط</b>.
///
/// <para>🔴 <b>ومفيش بصمة ولا ملح في الخرج.</b> لو التحويل اتكتب
/// سبع مرات، أول مرة حد يزوّد عمود بيطلّع البصمة في رد واحد — وده
/// النوع اللي محدش بيلاحظه.</para>
/// </summary>
internal static class TechnicianAccountMapping
{
    public static TechnicianAccount Account(
        Technician t, IReadOnlyList<Guid>? brandIds = null) =>
        new(
            Id: t.Id,
            Code: t.Code,
            DisplayName: t.DisplayName,
            Username: t.Username,
            IsActive: t.IsActive,
            SuspendedReason: t.SuspendedReason,
            SuspendedByName: t.SuspendedByName,
            SuspendedAtUtc: t.SuspendedAtUtc,
            MustChangePassword: t.MustChangePassword,
            CredentialVersion: t.CredentialVersion,
            Specialty: (int)t.Specialty,
            SpecialtyText: TechnicianSpecialtyText.Arabic(t.Specialty),
            DepartmentId: t.DepartmentId,
            CreatedAtUtc: t.CreatedAtUtc,
            LastSuccessfulLoginUtc: t.LastSuccessfulLoginUtc,
            CanTest: t.CanTest,
            CanRepair: t.CanRepair,
            CapabilityChangedAtUtc: t.CapabilityChangedAtUtc,
            BrandIds: brandIds);
}
