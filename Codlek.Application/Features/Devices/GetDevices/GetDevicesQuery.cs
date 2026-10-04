using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.GetDevices;

/// <summary>
/// قايمة الأجهزة — <b>١٥ فلتر</b>.
///
/// <para>⚠️ <b>ومفيش متحقّق على ولا واحد.</b> دي مدخلات من رابط
/// محفوظ في المتصفح: القيمة المش مفهومة بتتجاهل، والتصفيح بيتظبّط،
/// والرد عمره ما بيبقى <c>400</c>.</para>
///
/// <para>⚠️ <b>والأسماء دي هي اللي على السلك</b> — الداش بورد
/// بتحفظ الفلاتر في الرابط، فأي إعادة تسمية بتكسر كل رابط محفوظ
/// عند المدير.</para>
/// </summary>
public sealed record GetDevicesQuery(
    string? Search,
    string? Status,
    string? Confidence,
    string? Outcome,
    string? Technician,
    Guid? Rack,
    DateTime? From,
    DateTime? To,
    string? Stage,
    string? Sort,
    Guid? Container,
    string? Flag,
    string? Handover,
    Guid? Location,
    int? Page,
    int? PageSize) : IRequest<Result<PagedResult<DeviceListItem>>>;
