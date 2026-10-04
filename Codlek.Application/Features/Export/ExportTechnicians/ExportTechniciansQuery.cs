using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Export;
using MediatR;

namespace Codlek.Application.Features.Export.ExportTechnicians;

/// <summary>
/// ملف مراجعة الفنيين.
///
/// <para>🔴 <b>من غير <see cref="Technician"/> بيطلع الكل، ومعاه
/// بيطلع واحد.</b> ده اللي صاحب الشغل طلبه بالنص: «تصدير لكل فني
/// اسبوعي بقا او يومي لكل فني واصدرة او اصدر كله».</para>
///
/// <para>⚠️ <b>والأسبوعي واليومي مش مفاتيح جديدة.</b> السيرفر
/// بيفهم <c>range=custom&amp;from=&amp;to=</c> من الأول، واليومي
/// هو <c>from=to=اليوم</c>. مفيش أي مفتاح اتزاد عشان الطلب
/// ده.</para>
/// </summary>
public sealed record ExportTechniciansQuery(
    string? Range,
    DateTime? From,
    DateTime? To,
    string? Technician) : IRequest<Result<ExportWorkbook>>;
