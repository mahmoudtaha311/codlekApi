using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Analytics;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Devices;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using MediatR;

namespace Codlek.Application.Features.Devices.GetTimeline;

/// <summary>
/// خط زمن اللاب — <b>دمج خمس مصادر في صفحة واحدة</b>.
///
/// <para>المصادر: صف اللاب نفسه (حدث الاكتشاف)، والفحوص،
/// والملاحظات، ولحظات أوامر الصيانة، والحركات التشغيلية.</para>
///
/// <para>🔴 <b>والتحميل محدود بالصفحة المطلوبة:</b> كل مصدر بيرجّع
/// على الأكثر «رقم الصفحة × ٢٥» صف، مش كل تاريخ اللاب. والدمج
/// والترتيب بيحصلوا في الذاكرة على العدد ده وبس.</para>
///
/// <para>⚠️ <b>والحجّة إن ده كفاية:</b> أعلى <c>need</c> حدث في
/// الدمج مستحيل يحتوي أكتر من <c>need</c> صف من أي مصدر — فاللي
/// برّه أعلى <c>need</c> بتاع مصدره برّه النتيجة أكيد.</para>
/// </summary>
public sealed class GetDeviceTimelineQueryHandler(
    IDeviceRepository devices, ICurrentUser me)
    : IRequestHandler<GetDeviceTimelineQuery, Result<PagedResult<TimelineEventItem>>>
{
    /// <summary>حدث واحد قبل ما يتحوّل لعقد — ومعاه مفتاح ترتيبه.</summary>
    private sealed record Entry(string SortKey, TimelineEventItem Item);

    public async Task<Result<PagedResult<TimelineEventItem>>> Handle(
        GetDeviceTimelineQuery query, CancellationToken cancellationToken)
    {
        var device = await devices.FindDetailAsync(
            me.TenantId, query.DeviceId, cancellationToken);

        if (device is null)
            return Result.Failure<PagedResult<TimelineEventItem>>(DeviceErrors.NotFound);

        var counts = await devices.TimelineCountsAsync(
            me.TenantId, query.DeviceId, cancellationToken);

        /*
          🔴 **الصفحة بتترد لآخر صفحة موجودة بدل ما ترجّع فاضي.**

          رابط محفوظ على صفحة ٧ للاب بقى عنده صفحتين لازم يعرض
          حاجة — مش شاشة فاضية.
        */
        int total = counts.Total;
        int page = DeviceTimelineOrder.ClampPage(query.Page, total);
        int need = DeviceTimelineOrder.Need(page);

        var reports = await devices.TimelineReportsAsync(
            me.TenantId, query.DeviceId, need, cancellationToken);

        var notes = await devices.TimelineNotesAsync(
            me.TenantId, query.DeviceId, need, cancellationToken);

        var moments = await devices.TimelineRepairMomentsAsync(
            me.TenantId, query.DeviceId, need, cancellationToken);

        var movements = await devices.TimelineMovementsAsync(
            me.TenantId, query.DeviceId, need, cancellationToken);

        /*
          ⚠️ **أسماء الأماكن والفنيين بقراية واحدة لكل جدول.**

          الحركة بتعرض «من مكان لمكان» و«من فني لفني»، فالمعرّفات
          بتتلم من كل الحركات مرة واحدة — قراية لكل سطر كانت بتبقى
          N+1 على لاب اتنقل عشرين مرة.
        */
        var places = await devices.LocationNamesAsync(
            me.TenantId,
            movements.SelectMany(m => new[] { m.FromLocationId, m.ToLocationId }),
            cancellationToken);

        var holders = await devices.HolderNamesAsync(
            me.TenantId,
            movements.SelectMany(m => new[] { m.FromTechnicianId, m.ToTechnicianId })
                .Concat(moments.SelectMany(m =>
                    new[] { m.AssignedTechnicianId, m.CompletedByTechnicianId })),
            cancellationToken);

        var entries = new List<Entry>(
            reports.Count + notes.Count + moments.Count + movements.Count + 1)
        {
            Discovery(device),
        };

        entries.AddRange(reports.Select(Report));
        entries.AddRange(notes.Select(Note));
        entries.AddRange(moments.Select(m => Moment(m, holders)));
        entries.AddRange(movements.Select(m => Movement(m, places, holders)));

        /*
          🔴 **الترتيب نصي بترتيب البايت — ومفيش ثقافة في المفتاح.**

          المفتاح «تيكات بتسعتاشر خانة | أولوية النوع | مفتاح الصف»،
          فالمقارنة النصية تساوي المقارنة الزمنية بالظبط. ومقارنة
          بثقافة كانت بتختلف على نفس البيانات من ماكينة لماكينة.
        */
        var items = entries
            .OrderByDescending(e => e.SortKey, StringComparer.Ordinal)
            .Skip(DeviceTimelineOrder.Skip(page))
            .Take(DeviceTimelineOrder.PageSize)
            .Select(e => e.Item)
            .ToList();

        return Result.Success(new PagedResult<TimelineEventItem>(
            Items: items,
            Page: page,

            // 🔴 الحجم الحقيقي — اللي اتطلب بيتجاهل، راجع التعليق
            //    على `GetDeviceTimelineQuery.PageSize`.
            PageSize: DeviceTimelineOrder.PageSize,

            TotalItems: total,
            TotalPages: DeviceTimelineOrder.Pages(total)));
    }

    // =================================================================
    //  الأحداث
    // =================================================================

    /// <summary>
    /// ⚠️ <b>حدث الاكتشاف مسقط من صف اللاب، مش صف في جدول.</b>
    /// وعشان كده الإجمالي بيزود واحد بالإيد.
    /// </summary>
    private static Entry Discovery(Core.Entities.Device d) =>
        new(
            DeviceTimelineOrder.SortKey(
                d.FirstSeenAtUtc,
                DeviceTimelineEventType.DeviceDiscovered,
                d.Id.ToString("N")),
            new TimelineEventItem(
                Kind: nameof(DeviceTimelineEventType.DeviceDiscovered),
                AtUtc: d.FirstSeenAtUtc,
                Title: DeviceTimelineText.Title(DeviceTimelineEventType.DeviceDiscovered),
                Summary: DeviceTimelineText.Discovery(
                    d.PublicCode, d.Confidence, d.IdentityBasis),

                // ⚠️ الاسم مش متاح هنا — صفحة التفاصيل هي اللي
                // بتقراه من أقدم فحص. والكود متخزّن على اللاب.
                ActorName: "",
                ActorCode: d.FirstSeenByTechnicianCode,

                ReportId: null,
                Counts: null,
                NoteBody: null));

    private static Entry Report(TimelineReportRow r) =>
        new(
            DeviceTimelineOrder.SortKey(
                r.StartedAtUtc,
                DeviceTimelineEventType.TestPerformed,
                r.ReportId.ToString("N")),
            new TimelineEventItem(
                Kind: nameof(DeviceTimelineEventType.TestPerformed),
                AtUtc: r.StartedAtUtc,
                Title: DeviceTimelineText.Title(DeviceTimelineEventType.TestPerformed),
                Summary: DeviceTimelineText.Test(r.EndedAtUtc, r.DurationMs),

                // ⚠️ الفني جاي من الفحص نفسه — الاسم والكود اتخزّنوا
                // وقت الفحص، فمفيش تفتيش في جدول الحسابات.
                ActorName: r.TechnicianName,
                ActorCode: r.TechnicianCode,

                // 🔴 خانة ميتة — بترجع null دايماً زي القديم.
                ReportId: null,

                Counts: new TestCounts(
                    r.PassCount, r.FailCount, r.ErrorCount, r.NotPresentCount, r.SkipCount),

                NoteBody: null));

    private static Entry Note(TimelineNoteRow n) =>
        new(
            DeviceTimelineOrder.SortKey(
                n.CreatedAtUtc,
                DeviceTimelineEventType.NoteAdded,

                // ⚠️ المفتاح `bigint` مصفوف لتسعتاشر خانة عشان
                // المقارنة النصية تساوي المقارنة الرقمية.
                n.Id.ToString("D19")),
            new TimelineEventItem(
                Kind: nameof(DeviceTimelineEventType.NoteAdded),
                AtUtc: n.CreatedAtUtc,
                Title: DeviceTimelineText.Title(DeviceTimelineEventType.NoteAdded),

                // ⚠️ الملخّص فاضي — النص نفسه في `NoteBody` عشان
                // الواجهة تعرضه كاقتباس.
                Summary: "",

                // ⚠️ كاتب الملاحظة مستخدم لوحة — اسمه لقطة اتخزّنت
                // وقت الكتابة، مش مفتاح أجنبي بيتغيّر بأثر رجعي.
                ActorName: n.CreatedByName,
                ActorCode: "",

                ReportId: null,
                Counts: null,
                NoteBody: n.Body));

    /// <summary>
    /// لحظة من أمر صيانة.
    ///
    /// <para>🔴 <b>ومين المنفّذ بيختلف باختلاف اللحظة، ودي مش
    /// تفصيلة شكلية.</b> «اتفتح» ممكن يعمله مدير من اللوحة، و«بدأت»
    /// بيعملها الفني المُسنَد ليه، و«خلصت» بيعملها اللي قفل الأمر —
    /// وساعات بيبقوا تلات ناس مختلفين. حطّ اسم واحد على التلاتة
    /// بيخلّي السطر يكدب على اتنين منهم.</para>
    ///
    /// <para>⚠️ <b>و«اتلغى» مالوش منفّذ.</b> الإلغاء مابيسجّلش مين
    /// عمله في صف الأمر، فالاسم بيفضل فاضي والسبب المكتوب بيتعرض.
    /// واسم مخمّن هنا كان هيبقى اتهام.</para>
    /// </summary>
    private static Entry Moment(
        TimelineRepairMomentRow m,
        IReadOnlyDictionary<Guid, TechnicianLabel> holders)
    {
        var type = Kind(m.Moment);

        var (freeText, parts, actor) = m.Moment switch
        {
            RepairMoment.Opened =>
                (m.FaultSummary, 0, new TechnicianLabel(m.OpenedByName, "")),

            RepairMoment.Started =>
                ("", 0, Holder(holders, m.AssignedTechnicianId)),

            RepairMoment.Completed =>
                (m.RepairActions, m.PartCount, Holder(holders, m.CompletedByTechnicianId)),

            RepairMoment.Unable =>
                (m.OutcomeReason, 0, Holder(holders, m.CompletedByTechnicianId)),

            // ⚠️ الملغي: السبب بيتعرض، والمنفّذ فاضي.
            _ => (m.OutcomeReason, 0, new TechnicianLabel("", "")),
        };

        return new Entry(
            DeviceTimelineOrder.SortKey(
                m.AtUtc,
                type,

                /*
                  ⚠️ **مفتاح الصف هنا معرّف الأمر وبس — من غير رقم
                  اللحظة.**

                  اللحظة داخلة في المفتاح أصلاً عن طريق أولوية
                  النوع، ولحظتين من نفس الأمر مستحيل يبقى ليهم نفس
                  النوع.
                */
                m.RepairId.ToString("N")),
            new TimelineEventItem(
                Kind: type.ToString(),
                AtUtc: m.AtUtc,
                Title: DeviceTimelineText.Title(type),
                Summary: DeviceTimelineText.Repair(m.PublicCode, freeText, parts),
                ActorName: actor.Name,
                ActorCode: actor.Code,
                ReportId: null,
                Counts: null,
                NoteBody: null));
    }

    private static Entry Movement(
        TimelineMovementRow m,
        IReadOnlyDictionary<Guid, string> places,
        IReadOnlyDictionary<Guid, TechnicianLabel> holders)
    {
        return new Entry(
            DeviceTimelineOrder.SortKey(
                // ⚠️ وقت **حصول** الحركة مش وقت تسجيلها — ده مكانها
                // في التاريخ. والفرق بيتقال في الملخّص.
                m.OccurredAtUtc,
                DeviceTimelineEventType.DeviceMoved,
                m.Id.ToString("D19")),
            new TimelineEventItem(
                Kind: nameof(DeviceTimelineEventType.DeviceMoved),
                AtUtc: m.OccurredAtUtc,

                // 🔴 العنوان من نوع الحركة — والمساعد ده مستقل عن
                //    بتاع شريط الصيانة، راجع `DeviceMovementTitle`.
                Title: DeviceMovementTitle.Of(m.EventType),

                Summary: DeviceTimelineText.Movement(
                    m.FromStage,
                    m.ToStage,
                    Place(places, m.FromLocationId),
                    Place(places, m.ToLocationId),
                    m.FromLocationId != m.ToLocationId,
                    Holder(holders, m.FromTechnicianId).Name,
                    Holder(holders, m.ToTechnicianId).Name,
                    m.FromTechnicianId != m.ToTechnicianId,
                    m.Reason,
                    m.OccurredAtUtc,
                    m.RecordedAtUtc),

                ActorName: m.ActorName,

                // ⚠️ ومفيش كود فاعل على الحركة — مفيش عمود.
                ActorCode: "",

                ReportId: null,
                Counts: null,
                NoteBody: null));
    }

    private static DeviceTimelineEventType Kind(RepairMoment moment) => moment switch
    {
        RepairMoment.Opened => DeviceTimelineEventType.RepairOpened,
        RepairMoment.Started => DeviceTimelineEventType.RepairStarted,
        RepairMoment.Completed => DeviceTimelineEventType.RepairCompleted,
        RepairMoment.Unable => DeviceTimelineEventType.RepairUnableToRepair,
        _ => DeviceTimelineEventType.RepairCancelled,
    };

    /// <summary>
    /// ⚠️ <b>المكان المش معروف «مفيش» مش فراغ.</b> سطر بيقول «اتنقل
    /// لـ» وبعدها فراغ بيبان مكسور.
    /// </summary>
    private static string Place(IReadOnlyDictionary<Guid, string> places, Guid? id) =>
        id is { } key && places.TryGetValue(key, out string? name)
            ? name
            : DeviceMovementTitle.Unknown;

    private static TechnicianLabel Holder(
        IReadOnlyDictionary<Guid, TechnicianLabel> holders, Guid? id) =>
        id is { } key && holders.TryGetValue(key, out var label)
            ? label
            : new TechnicianLabel(DeviceMovementTitle.Unknown, "");
}
