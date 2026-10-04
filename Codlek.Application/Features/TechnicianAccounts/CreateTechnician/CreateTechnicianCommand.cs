using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.TechnicianAccounts.CreateTechnician;

/// <summary>
/// إنشاء حساب فني.
///
/// <para>⚠️ <b>ومفيش معرّف شركة في الأمر ده.</b> الشركة بتيجي من
/// التوكن دايماً — ولو أخدها من الطلب، مدير شركة كان هيقدر يعمل
/// فني في شركة تانية.</para>
/// </summary>
public sealed record CreateTechnicianCommand(
    string? DisplayName,
    string? Username,
    string? Password,
    int? Specialty,
    Guid? DepartmentId) : IRequest<Result<TechnicianSecretResponse>>;
