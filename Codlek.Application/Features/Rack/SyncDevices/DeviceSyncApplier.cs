using Codlek.Application.Contracts.Sync;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Sync;
using Codlek.Core.Text;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Rack.SyncDevices;

/// <summary>نتيجة تطبيق جهاز واحد.</summary>
public enum DeviceApplyOutcome
{
    Added,
    Updated,
    Unchanged,
    Rejected,
}

/// <summary>
/// نتيجة تطبيق جهاز — <b>بنفس لغة رد الدفعة</b>.
/// </summary>
/// <param name="CanonicalDeviceId">
/// 🔴 <b>الجهاز اتعرّف عليه كجهاز موجود — مااتعملش صف جديد.</b>
/// المعرّف اللي الراكة بعتته اتربط بالكانوني، فأي فحص جاي بنفس
/// المعرّف بيروح للجهاز الصح.
/// </param>
public sealed record DeviceApplyResult(
    DeviceApplyOutcome Outcome,
    Guid? CanonicalDeviceId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    bool Retryable = false)
{
    public static DeviceApplyResult Reject(
        string code, string message, bool retryable = false) =>
        new(DeviceApplyOutcome.Rejected, null, code, message, retryable);
}

/// <summary>
/// استقبال الأجهزة الجاية من الراكة.
///
/// <para>🔴 <b>والراكة هي اللي بتولّد المعرّف.</b> الفني ماسك اللاب
/// على بنش من غير نت، فالجهاز لازم يتعمل هناك دلوقتي. السيرفر بيعمل
/// <c>upsert</c> بالمعرّف ده — لو ولّد واحد جديد، كل مزامنة كانت
/// هتخلّق <b>نسخة تانية من نفس اللاب</b>.</para>
///
/// <para>🔴 <b>ومفيش دمج تلقائي.</b> جهازين بنفس المرساة بيتعلّموا
/// «يُشتبه أنه مكرر» ويستنّوا قرار بني آدم. تغيير بوردة شرعي بيدّي
/// نفس الإشارة بالظبط زي ليبل اتحط على جهاز تاني — والفرق بينهم مش
/// قرار برنامج.</para>
///
/// <para>⚠️ <b>ومابيحفظش</b> — المنادي هو اللي بيحفظ، عشان الجهاز
/// والفحص اللي بيشاور عليه يوصلوا مع بعض.</para>
/// </summary>
public sealed class DeviceSyncApplier(
    IDeviceSyncRepository devices,
    ILogger<DeviceSyncApplier> log)
{
    /// <summary>أكواد الرفض — <b>والراكة بتتفرّع عليها</b>.</summary>
    public const string MissingDeviceId = "MissingDeviceId";

    public const string AmbiguousIdentity = "AmbiguousIdentity";

    public const string DuplicateDeviceCode = "DuplicateDeviceCode";

    public async Task<DeviceApplyResult> ApplyAsync(
        Guid tenantId, Guid rackId, DeviceSyncPayload dto, CancellationToken ct = default)
    {
        if (dto.Id == Guid.Empty)
            return DeviceApplyResult.Reject(MissingDeviceId, "جهاز من غير رقم تعريف.");

        var now = DateTime.UtcNow;

        /*
          🔴 **الربط بيتترجم الأول.**

          راكة اتعرّفنا على جهازها قبل كده بتفضل تبعت بمعرّفها المحلي
          **للأبد** — لازم يتترجم كل مرة، من غير ما نطلب من الراكة
          تغيّر حاجة (الأوفلاين لازم يفضل شغّال).
        */
        Guid targetId = dto.Id;

        // 🔴 الجهاز ده وصلنا عن طريق التعرّف على هويته مش بمعرّفه؟ لو
        //    أيوه، كوده الدائم ليه قاعدة مختلفة — شوف تحت.
        bool resolvedByIdentity = false;

        var alias = await devices.FindAliasAsync(tenantId, dto.Id, ct);

        if (alias is not null)
        {
            targetId = alias.CanonicalDeviceId;
            resolvedByIdentity = true;
        }

        var row = await devices.FindWithIdentifiersAsync(tenantId, targetId, ct);

        bool isNew = row is null;

        // ⚠️ الربط اللي هيتضاف بعد ما التحقق يخلص — شوف التعليق تحت.
        DeviceAlias? pendingAlias = null;

        if (row is null)
        {
            /*
              🔴 **التعرّف على الهوية قبل إنشاء أي جهاز جديد.**

              ده العطل اللي اتكشف في الإنتاج: الراكة بتتعرّف على اللاب
              في قاعدتها المحلية وبس. راكة تانية ماشافتش اللاب دي
              بتعمل جهاز جديد بكود جديد، والسيرفر كان بيقبله زي ما هو
              **من غير ما يقارن ولا مرساة**.
            */
            var decision = await ResolveIdentityAsync(tenantId, dto, ct);

            if (decision.Outcome == IdentityOutcome.Ambiguous)
            {
                /*
                  ⚠️ **ممنوع نعمل جهاز تالت.** لما مرساة قوية تشاور
                  على جهاز ومرساة تانية تشاور على غيره، الإنشاء بيخفي
                  التعارض بدل ما يحله.
                */
                log.LogWarning(
                    "هوية ملتبسة للجهاز {Device}: {Reason}", dto.Id, decision.Reason);

                return DeviceApplyResult.Reject(
                    AmbiguousIdentity,
                    "هوية الجهاز ملتبسة: " + decision.Reason);
            }

            if (decision is { Outcome: IdentityOutcome.Resolved, DeviceId: { } found })
            {
                // اتعرّفنا عليه. بنربط معرّف الراكة بالكانوني
                // ومابنعملش صف جديد.
                targetId = found;
                resolvedByIdentity = true;

                /*
                  ⚠️ **الربط بيتأجّل لحد ما كل التحقق يخلص.**

                  الإضافة بتفضل قايمة في السياق حتى لو رجّعنا رفض
                  بعديها، فأول حفظ على صف تاني في نفس الدفعة بيكتبها.
                  **والرفض لازم يبقى معناه «مفيش أثر خالص»**.
                */
                pendingAlias = new DeviceAlias
                {
                    AliasDeviceId = dto.Id,
                    TenantId = tenantId,
                    CanonicalDeviceId = targetId,
                    SourceRackId = rackId,
                    Reason = decision.Reason,
                };

                row = await devices.FindWithIdentifiersAsync(tenantId, targetId, ct);

                isNew = false;

                log.LogInformation(
                    "الجهاز {Incoming} اتعرّف عليه كـ {Canonical}: {Reason}",
                    dto.Id, targetId, decision.Reason);
            }
        }

        /*
          🔴 **فحص تكرار الكود — بعد التعرّف على الهوية، مش قبله.**

          ده كان فوق، والترتيب اتكسر لما كود المخزن بقى هو الهوية.

          قبل كده كل راكة كانت بتخترع كود من بلوكها هي، فراكتين شايفين
          نفس اللاب بيبعتوا كودين مختلفين — والفحص عمره ما كان بيضرب.

          دلوقتي المخزن بيطبع استيكر واحد على اللاب، والراكتين بيقرأوا
          **نفس الكود**. فالفحص وهو فوق كان بيشوف الكود متسجّل لجهاز
          تاني ويرفض — **رفض نهائي مش بيتعاد** — قبل ما التعرّف على
          الهوية يلحق يقول إن ده نفس اللاب. النتيجة: **تاني راكة تفحص
          أي لاب مابتقدرش ترفعه أبداً**.

          ⚠️ وبعد التعرّف، الهدف بقى الكانوني — فنفس اللاب بيستثني
          نفسه، والكود المتكرر بيفضل مرفوض لما يكون على **هاردوير
          تاني فعلاً**.

          ⚠️ **ولسه مفيش أي تعديل اتعمل على السياق لحد السطر ده.**
        */
        if (!string.IsNullOrWhiteSpace(dto.PublicCode))
        {
            var clash = await devices.CodeClashAsync(
                tenantId, TextClip.To(dto.PublicCode, 20), targetId, ct);

            if (clash is { } other)
            {
                log.LogWarning(
                    "كود جهاز متكرر: {Code} على {Existing} و{Incoming}",
                    dto.PublicCode, other, dto.Id);

                return DeviceApplyResult.Reject(
                    DuplicateDeviceCode,
                    $"الكود {dto.PublicCode} متسجّل لجهاز تاني. محتاج مراجعة المدير.");
            }
        }

        // التحقق خلص — دلوقتي بس بنلمس السياق.
        if (pendingAlias is not null) devices.AddAlias(pendingAlias);

        if (row is null)
        {
            row = new Device
            {
                Id = dto.Id,
                TenantId = tenantId,
                FirstSeenAtUtc = ReportIngestRules.Utc(dto.FirstSeenAtUtc),
                FirstSeenByTechnicianCode = TextClip.To(dto.FirstSeenByTechnicianCode, 20),
                FirstSeenByRackId = rackId,
            };

            devices.Add(row);
        }

        string beforeCode = row.PublicCode;

        WriteCode(tenantId, row, dto, resolvedByIdentity, beforeCode, now);

        row.Confidence = (DeviceIdentityConfidence)dto.Confidence;
        row.IdentityBasis = TextClip.To(dto.IdentityBasis, 200);
        row.LastKnownManufacturer = TextClip.To(dto.LastKnownManufacturer, 80);
        row.LastKnownModel = TextClip.To(dto.LastKnownModel, 120);

        /*
          ⚠️ **والحالة مش بترجع لـ«شغّال» من الراكة.**

          لو المدير علّم الجهاز «مشكوك إنه مكرر» أو «متقاعد»، راكة
          بتزامن حمولة أقدم ماينفعش تلغي القرار ده.
        */
        if (row.Status == DeviceLifecycleStatus.Active)
            row.Status = (DeviceLifecycleStatus)dto.Status;

        var lastSeen = ReportIngestRules.Utc(dto.LastSeenAtUtc);
        if (lastSeen > row.LastSeenAtUtc) row.LastSeenAtUtc = lastSeen;

        var firstSeen = ReportIngestRules.Utc(dto.FirstSeenAtUtc);
        if (firstSeen != default && firstSeen < row.FirstSeenAtUtc)
            row.FirstSeenAtUtc = firstSeen;

        await ApplyContainerAsync(tenantId, row, dto, ct);

        ApplyIdentifiers(tenantId, row, dto);

        // 🔴 الكود الحالي بيتسجّل كمرساة كمان — مش بس كعمود.
        DeviceCodeAnchor.Ensure(row, now);

        // 🔴 والبحث بيشوف **كل** كود عدّى على اللاب.
        row.SearchText = DeviceCodeAnchor.SearchText(row);

        await FlagDuplicatesAsync(tenantId, row, ct);

        if (isNew)
            return new DeviceApplyResult(DeviceApplyOutcome.Added, row.Id);

        return new DeviceApplyResult(
            devices.WasTouched(row) || row.PublicCode != beforeCode
                ? DeviceApplyOutcome.Updated
                : DeviceApplyOutcome.Unchanged,
            row.Id);
    }

    /// <summary>
    /// الكود بييجي من استيكر المخزن اللي الفني قراه وكتبه.
    ///
    /// <para>⚠️ <b>ومش بيمسح كود موجود بفاضي</b> — راكة نسختها أقدم،
    /// أو فحص اتعمل قبل ما خانة الكود تتزوّد، مالهمش حق يشيلوا هوية
    /// جهاز.</para>
    ///
    /// <para>🔴 <b>والهاردوير المعروف بكود تاني لازم يتقال
    /// لإنسان.</b> القاعدة القديمة كانت: راكة اتعرّفنا على جهازها
    /// مالهاش حق تعيد تسميته. دي كانت صح لما الراكة بتخترع الأكواد من
    /// بلوك مؤجّر — كود مخترع مالوش حق يدهس هوية مطبوعة. دلوقتي الكود
    /// بيتكتب من استيكر المخزن، فهو <b>هو</b> الهوية المطبوعة، ورفضه
    /// بيخلّي الجهاز يفضل بكود قديم محدّش في الورشة بيشوفه.</para>
    ///
    /// <para>⚠️ <b>بس الحالتين شكلهم واحد من هنا:</b> اللاب اتعاد
    /// ترقيمه واستيكر جديد اتلزق، أو الفني كتب رقم غلط. والأولى لازم
    /// تعدّي والتانية لازم حد يشوفها — فبناخد الكود الجديد
    /// <b>ونسجّل الحركة</b>، ومحدّش بيخسر شغل في الحالتين.</para>
    /// </summary>
    private void WriteCode(
        Guid tenantId, Device row, DeviceSyncPayload dto, bool resolvedByIdentity,
        string beforeCode, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(dto.PublicCode)) return;

        string incoming = TextClip.To(dto.PublicCode, 20);

        bool renamed = resolvedByIdentity
            && !string.IsNullOrWhiteSpace(beforeCode)
            && !string.Equals(beforeCode, incoming, StringComparison.OrdinalIgnoreCase);

        row.PublicCode = incoming;

        // ⚠️ ٢ = «الكود مثبّت من المخزن» — الراكة بتبعته، وإحنا
        //    بنثبّته هنا عشان أي مسار تاني يقراه.
        row.CodeState = 2;

        if (!renamed) return;

        log.LogWarning(
            "الجهاز {Device} اتعرفنا عليه بالهاردوير وكوده اتغيّر من {Before} لـ {After} — "
            + "يا استيكر جديد يا غلطة كتابة، محتاج مراجعة",
            row.Id, beforeCode, incoming);

        devices.AddWorkflowEvent(new DeviceWorkflowEvent
        {
            TenantId = tenantId,
            DeviceId = row.Id,
            EventType = DeviceWorkflowEventType.CodeChanged,
            ActorName = "مزامنة الراكة",
            Reason = $"كود الجهاز اتغيّر من {beforeCode} لـ {incoming}",
            Notes = "الهاردوير اتعرفنا عليه بالمراسي، فالكود الجديد جه من استيكر تاني "
                + "أو من غلطة كتابة. محتاج مراجعة.",
            OccurredAtUtc = now,
            RecordedAtUtc = now,
        });
    }

    /// <summary>
    /// بيربط الجهاز بحاوية الاستيراد — <b>مرة واحدة وخلاص</b>.
    ///
    /// <para>🔴 <b>مابيكتبش فوق ربط موجود.</b> صاحب الشغل قال الحاوية
    /// «مش بتتغير». فلو الجهاز متربط خلاص، المزامنة الجاية برمز
    /// مختلف <b>بتتجاهل</b> — من غير القاعدة دي، راكة بتعيد إرسال
    /// حمولة قديمة كانت هتنقل اللاب لحاوية غلط <b>في صمت</b>.</para>
    ///
    /// <para>⚠️ <b>والرمز الفاضي معناه «ماتعملش حاجة»، مش
    /// «امسح».</b> الراكات القديمة مش عارفة الحقل ده أصلاً وبتبعته
    /// فاضي في كل مزامنة — لو الفاضي مسح، أول راكة قديمة كانت هتفضّي
    /// الحاويات كلها.</para>
    ///
    /// <para>⚠️ <b>والحاوية بتتعمل لو مش موجودة.</b> الفني بيكتب رمز
    /// جديد على راكة أوفلاين؛ رفض الرمز لأنه مش في الليستة كان هيوقف
    /// الفحص.</para>
    /// </summary>
    private async Task ApplyContainerAsync(
        Guid tenantId, Device row, DeviceSyncPayload dto, CancellationToken ct)
    {
        if (row.ContainerId is not null) return;

        string raw = (dto.ContainerCode ?? "").Trim();

        if (raw.Length == 0) return;

        raw = TextClip.To(raw, ContainerCode.MaxCodeLength);

        string key = ContainerCode.Normalize(raw);

        if (key.Length == 0) return;

        var container = await devices.FindContainerAsync(tenantId, key, ct);

        if (container is null)
        {
            container = new ImportContainer
            {
                TenantId = tenantId,
                Code = raw,
                NormalizedCode = key,
                CreatedByName = "مزامنة الراكة",
            };

            devices.AddContainer(container);
        }

        row.Container = container;
        row.ContainerId = container.Id;
    }

    /// <summary>
    /// المراسي <b>تاريخ مش أعمدة</b> — بنضيف الجديد ومبنمسحش القديم.
    ///
    /// <para>بوردة اتغيّرت بشكل شرعي: الصف القديم بيتعلّم مش نشط
    /// بالسبب، وصف جديد بيتضاف. الجهاز بيحتفظ بكوده وبكل تاريخه،
    /// والتغيير بيفضل <b>مرئي</b> بدل ما يتمسح.</para>
    /// </summary>
    private static void ApplyIdentifiers(Guid tenantId, Device row, DeviceSyncPayload dto)
    {
        foreach (var incoming in dto.Identifiers ?? [])
        {
            string normalized = (incoming.NormalizedValue ?? "").Trim();

            if (normalized.Length == 0) continue;

            var kind = (DeviceIdentifierKind)incoming.Kind;

            var existing = row.Identifiers.FirstOrDefault(
                i => i.Kind == kind && i.NormalizedValue == normalized);

            if (existing is not null)
            {
                /*
                  ⚠️ **«آخر ظهور» بيتقدّم بس — مابيترجعش.**

                  والشرط ده هو اللي بيمنع الصف يبان «متغيّر» في كل
                  مزامنة: حمولة بنفس الوقت مابتلمسش حاجة، فالجهاز
                  بيرجع «زي ما هو» صح.
                */
                var seen = ReportIngestRules.Utc(incoming.LastSeenAtUtc);

                if (seen > existing.LastSeenAtUtc) existing.LastSeenAtUtc = seen;

                /*
                  🔴 **وإلغاء التنشيط بيوصل من الراكة، بس الإرجاع
                  لأ.** مرساة اتلغت بقرار مراجعة ماتترجّعش بمزامنة.
                */
                if (!incoming.IsActive && existing.IsActive)
                {
                    existing.IsActive = false;

                    existing.SupersededAtUtc = incoming.SupersededAtUtc.HasValue
                        ? ReportIngestRules.Utc(incoming.SupersededAtUtc.Value)
                        : DateTime.UtcNow;

                    existing.SupersededReason = TextClip.To(incoming.SupersededReason, 300);
                    existing.SupersededByName = TextClip.To(incoming.SupersededByName, 120);
                }

                continue;
            }

            row.Identifiers.Add(new DeviceIdentifierRow
            {
                TenantId = tenantId,
                DeviceId = row.Id,
                Kind = kind,
                RawValue = TextClip.To(incoming.RawValue, 200),
                NormalizedValue = TextClip.To(normalized, 200),
                Source = TextClip.To(incoming.Source, 120),
                Confidence = (DeviceIdentityConfidence)incoming.Confidence,
                FirstSeenAtUtc = ReportIngestRules.Utc(incoming.FirstSeenAtUtc),
                LastSeenAtUtc = ReportIngestRules.Utc(incoming.LastSeenAtUtc),
                IsActive = incoming.IsActive,
                SupersededReason = TextClip.To(incoming.SupersededReason, 300),
                SupersededByName = TextClip.To(incoming.SupersededByName, 120),
                SupersededAtUtc = incoming.SupersededAtUtc.HasValue
                    ? ReportIngestRules.Utc(incoming.SupersededAtUtc.Value)
                    : null,
            });
        }
    }

    /// <summary>
    /// بيعلّم الجهاز لو فيه جهاز تاني شايل نفس المرساة.
    ///
    /// <para><b>وليه ده بيحصل أصلاً.</b> راكة اتعاد تنصيبها، أو راكة
    /// تانية شافت نفس اللاب — الاتنين بيولّدوا معرّف محلي جديد لنفس
    /// الجهاز الفيزيائي، والسيرفر بيستقبل صفّين مراسيهم واحدة.</para>
    ///
    /// <para>🔴 <b>وبيتعلّم بس، مبيتدمجش.</b> الدمج التلقائي بياخد
    /// قرار مالوش رجعة على أساس إشارة <b>غامضة</b>: نفس المرساة ممكن
    /// تبقى نفس اللاب فعلاً، وممكن تبقى بوردة اتنقلت، وممكن تبقى شركة
    /// بتكرّر سيريالاتها. التلاتة شكلهم واحد هنا. النظام بيقول «دول
    /// شكلهم واحد» <b>والمدير بيقرّر</b>.</para>
    ///
    /// <para>⚠️ <b>وبنعلّم كل الأطراف مش أول واحد.</b> تلات راكات
    /// شافوا نفس اللاب بيدّوا تلات صفوف؛ لو علّمنا واحد بس، الباقي
    /// بيفضل شكله سليم والمدير بيقفل الحالة وهو فاكر إنه خلّص.</para>
    /// </summary>
    private async Task FlagDuplicatesAsync(Guid tenantId, Device row, CancellationToken ct)
    {
        // ⚠️ المراسي اللي لسه ماتحفظتش مش في القاعدة، فبنجيبها من
        //    الكيان.
        var values = row.Identifiers
            .Where(i => i.IsActive && i.NormalizedValue.Length > 0)
            .Select(i => i.NormalizedValue)
            .Distinct()
            .ToList();

        if (values.Count == 0) return;

        var twins = await devices.TwinsByAnchorsAsync(tenantId, row.Id, values, ct);

        if (twins.Count == 0) return;

        log.LogWarning(
            "الجهاز {Incoming} بيشارك مرساة مع {Count} جهاز تاني — اتعلّموا للمراجعة، "
            + "**من غير دمج**",
            row.Id, twins.Count);

        if (row.Status == DeviceLifecycleStatus.Active)
            row.Status = DeviceLifecycleStatus.DuplicateSuspected;

        foreach (var other in await devices.ActiveDevicesAsync(tenantId, twins, ct))
            other.Status = DeviceLifecycleStatus.DuplicateSuspected;
    }

    /// <summary>
    /// بيبني المراسي من الحمولة وبيسأل عليها.
    ///
    /// <para>⚠️ <b>الراكة بتبعت المراسي صراحةً</b>، فمفيش داعي نعيد
    /// استخراجها من المواصفات.</para>
    /// </summary>
    private async Task<IdentityDecision> ResolveIdentityAsync(
        Guid tenantId, DeviceSyncPayload dto, CancellationToken ct)
    {
        var probes = new List<AnchorProbe>();

        foreach (var anchor in dto.Identifiers ?? [])
        {
            var kind = (DeviceIdentifierKind)anchor.Kind;
            string raw = anchor.RawValue ?? "";

            string match = DeviceIdentity.MatchValue(kind, raw);

            var matched = match.Length == 0
                ? []
                : await devices.MatchAnchorAsync(
                    tenantId, kind, match, null, DeviceIdentity.ProbeTake, ct);

            probes.Add(new AnchorProbe(
                kind, raw, match,
                DeviceIdentity.IsStrong(kind),
                DeviceIdentity.Grade(kind, raw),
                matched));
        }

        return DeviceIdentity.Decide(probes);
    }
}
