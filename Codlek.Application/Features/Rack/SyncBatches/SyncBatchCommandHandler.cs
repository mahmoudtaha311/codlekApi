using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Sync;
using Codlek.Application.Contracts.Wire;
using Codlek.Application.Features.Rack.IngestReports;
using Codlek.Application.Features.Rack.SyncDevices;
using Codlek.Application.Features.Rack.SyncOperational;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Entities;
using Codlek.Core.Sync;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Rack.SyncBatches;

/// <inheritdoc cref="SyncBatchCommand"/>
///
/// <remarks>
/// <para>🔴 <b>أربع قواعد مجمّدة — منقولة من القديم بالحرف:</b></para>
/// <list type="number">
/// <item><c>results</c> بتسمّي <b>كل</b> <c>outboxId</c> وصل — الغياب
/// بيتقري نجاح والراكة بتمسح الصف.</item>
/// <item><c>status</c> مجموعة مقفولة من تلاتة، و<c>Rejected</c> هي
/// الوحيدة اللي مابتمسحش.</item>
/// <item>كل نتيجة بتتولد <c>Rejected</c> + <c>Retryable</c> — <b>ده
/// الفرق المقصود عن القديم</b> اللي كان بيولّدها <c>Applied</c>
/// (يعني أي مسار ينسى يحسم الصف كان بيمسحه).</item>
/// <item>الرد المخزّن بيتكتب بـ<see cref="RackWire.Wire"/>، وإعادة
/// قرايته بتفشل <b>مقفولة</b>.</item>
/// </list>
/// </remarks>
public sealed class SyncBatchCommandHandler(
    ISyncBatchRepository batches,
    DeviceSyncApplier devices,
    IDeviceReference deviceReference,
    IRequestHandler<IngestReportsCommand, Result<IngestResult>> ingest,
    OperationalSyncApplier operational,
    IUnitOfWork unitOfWork,
    ILogger<SyncBatchCommandHandler> log)
    : IRequestHandler<SyncBatchCommand, Result<SyncBatchOutcome>>
{
    /// <summary>
    /// ⚠️ <b>نفس خيارات قراية الفحوص بالحرف</b> — للحمولات كلها، زي
    /// القديم.
    /// </summary>
    private static JsonSerializerOptions Json => IngestReportsCommandHandler.Json;

    public async Task<Result<SyncBatchOutcome>> Handle(
        SyncBatchCommand command, CancellationToken cancellationToken)
    {
        var replay = await ReplayAsync(command.RackId, command.Request.BatchId, cancellationToken);

        if (replay is not null) return Result.Success(new SyncBatchOutcome(replay, Replayed: true));

        return Result.Success(await ApplyAsync(command, cancellationToken));
    }

    // =================================================================
    //  الإعادة
    // =================================================================

    /// <summary>
    /// الرد المسجّل لدفعة اتستقبلت قبل كده — ككائن.
    ///
    /// <para>⚠️ <b>كائن مش نص عن قصد.</b> المخزّن ممكن يكون بأي شكل من
    /// الاتنين (القديم PascalCase والجديد camelCase)، فبيتقرا بخيارات
    /// متسامحة وبيخرج على السلك بالشكل القانوني. من غير كده، الإعادة
    /// كانت بترجّع PascalCase والراكة بتقرا حسّاس لحالة الحروف — فالصف
    /// المرفوض كان بيتقفل ويضيع.</para>
    ///
    /// <para>🔴 <b>وبيفشل مقفول.</b> «فيه صف» و«الصف بيتقرا» حاجتين
    /// مختلفتين. لو الصف موجود ومحتواه مش صالح بنرمي — <c>null</c>
    /// معناها «مفيش دفعة قبل كده» والشغل كله كان هيتعاد، يعني سجل
    /// التكرار بيتحوّل لشغل جديد لمجرد إن رده مش مقروء. الرمي بيطلع
    /// <c>500</c>، والراكة بتعتبرها إعادة جدولة وبتحتفظ بالشغل.</para>
    /// </summary>
    public async Task<SyncBatchResponse?> ReplayAsync(
        Guid rackId, Guid batchId, CancellationToken ct = default)
    {
        if (batchId == Guid.Empty) return null;

        string? json = await batches.FindResponseJsonAsync(rackId, batchId, ct);

        // مفيش صف = مفيش دفعة قبل كده. ده المسار الوحيد اللي بيرجّع null.
        if (json is null) return null;

        SyncBatchResponse? stored;

        try
        {
            stored = JsonSerializer.Deserialize<SyncBatchResponse>(json, RackWire.Stored);
        }
        catch (JsonException ex)
        {
            log.LogError(ex,
                "رد دفعة مخزّن مش صالح — راكة {Rack}، دفعة {Batch}. الدفعة مش هتتعاد.",
                rackId, batchId);

            throw new InvalidOperationException(
                $"الرد المسجّل للدفعة {batchId} مش صالح. " +
                "الدفعة مش هتتنفّذ تاني عشان سجل التكرار مايضيعش.", ex);
        }

        if (stored is null)
        {
            log.LogError(
                "رد دفعة مخزّن فاضي — راكة {Rack}، دفعة {Batch}. الدفعة مش هتتعاد.",
                rackId, batchId);

            throw new InvalidOperationException(
                $"الرد المسجّل للدفعة {batchId} فاضي. " +
                "الدفعة مش هتتنفّذ تاني عشان سجل التكرار مايضيعش.");
        }

        return stored;
    }

    // =================================================================
    //  التطبيق
    // =================================================================

    private async Task<SyncBatchOutcome> ApplyAsync(SyncBatchCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var now = DateTime.UtcNow;

        var response = new SyncBatchResponse
        {
            BatchId = request.BatchId,
            ReceivedAtUtc = now,
            ServerTimeUtc = now,
        };

        var deviceItems = new List<(SyncItemResult Result, DeviceSyncPayload Dto)>();
        var reportItems = new List<(SyncItemResult Result, LaptopReportPayload Dto)>();
        var workItems = new List<(SyncItemRequest Item, SyncItemResult Result)>();
        var workflowEvents = new List<(SyncItemRequest Item, SyncItemResult Result)>();

        /*
          ⚠️ **الأجهزة الأول، مهما كان ترتيبها في المصفوفة.** فحص وجهازه
          في نفس الدفعة لازم يشتغلوا صح — من غير الترتيب ده الفحص بيلاقي
          جهازه لسه ماوصلش ويترفض وهو مش محتاج.
        */
        var ordered = (request.Items ?? [])
            .Where(i => i is not null)
            .OrderBy(i => SyncEntityTypes.ApplyOrder(i.EntityType))
            .ThenBy(i => i.OccurredAtUtc)
            .ThenBy(i => i.SequenceNumber)
            .ToList();

        foreach (var item in ordered)
        {
            /*
              🔴 **كل صف ليه نتيجة خاصة بيه — مش بحث بالمعرّف.**

              القديم كان بيدوّر على النتيجة بـ`First(r => r.OutboxId == …)`،
              فصفّين بنفس المعرّف كانوا بيكتبوا على نفس النتيجة والتاني
              بيفضل بالافتراضي — وكان «اتطبّق».
            */
            var result = new SyncItemResult
            {
                OutboxId = item.OutboxId,
                EntityId = item.EntityId ?? "",
                HashMatch = CheckHash(command, item),
            };

            response.Results.Add(result);

            string type = item.EntityType ?? "";

            /*
              🔴 **«اسم مش معروف» غير «السيرفر ده قديم».**

              الاتنين مش بيتعادوا آلياً — إعادة عمياء مش هتغيّر النتيجة.
              بس الراكة بتقرا الكود: `UnsupportedEntity` معناها «نفس
              الحمولة هتعدّي بعد تحديث الخادم»، فالصف بيتعلّم «مستنّي
              تحديث» ويفضل قابل للإعادة بإيد المدير.
            */
            if (!SyncEntityTypes.Supported.Contains(type))
            {
                if (SyncEntityTypes.Known.Contains(type))
                    Reject(result, SyncBatchCodes.UnsupportedEntity,
                        $"نوع «{type}» محتاج تحديث الخادم.", retryable: false);
                else
                    Reject(result, SyncBatchCodes.UnknownEntityType,
                        $"نوع مش معروف: {type}", retryable: false);

                continue;
            }

            switch (type.ToLowerInvariant())
            {
                case SyncEntityTypes.Device:
                    if (!TryParse<DeviceSyncPayload>(
                            item, result, "حمولة الجهاز مش JSON صالح: ", out var device))
                    {
                        break;
                    }

                    if (device is null || device.Id == Guid.Empty)
                    {
                        Reject(result, SyncBatchCodes.MissingFields,
                            "جهاز من غير رقم تعريف.", retryable: false);
                        break;
                    }

                    deviceItems.Add((result, device));
                    break;

                case SyncEntityTypes.Report:
                    if (!TryParse<LaptopReportPayload>(
                            item, result, "الحمولة مش JSON صالح: ", out var report))
                    {
                        break;
                    }

                    if (report is null || report.Id == Guid.Empty || report.StartedAtUtc == default)
                    {
                        Reject(result, SyncBatchCodes.MissingFields,
                            "الفحص من غير رقم تعريف أو تاريخ بداية.", retryable: false);
                        break;
                    }

                    reportItems.Add((result, report));
                    break;

                case SyncEntityTypes.RepairWorkItem:
                    workItems.Add((item, result));
                    break;

                case SyncEntityTypes.DeviceWorkflowEvent:
                    workflowEvents.Add((item, result));
                    break;
            }
        }

        if (await ApplyDevicesAsync(command, response, deviceItems, ct) is { } raced)
            return new SyncBatchOutcome(raced, Replayed: true);

        await ApplyReportsAsync(command, response, reportItems, ct);

        await ApplyOperationalAsync(command, response, workItems, workflowEvents, ct);

        response.Summary.Rejected =
            response.Results.Count(r => r.Status == SyncItemStatus.Rejected);

        return await RecordAsync(command, response, ct);
    }

    // =================================================================
    //  ١ · الأجهزة
    // =================================================================

    /// <summary>
    /// بيطبّق الأجهزة ويحفظها — <b>قبل</b> أي فحص عشان الاستقبال يلاقيها.
    /// </summary>
    /// <returns>
    /// رد متخزّن لو طلب تاني سبقنا بنفس الدفعة — وإلا <c>null</c>.
    /// </returns>
    private async Task<SyncBatchResponse?> ApplyDevicesAsync(
        SyncBatchCommand command, SyncBatchResponse response,
        List<(SyncItemResult Result, DeviceSyncPayload Dto)> items, CancellationToken ct)
    {
        if (items.Count == 0) return null;

        /*
          ⚠️ الطور ده ممكن يتنفّذ **مرتين** لو حصل سباق. عشان كده
          العدّادات بترجع لقيمتها قبل الطور في أول كل تنفيذ — من غير ده
          الإعادة كانت هتعدّ نفس الجهاز مرتين.
        */
        int appliedBefore = response.Summary.Applied;
        int unchangedBefore = response.Summary.Unchanged;

        async Task RunAsync()
        {
            response.Summary.Applied = appliedBefore;
            response.Summary.Unchanged = unchangedBefore;

            foreach (var (result, dto) in items)
            {
                var applied = await devices.ApplyAsync(command.TenantId, command.RackId, dto, ct);

                switch (applied.Outcome)
                {
                    case DeviceApplyOutcome.Rejected:
                        Reject(result,
                            applied.ErrorCode ?? SyncBatchCodes.DeviceRejected,
                            applied.ErrorMessage ?? "الجهاز اترفض.",
                            applied.Retryable);
                        break;

                    case DeviceApplyOutcome.Unchanged:
                        Settle(result, SyncItemStatus.Unchanged);
                        response.Summary.Unchanged++;
                        break;

                    default:
                        Settle(result, SyncItemStatus.Applied);
                        response.Summary.Applied++;
                        break;
                }
            }
        }

        await RunAsync();

        if (await unitOfWork.TrySaveChangesAsync(ct)) return null;

        /*
          🔴 **سباق حقيقي، مش حمولة غلط.**

          نفس الدفعة وصلت مرتين مع بعض. الاتنين قروا «الجهاز مش موجود»
          والاتنين عملوا صف، وواحد فيهم وقع على القيد. القديم في أوله
          كان بيطلّع ٥٠٠ على شغل سليم تماماً، والراكة بتعيد وتقع تاني.

          لو التاني كمّل وسجّل الدفعة، رده هو المرجع. لو لسه — الجهاز
          بقى موجود، فإعادة التطبيق بتمشي في مسار التحديث وبتعدّي. ولو
          الرفض مش سباق، الحفظ التاني بيرمي (٥٠٠ = الراكة بتحتفظ بالشغل).
        */
        var stored = await ReplayAsync(command.RackId, command.Request.BatchId, ct);

        if (stored is not null) return stored;

        await RunAsync();
        await unitOfWork.SaveChangesAsync(ct);

        return null;
    }

    // =================================================================
    //  ٢ · الفحوص
    // =================================================================

    private async Task ApplyReportsAsync(
        SyncBatchCommand command, SyncBatchResponse response,
        List<(SyncItemResult Result, LaptopReportPayload Dto)> items, CancellationToken ct)
    {
        if (items.Count == 0) return;

        var wanted = items
            .Where(r => r.Dto.DeviceId is { } id && id != Guid.Empty)
            .Select(r => r.Dto.DeviceId!.Value)
            .ToHashSet();

        /*
          🔴 **الترجمة عن طريق سلسلة الدمج — مش بحث في الأجهزة وبس.**

          راكة جديدة بتولّد معرّف محلي للاب، والسيرفر بيتعرّف عليه كجهاز
          موجود بمعرّف تاني. الفحص اللي بعده بيوصل بنفس المعرّف المحلي —
          والبوابة لو بتدوّر في الأجهزة بس كانت بترفض «لسه ماوصلش» وهو
          واصل، والرفض مؤقت يعني إعادة للأبد.
        */
        var present = wanted.Count == 0
            ? []
            : (await deviceReference.ResolveManyAsync(command.TenantId, wanted, ct))
                .Keys.ToHashSet();

        var ready = new List<(SyncItemResult Result, LaptopReportPayload Dto)>();

        foreach (var entry in items)
        {
            bool orphan = entry.Dto.DeviceId is { } id
                       && id != Guid.Empty
                       && !present.Contains(id);

            if (orphan)
            {
                /*
                  🔴 **إعادة ممكنة عن قصد.** الفحص سليم تماماً — جهازه بس
                  لسه ماوصلش. الرفض النهائي هنا معناه فحص بيتقفل من غير
                  جهاز للأبد.
                */
                Reject(entry.Result, SyncBatchCodes.DeviceNotSynced,
                    "الجهاز بتاع الفحص ده لسه ماوصلش. ابعت الجهاز الأول.",
                    retryable: true);

                log.LogWarning(
                    "فحص {Report} سبق جهازه {Device} — راكة {Rack}",
                    entry.Dto.Id, entry.Dto.DeviceId, command.RackCode);

                continue;
            }

            ready.Add(entry);
        }

        if (ready.Count == 0) return;

        var ingested = (await ingest.Handle(
            new IngestReportsCommand(
                command.TenantId, command.RackId, ready.Select(r => r.Dto).ToList()),
            ct)).Value;

        foreach (var (result, dto) in ready)
        {
            /*
              ⚠️ **«دخل الاستقبال» مش «اتقبل».** فحص بيدّعي فني من شركة
              تانية بيترفض جوّه، ولو اتقفل هنا «اتطبّق» الراكة كانت
              هتشيله من طابورها وهو ماوصلش.
            */
            if (ingested.RejectedById.TryGetValue(dto.Id, out var rejection))
            {
                Reject(result, rejection.Code, rejection.Message, rejection.Retryable);
                continue;
            }

            // ⚠️ «زي ما هو» بيتقفل «اتطبّق» زي القديم — اللي يهم الراكة
            //    إن الصف وصل. والعدّادات تحت بتفرّق.
            Settle(result, SyncItemStatus.Applied);
        }

        response.Summary.Applied += ingested.Added + ingested.Updated;
        response.Summary.Unchanged += ingested.Unchanged;

        // الفحوص وحدها — عدّاد المحطة بيتحرّك بده مش بـApplied.
        response.Summary.ReportsApplied += ingested.Added + ingested.Updated;
    }

    // =================================================================
    //  ٣ · الشغل التشغيلي — الأوامر، وبعدها الحركات
    // =================================================================

    private async Task ApplyOperationalAsync(
        SyncBatchCommand command, SyncBatchResponse response,
        List<(SyncItemRequest Item, SyncItemResult Result)> workItems,
        List<(SyncItemRequest Item, SyncItemResult Result)> workflowEvents,
        CancellationToken ct)
    {
        if (workItems.Count == 0 && workflowEvents.Count == 0) return;

        var source = new OperationalSource(
            command.TenantId, command.RackId, command.OfflineValidityDays);

        foreach (var (item, result) in workItems)
        {
            await ApplyOneAsync(command, response, item, result, async payload =>
            {
                var dto = JsonSerializer.Deserialize<RepairWorkItemSyncPayload>(payload, Json);

                return dto is null
                    ? OperationalOutcome.Reject(SyncBatchCodes.MissingFields, "أمر صيانة فاضي.")
                    : await operational.ApplyWorkItemAsync(source, dto, ct);
            });
        }

        // ⚠️ الحفظ بين الطورين: الحركة اللي بتشاور على أمر في نفس
        //    الدفعة بتلاقيه متثبّت.
        await unitOfWork.SaveChangesAsync(ct);

        foreach (var (item, result) in workflowEvents)
        {
            await ApplyOneAsync(command, response, item, result, async payload =>
            {
                var dto = JsonSerializer.Deserialize<DeviceWorkflowEventSyncPayload>(payload, Json);

                return dto is null
                    ? OperationalOutcome.Reject(SyncBatchCodes.MissingFields, "حركة فاضية.")
                    : await operational.ApplyWorkflowEventAsync(source, dto, ct);
            });
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ApplyOneAsync(
        SyncBatchCommand command, SyncBatchResponse response,
        SyncItemRequest item, SyncItemResult result,
        Func<string, Task<OperationalOutcome>> apply)
    {
        OperationalOutcome outcome;

        try
        {
            outcome = await apply(item.Payload ?? "");
        }
        catch (JsonException ex)
        {
            // ⚠️ `JsonException` بس — أي حاجة تانية (القاعدة مثلاً) بتطلع
            //    ٥٠٠ والراكة بتحتفظ بالشغل.
            Reject(result, SyncBatchCodes.InvalidPayload,
                "الحمولة مش JSON صالح: " + ex.Message, retryable: false);
            return;
        }

        if (outcome.Code is not null)
        {
            Reject(result, outcome.Code, outcome.Message ?? "اترفض.", outcome.Retryable);

            log.LogWarning(
                "صف تشغيلي اترفض — راكة {Rack}، نوع {Type}، كود {Code}",
                command.RackCode, item.EntityType, outcome.Code);

            return;
        }

        if (outcome.Unchanged)
        {
            Settle(result, SyncItemStatus.Unchanged);
            response.Summary.Unchanged++;
        }
        else
        {
            Settle(result, SyncItemStatus.Applied);
            response.Summary.Applied++;
        }
    }

    // =================================================================
    //  التسجيل
    // =================================================================

    /// <summary>
    /// بيسجّل الدفعة عشان إعادة الإرسال ترجّع نفس الرد.
    ///
    /// <para>⚠️ <b><see cref="RackWire.Wire"/></b> — الشكل القانوني.
    /// الصفوف القديمة بتفضل زي ما هي وبتتقرا بخيارات متسامحة؛ مفيش صف
    /// بيتعاد كتابته ومفيش هجرة.</para>
    /// </summary>
    private async Task<SyncBatchOutcome> RecordAsync(
        SyncBatchCommand command, SyncBatchResponse response, CancellationToken ct)
    {
        // ⚠️ دفعة من غير معرّف مالهاش سجل تكرار — الإعادة بتتطبّق تاني،
        //    وكل حاجة جوّاها ساكنة بمعرّفها.
        if (command.Request.BatchId == Guid.Empty)
            return new SyncBatchOutcome(response, Replayed: false);

        batches.Add(new SyncBatch
        {
            TenantId = command.TenantId,
            RackId = command.RackId,
            BatchId = command.Request.BatchId,
            ItemCount = response.Results.Count,
            ResponseJson = JsonSerializer.Serialize(response, RackWire.Wire),
        });

        if (await unitOfWork.TrySaveChangesAsync(ct))
            return new SyncBatchOutcome(response, Replayed: false);

        /*
          سباق: نفس الدفعة وصلت مرتين مع بعض، والقيد الفريد منع التانية.
          الرد المخزّن هو المرجع — من نفس مسار الإعادة بالظبط، فنفس
          الإعادة مابتطلعش بشكلين حسب التوقيت.
        */
        var stored = await ReplayAsync(command.RackId, command.Request.BatchId, ct);

        return stored is not null
            ? new SyncBatchOutcome(stored, Replayed: true)
            : new SyncBatchOutcome(response, Replayed: false);
    }

    // =================================================================
    //  مساعدات
    // =================================================================

    /// <summary>
    /// الهاش على <b>نفس النص</b> اللي وصل — مش على كائن اتعاد تسلسله.
    ///
    /// <para>⚠️ <b>الاختلاف مش رفض.</b> الحمولة ممكن تكون سليمة، بس
    /// الاختلاف معناه إن التسلسل القانوني اختلف بين الطرفين، وده بيهدّ
    /// كشف التغيير. بنسجّله عشان يتصلّح.</para>
    /// </summary>
    private bool CheckHash(SyncBatchCommand command, SyncItemRequest item)
    {
        string computed = Sha256(item.Payload ?? "");

        bool match = string.Equals(computed, item.PayloadHash, StringComparison.OrdinalIgnoreCase);

        if (!match && !string.IsNullOrWhiteSpace(item.PayloadHash))
        {
            log.LogWarning(
                "هاش الحمولة مش مطابق — راكة {Rack}، كيان {Entity}. متوقّع {Expected} ووصل {Actual}",
                command.RackCode, item.EntityId, item.PayloadHash, computed);
        }

        return match;
    }

    /// <summary>
    /// بيفكّ الحمولة — ولو الفكّ رمى بيرفض الصف ويرجّع <c>false</c>.
    ///
    /// <para>⚠️ <b>أي استثناء من الفكّ = حمولة غلط</b> — زي القديم.
    /// الفكّ من نص مابيلمسش قاعدة، فمفيش حاجة تانية ممكن تتبلع هنا.
    /// و<c>null</c> (الحمولة <c>null</c> حرفياً) مش فشل فكّ — المنادي
    /// بيرفضها برسالة النوع نفسه.</para>
    /// </summary>
    private static bool TryParse<T>(
        SyncItemRequest item, SyncItemResult result, string prefix, out T? value)
        where T : class
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(item.Payload ?? "", Json);
            return true;
        }
        catch (Exception ex)
        {
            value = null;
            Reject(result, SyncBatchCodes.InvalidPayload, prefix + ex.Message, retryable: false);
            return false;
        }
    }

    private static void Settle(SyncItemResult result, string status)
    {
        result.Status = status;
        result.Error = null;
    }

    private static void Reject(SyncItemResult result, string code, string message, bool retryable)
    {
        result.Status = SyncItemStatus.Rejected;
        result.Error = new SyncItemError { Code = code, Message = message, Retryable = retryable };
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
