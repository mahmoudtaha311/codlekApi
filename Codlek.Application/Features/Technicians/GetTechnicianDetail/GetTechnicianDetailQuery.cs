using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.Technicians.GetTechnicianDetail;

/// <summary>
/// صفحة فني واحد — <b>مفتاحها الكود</b>.
///
/// <para>⚠️ الفني هنا هوية <b>محطة</b>: الكود بيتكتب على الفحص من
/// الراكة، وفيه فنيين مالهمش حساب في اللوحة خالص. فالكود هو المفتاح
/// الوحيد اللي بيشتغل على الاتنين.</para>
/// </summary>
public sealed record GetTechnicianDetailQuery(
    string Code,
    string? Range,
    DateTime? From,
    DateTime? To) : IRequest<Result<TechnicianDetail>>;
