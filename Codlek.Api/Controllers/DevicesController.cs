using Codlek.Api.Authorization;
using Codlek.Api.Extensions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Features.Devices.AddNote;
using Codlek.Application.Features.Devices.GetDevice;
using Codlek.Application.Features.Devices.GetDevices;
using Codlek.Application.Features.Devices.GetIdentifiers;
using Codlek.Application.Features.Devices.GetLabel;
using Codlek.Application.Features.Devices.GetNotes;
using Codlek.Application.Features.Devices.GetTests;
using Codlek.Application.Features.Devices.GetTimeline;
using Codlek.Application.Features.Devices.LookupDevice;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Controllers;

/// <summary>
/// <c>/api/v1/devices</c> — قايمة اللابات.
///
/// <para>⚠️ <b>فيه تلات كنترولرات على نفس البادئة دي، وده
/// مقصود:</b> ده للقايمة، و<see cref="DeviceRepairsController"/>
/// لتبويب الصيانة، و<see cref="HardwareController"/> للعتاد. التقسيم
/// <b>بالقطاع</b> مش بالبادئة — زي ما القديم كان مقسّم على ملفات
/// (<c>ApiV1.Hardware.cs</c> · <c>ApiV1.Repairs.cs</c>). وASP.NET
/// بيجمعهم على نفس المسار عادي لأن قوالب الإجراءات مابتتعارضش.</para>
///
/// <para>⚠️ <b>والقايمة <c>ManagerOrAbove</c>:</b> الفني بيشوف
/// <b>فحوصاته</b>، مش قايمة لابات الورشة.</para>
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize(Policies.ManagerOrAbove)]
public sealed class DevicesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// ⚠️ <b>كل الفلاتر «أحسن مجهود».</b> القيمة المش مفهومة بتتجاهل
    /// — دي مدخلات من رابط محفوظ في المتصفح، والرد عمره ما بيبقى
    /// <c>400</c>.
    /// </summary>
    [HttpGet("")]
    [ProducesResponseType<PagedResult<DeviceListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? confidence,
        [FromQuery] string? outcome,
        [FromQuery] string? technician,
        [FromQuery] Guid? rack,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? stage,
        [FromQuery] string? sort,
        [FromQuery] Guid? container,
        [FromQuery] string? flag,
        [FromQuery] string? handover,
        [FromQuery] Guid? location,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetDevicesQuery(
                search, status, confidence, outcome, technician, rack, from, to,
                stage, sort, container, flag, handover, location, page, pageSize), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// مسح كود لاب — <b>مطابقة تامة</b>.
    ///
    /// <para>🔴 <b>ودي مش البحث.</b> البحث في القايمة بيمشي على
    /// <c>LIKE</c>؛ ده بياخد قيمة واحدة من ماسح باركود والمطلوب يا
    /// اللاب ده يا «مش موجود».</para>
    ///
    /// <para>⚠️ <b>و<c>lookup</c> قبل <c>{id:guid}</c> في الترتيب:</b>
    /// القيد <c>:guid</c> بيمنع التصادم أصلاً، بس الترتيب مكتوب كده
    /// عشان اللي بيقرا مايحتاجش يعرف الحكاية دي.</para>
    /// </summary>
    [HttpGet("lookup")]
    [ProducesResponseType<DeviceLookupResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Lookup([FromQuery] string? code, CancellationToken ct)
    {
        var result = await sender.Send(new LookupDeviceQuery(code), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// مراسي هوية اللاب — <b>والملغية معاها</b>.
    ///
    /// <para>🔴 المرساة اللي اتلغت هي اللي بتفسّر ليه بوردة اتغيّرت
    /// أو هارد اتبدّل. إخفاؤها بيخلّي الصفحة تقول حاجة ناقصة.</para>
    /// </summary>
    /// <summary>
    /// صفحة اللاب.
    ///
    /// <para>⚠️ <b>واللاب المدموج بيفتح.</b> دي صفحة تاريخه — اللي
    /// اندمج في غيره لازم يفتح ويقول إنه اندمج، مش يرجّع
    /// <c>404</c> على صف القايمة لسه مشاورة عليه.</para>
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<DeviceDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeviceQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id:guid}/identifiers")]
    [ProducesResponseType<IReadOnlyList<DeviceIdentifierItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Identifiers(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeviceIdentifiersQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id:guid}/notes")]
    [ProducesResponseType<IReadOnlyList<DeviceNoteItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Notes(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeviceNotesQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// ملاحظة جديدة على اللاب — <b>بتتضاف وبس</b>.
    ///
    /// <para>⚠️ <b>والرد هو الملاحظة نفسها بنفس شكل القايمة</b> — اللوحة
    /// تحطها فوق القايمة من غير ما تعيد القراية. ومفيش تعديل ولا مسح:
    /// التصحيح ملاحظة جديدة.</para>
    /// </summary>
    [HttpPost("{id:guid}/notes")]
    [ProducesResponseType<DeviceNoteItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddNote(
        Guid id, [FromBody] AddDeviceNoteRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new AddDeviceNoteCommand(id, body.Body), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// ليبل الـQR — <b>SVG متولّد على السيرفر</b>.
    ///
    /// <para>🔴 <b>على السيرفر عشان الرمز يطلع نفس رمز القديم بالحرف</b>
    /// (نفس المكتبة ونفس الإعدادات) — والليبلات المطبوعة من الشاشة
    /// القديمة ملزوقة على لابات.</para>
    ///
    /// <para>⚠️ <b><c>&lt;img src&gt;</c> مابيبعتش التوكن</b> — اللوحة
    /// لازم تجيبه بـ<c>fetch</c> مع التوكن. والناتج مفيهوش أي نص من
    /// المستخدم، فحطّه جوّه الصفحة آمن.</para>
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>مفيش <c>[Produces]</c> عن قصد:</b> كان هيقيّد رد الفشل
    /// كمان على <c>image/svg+xml</c>، ومفيش كاتب بيكتب
    /// <c>ProblemDetails</c> بالشكل ده — فالـ<c>404</c> كان هيبقى
    /// <c>406</c> من غير رسالة.
    /// </remarks>
    [HttpGet("{id:guid}/qr.svg")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "image/svg+xml")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Label(
        Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeviceLabelQuery(id), ct);

        return result.IsSuccess
            ? Content(result.Value.Svg, DeviceLabel.ContentType)
            : result.ToProblem();
    }

    [HttpGet("{id:guid}/tests")]
    [ProducesResponseType<PagedResult<DeviceTestItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Tests(
        Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeviceTestsQuery(id, page, pageSize), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    /// <summary>
    /// خط زمن اللاب — <b>دمج خمس مصادر</b>.
    ///
    /// <para>⚠️ <b>و<c>pageSize</c> بيتجاهل.</b> الحجم ثابت على ٢٥
    /// لأنه داخل في حساب الاستراتيجية نفسها (كل مصدر بيجيب «الصفحة
    /// × الحجم» صف). والرد بيقول الحجم <b>الحقيقي</b> — القديم كان
    /// بيرجّع اللي اتطلب كأنه اتطبّق.</para>
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [ProducesResponseType<PagedResult<TimelineEventItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Timeline(
        Guid id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeviceTimelineQuery(id, page, pageSize), ct);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
