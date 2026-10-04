using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.GetTimeline;

/// <summary>
/// خط زمن اللاب — <b>مصفّح</b>.
/// </summary>
/// <param name="PageSize">
/// ⚠️ <b>بيتجاهل — والحجم ثابت على ٢٥.</b>
///
/// <para>🔴 ودي حالة منقولة بوعي: القديم بياخد <c>pageSize</c> في
/// العنوان، <b>مابيستعملهوش</b>، وبيرجّعه في الرد كأنه اتطبّق.
/// يعني اللي بيطلب ٥٠ بياخد ٢٥ والرد بيقوله «الحجم ٥٠».</para>
///
/// <para>⚠️ <b>والسبب إن الحجم داخل في الحساب نفسه:</b> كل مصدر
/// بيجيب «الصفحة × الحجم» صف، والسقف (٢٠٠ صفحة) محسوب على ٢٥.
/// فتفعيله محتاج الاستراتيجية كلها تتغيّر، مش سطر. واللي اتعمل
/// هنا إن الرد بيقول الحجم <b>الحقيقي</b> — فالكدبة اتشالت
/// والسلوك زي ما هو.</para>
/// </param>
public sealed record GetDeviceTimelineQuery(Guid DeviceId, int? Page, int? PageSize)
    : IRequest<Result<PagedResult<TimelineEventItem>>>;
