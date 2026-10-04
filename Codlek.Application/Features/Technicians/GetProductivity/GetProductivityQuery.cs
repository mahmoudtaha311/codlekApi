using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Technicians;
using MediatR;

namespace Codlek.Application.Features.Technicians.GetProductivity;

/// <summary>
/// جدول إنتاجية الفنيين.
///
/// <para>⚠️ <b>بلا تصفيح عن قصد:</b> عدد الفنيين عشرات، والجدول
/// بيتقرا كله مرة واحدة — والتصفيح كان بيخلّي الترتيب على السيرفر
/// بلا معنى.</para>
/// </summary>
public sealed record GetProductivityQuery(
    string? Range,
    DateTime? From,
    DateTime? To,
    string? Search,
    string? Sort) : IRequest<Result<IReadOnlyList<TechnicianListItem>>>;
