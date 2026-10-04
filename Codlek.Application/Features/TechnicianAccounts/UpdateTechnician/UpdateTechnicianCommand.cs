using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.UpdateTechnician;

/// <summary>
/// تعديل بيانات فني.
/// </summary>
/// <param name="DepartmentId">
/// 🔴 <b>تلات حالات في حقل واحد:</b> <c>null</c> = «ماتلمسش»،
/// و<see cref="Guid.Empty"/> = «شيل القسم»، وأي معرّف تاني = «حطّه
/// في القسم ده».
///
/// <para>⚠️ والكنترولر هو اللي بيقرا الجسم الخام ويفرّق بين «الحقل
/// مااتبعتش» و«اتبعت <c>null</c>» — JSON مابيفرّقش بينهم في
/// النوع.</para>
/// </param>
public sealed record UpdateTechnicianCommand(
    Guid Id,
    string? DisplayName,
    int? Specialty,
    Guid? DepartmentId,
    bool? CanTest,
    bool? CanRepair) : IRequest<Result<TechnicianActionResponse>>;
