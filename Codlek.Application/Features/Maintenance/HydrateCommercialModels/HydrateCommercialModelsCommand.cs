using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Maintenance;
using MediatR;

namespace Codlek.Application.Features.Maintenance.HydrateCommercialModels;

/// <summary>
/// بيملأ الاسم التجاري على <b>كل</b> أجهزة الشركة من فحوصها.
///
/// <para>🔴 <b>الاستقبال بيملاه للأجهزة اللي الدفعة لمستها بس.</b> الجهاز
/// اللي بيكسب فحوص من غير استقبال — أهمها الفحوص اليتيمة اللي اترطبت في
/// نفس الإقلاع — بيفضل على الموديل الخام (<c>LENOVO 81FK</c>) رغم إن
/// فحوصه شايلة <c>ideapad 330-15ICH</c>. القديم كان بيعمل اللفة دي مع
/// كل إقلاع (<c>DeviceCommercialModelHydrator.HydrateTenantAsync</c>).</para>
///
/// <para>⚠️ <b>مفيش اختراع أسامي.</b> القيمة الوحيدة اللي بتتكتب هي قيمة
/// الراكة قرأتها من عتاد اللاب واتخزّنت في فحص — وبنفس قاعدة الاستقبال
/// بالظبط (<see cref="Rack.IngestReports.CommercialModelHydration"/>).</para>
/// </summary>
public sealed record HydrateCommercialModelsCommand(Guid TenantId, int BatchSize = 500)
    : IRequest<Result<HydrationSweepResult>>;
