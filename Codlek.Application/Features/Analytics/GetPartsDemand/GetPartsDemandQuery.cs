using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using MediatR;

namespace Codlek.Application.Features.Analytics.GetPartsDemand;

/// <summary>
/// أكتر قطع الغيار المستخدمة.
///
/// <para>⚠️ <b>للمديرين وفوق بس.</b> ده رقم مخزن على مستوى الشركة
/// كلها — مالوش نسخة «بتاعتي» تتعرض لفني، والتضييق بالفني كان
/// هيدّي رقم مالوش معنى بدل ما يمنع.</para>
/// </summary>
public sealed record GetPartsDemandQuery(
    string? Range,
    DateTime? From,
    DateTime? To,
    int? Limit) : IRequest<Result<PartsDemandResponse>>;
