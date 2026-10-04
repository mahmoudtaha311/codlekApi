using System.Text.Json;
using Codlek.Core.Entities;

namespace Codlek.Core.Hardware;

/// <summary>
/// مقارنة لقطتين عتاد.
///
/// <para><b>خدمة نقية.</b> مفيش قاعدة بيانات ولا HTTP — بتاخد صفّين
/// وبترجّع فروق، عشان كل قاعدة تحت تتجرّب لوحدها من غير
/// سيرفر.</para>
///
/// <para>🔴 <b>المبدأ الحاكم: مانقولش «القطعة اتغيّرت» إلا لما
/// الدليل يقول كده.</b> ده مش تشدّد نظري — النظام ده بيتقال عليه إن
/// موظف غيّر قطعة، والاتهام ده بيتبني على السطور دي. فالافتراضي هو
/// <see cref="ChangeKind.IdentityUncertain"/> و
/// <see cref="ChangeKind.NotObserved"/>.</para>
/// </summary>
public static class SnapshotComparison
{
    /// <summary>
    /// ثقة الهوية زي ما الراكة بتكتبها: <c>1</c> = أ (سيريال
    /// حقيقي).
    /// </summary>
    private const int ConfidenceA = 1;

    /// <summary>
    /// الفئات اللي القارئ بيفلتر منها صفوف قبل ما يسجّل.
    ///
    /// <para>🔴 الشبكة بتستبعد الكروت الوهمية وUSB واللي مالوش MAC،
    /// والشاشات بتستبعد الخارجية. يعني اختفاء صف ممكن يكون تغيّر في
    /// <b>الفلترة</b> أو في الدرايفر — <b>مش</b> دليل إن القطعة
    /// اتشالت. فالفئات دي عمرها ما تدّي
    /// <see cref="ChangeKind.Removed"/>.</para>
    /// </summary>
    private static readonly HashSet<int> FiltersRowsOut =
        [ComponentType.Network, ComponentType.Display];

    // =================================================================
    //  المقارنة
    // =================================================================

    /// <param name="aPartial">لقطة أ ناقصة؟ (<c>SnapshotIsPartial</c>)</param>
    /// <param name="bPartial">لقطة ب ناقصة؟</param>
    public static List<ComponentDiff> Compare(
        IReadOnlyList<ReportSnapshotComponent> a,
        IReadOnlyList<ReportSnapshotComponent> b,
        bool aPartial = false,
        bool bPartial = false)
    {
        var result = new List<ComponentDiff>();

        var types = a.Select(c => c.Type)
            .Concat(b.Select(c => c.Type))
            .Distinct()
            .OrderBy(t => t);

        foreach (int type in types)
        {
            result.AddRange(CompareType(
                type,
                a.Where(c => c.Type == type).ToList(),
                b.Where(c => c.Type == type).ToList(),
                aPartial, bPartial));
        }

        return result;
    }

    private static List<ComponentDiff> CompareType(
        int type,
        List<ReportSnapshotComponent> a,
        List<ReportSnapshotComponent> b,
        bool aPartial,
        bool bPartial)
    {
        var diffs = new List<ComponentDiff>();

        /*
          🔴 **«الفئة اتفحصت» = طلّعت صف واحد على الأقل (موجود أو
          غائب صراحةً) واللقطة مش ناقصة.**

          من غير الشرط ده، فئة ماطلّعتش صفوف خالص كانت هتتقرا «كل
          حاجة اتشالت» — وهي في الحقيقة «ماحدش بصّ». الفرق ده هو
          الفرق بين تقرير سليم واتهام.
        */
        bool aEnumerated = a.Count > 0 && !aPartial;
        bool bEnumerated = b.Count > 0 && !bPartial;

        var usedB = new HashSet<ReportSnapshotComponent>();

        // --- ١ · مطابقة بالهوية القوية (سيريال) — مستقلة عن الترتيب ---
        foreach (var left in a.Where(HasStrongIdentity))
        {
            var match = b.FirstOrDefault(r =>
                !usedB.Contains(r) && HasStrongIdentity(r) && SameSerial(left, r));

            if (match is null) continue;

            usedB.Add(match);
            diffs.Add(StrongPair(type, left, match));
        }

        var leftOver = a.Where(c => !diffs.Any(d => ReferenceEquals(d.A, c))).ToList();
        var rightOver = b.Where(c => !usedB.Contains(c)).ToList();

        /*
          --- ٢ · مطابقة بمكان ثابت وفريد (لو موجود فعلاً) ---

          🔴 ده المكان الوحيد اللي `SerialChanged` مسموحة فيه:
          قطعتين في نفس المكان الفيزيائي المثبت، والسيريال اتغيّر.
        */
        if (HasUsableLocationKey(type, a) && HasUsableLocationKey(type, b))
        {
            foreach (var left in leftOver.ToList())
            {
                string key = LocationKey(type, left);

                if (key.Length == 0) continue;

                var match = rightOver.FirstOrDefault(r => LocationKey(type, r) == key);

                if (match is null) continue;

                leftOver.Remove(left);
                rightOver.Remove(match);

                diffs.Add(LocationPair(type, left, match));
            }
        }

        /*
          --- ٢٫٥ · مطابقة برقم الجهاز على البوردة ---

          🔴 **الرقم ده أدق من البصمة وأضعف من السيريال.**

          `PnPDeviceId` هو المسار اللي ويندوز شايف بيه القطعة
          (`USB\VID_5986&PID_212B&MI_00&13219401&0&0000`). جوّاه
          الموديل **ومكان القطعة على الناقل** — فهو بيفرّق بين
          كاميرتين متطابقتين على نفس اللاب، والبصمة مابتفرّقش.

          ⚠️ **وبيفضل مطابقة ضعيفة عن قصد.** المسار ده بيتكرّر بين
          لابين متطابقين (نفس الموديل = نفس المنافذ = نفس المسار)،
          فهو بيحدّد **المكان** مش **الوحدة**. لو اتحسب هوية قوية،
          إثبات الغياب كان بيصدّقه وقطعة اتبدّلت بين لابين متشابهين
          كانت هتعدّي كأنها «زي ما هي».

          ⚠️ وبييجي **قبل** البصمة مش بدلها: لو المسار اتغيّر لسبب
          شرعي (القطعة اتنقلت لمنفذ تاني، أو ويندوز أعاد تعدادها)،
          المطابقة بتكمّل على البصمة بعده بدل ما القطعة تبقى يتيمة
          وتطلع «هوية مش مؤكّدة» من غير داعي.
        */
        foreach (var left in leftOver.ToList())
        {
            if (HasStrongIdentity(left)) continue;

            string path = Norm(left.PnPDeviceId);

            if (path.Length == 0) continue;

            var match = rightOver.FirstOrDefault(r =>
                !HasStrongIdentity(r) && Norm(r.PnPDeviceId) == path);

            if (match is null) continue;

            leftOver.Remove(left);
            rightOver.Remove(match);

            diffs.Add(new ComponentDiff
            {
                Type = type,
                Kind = ChangeKind.Unchanged,
                A = left,
                B = match,
                MatchedBy = "رقم البوردة",
                StrongBothSides = false,
                Explanation = "نفس الرقم اللي البوردة شايفة بيه القطعة. "
                            + "الرقم ده بيثبت المكان على الناقل — مش سيريال فردي.",
            });
        }

        /*
          --- ٣ · مطابقة بالبصمة/الموديل — للهوية الضعيفة **بس** ---

          🔴 **المرحلة دي ممنوع تلمس قطعة عندها سيريال حقيقي.**

          قطعة بهوية قوية وصلت هنا يعني سيريالها **ماطابقش** حاجة في
          اللقطة التانية. لو طابقناها بالموديل بعد كده، هاردين
          مختلفين بنفس الموديل كانوا هيطلعوا «زي ما هما» — وده أسوأ
          خطأ ممكن الملف ده يعمله: **بيخفي تبديل حقيقي**.

          (فحوص المشروع القديم هي اللي كشفت ده — كانت المرحلة بتطابق
          أي حاجة.)
        */
        foreach (var left in leftOver.ToList())
        {
            if (HasStrongIdentity(left)) continue;

            string key = WeakKey(left);

            if (key.Length == 0) continue;

            var match = rightOver.FirstOrDefault(r =>
                !HasStrongIdentity(r) && WeakKey(r) == key);

            if (match is null) continue;

            leftOver.Remove(left);
            rightOver.Remove(match);

            diffs.Add(new ComponentDiff
            {
                Type = type,
                Kind = ChangeKind.Unchanged,
                A = left,
                B = match,
                MatchedBy = "بصمة/موديل",
                StrongBothSides = false,

                // ⚠️ نفس التكوين ≠ نفس القطعة. البصمة بتحدّد الموديل
                // مش الوحدة — لابين متطابقين بيدّوا نفس البصمة
                // بالظبط.
                Explanation = "التكوين متطابق. الهوية مش مؤكّدة — "
                            + "القطعة دي مالهاش سيريال فردي.",
            });
        }

        // --- ٤ · اللي فضل: الحكم بيعتمد على إثبات الغياب ---
        foreach (var left in leftOver)
            diffs.Add(Unpaired(type, left, isLeft: true, otherEnumerated: bEnumerated, other: b));

        foreach (var right in rightOver)
            diffs.Add(Unpaired(type, right, isLeft: false, otherEnumerated: aEnumerated, other: a));

        return diffs;
    }

    // =================================================================
    //  المطابقات
    // =================================================================

    private static ComponentDiff StrongPair(
        int type, ReportSnapshotComponent a, ReportSnapshotComponent b)
    {
        bool modelChanged = !string.Equals(
            Norm(a.Model), Norm(b.Model), StringComparison.Ordinal);

        return new ComponentDiff
        {
            Type = type,
            Kind = modelChanged ? ChangeKind.ModelChanged : ChangeKind.Unchanged,
            A = a,
            B = b,
            MatchedBy = "سيريال",
            StrongBothSides = true,

            // ⚠️ نفس السيريال = نفس القطعة. فرق الاسم قراءة أو
            // فيرموير — مش تبديل.
            Explanation = modelChanged
                ? "نفس القطعة (نفس السيريال) — بس نص الموديل اتغيّر. "
                  + "غالباً فرق في القراءة أو الفيرموير، مش تبديل."
                : "نفس القطعة — السيريال زي ما هو.",
        };
    }

    private static ComponentDiff LocationPair(
        int type, ReportSnapshotComponent a, ReportSnapshotComponent b)
    {
        bool bothStrong = HasStrongIdentity(a) && HasStrongIdentity(b);

        if (bothStrong && !SameSerial(a, b))
        {
            return new ComponentDiff
            {
                Type = type,
                Kind = ChangeKind.SerialChanged,
                A = a,
                B = b,
                MatchedBy = "مكان ثابت (" + LocationKey(type, a) + ")",
                StrongBothSides = true,
                Explanation = "نفس المكان الفيزيائي، وسيريال مختلف — "
                            + "القطعة اللي في المكان ده اتغيّرت.",
            };
        }

        return new ComponentDiff
        {
            Type = type,
            Kind = ChangeKind.ConfigurationDifferent,
            A = a,
            B = b,
            MatchedBy = "مكان ثابت (" + LocationKey(type, a) + ")",
            StrongBothSides = false,
            Explanation = "نفس المكان، بس الهوية مش قوية على الطرفين — "
                        + "مينفعش نقول القطعة اتغيّرت.",
        };
    }

    /// <summary>
    /// مكوّن مالوش نظير.
    ///
    /// <para>🔴 <b>هنا بالظبط بيتقرر الفرق بين «اتشال» و«ماحدش
    /// شافه».</b> <see cref="ChangeKind.Removed"/> عايزة تلات شروط
    /// مع بعض: هوية قوية، والفئة اتفحصت فعلاً في اللقطة التانية،
    /// والفئة دي مش من اللي القارئ بيفلتر منها صفوف.</para>
    /// </summary>
    private static ComponentDiff Unpaired(
        int type, ReportSnapshotComponent c, bool isLeft, bool otherEnumerated,
        IReadOnlyList<ReportSnapshotComponent> other)
    {
        // ⚠️ القطعة نفسها متسجّلة «مش موجودة» — دي مش قطعة، دي
        // ملاحظة غياب.
        if (!c.IsPresent)
        {
            return Soft(type, c, isLeft, ChangeKind.NotObserved,
                "اللقطة بتقول إن المكان ده فاضي.");
        }

        if (!otherEnumerated)
        {
            return Soft(type, c, isLeft, ChangeKind.NotObserved,
                "الفئة دي ماتفحصتش في اللقطة التانية — الغياب مش مثبت.");
        }

        /*
          🔴 **القراءة اللي فشلت مش قطعة اتشالت.**

          القارئ **مابيشيلش** الهارد اللي سيريـاله مااتقراش — بيسجّله
          ببصمة وثقة ب. فهارد سيريـاله اتقرا المرة اللي فاتت وفشل
          المرة دي بيبان كأنه **اختفى**، والصف الجديد بيبان كأنه
          **قطعة تانية**. يعني قراءة SMART فشلت = موظف اتّهم بتغيير
          قطعة.

          ⚠️ **فالقاعدة الصح مش «الفئة اتفحصت» — دي «الفئة اتفحصت
          بالكامل».** لو الناحية التانية فيها أي صف من نفس النوع
          بهوية ضعيفة، الصف ده ممكن يكون هو نفسه قطعتنا بقراءة فاشلة،
          فمينفعش نقول اتشالت. ولو كل صفوف النوع هناك بهوية قوية،
          فالقايمة كاملة والغياب مثبت فعلاً — وده اللي بيخلّي الهارد
          اللي **اتبدّل فعلاً** (سيريال قديم ← سيريال جديد، الاتنين
          مقروءين) يفضل بيطلع `Removed` + `Added` زي ما المفروض.

          ⚠️ **وصف الغياب الصريح داخل في الشرط ده من غير حالة خاصة.**
          لما القارئ يكتب `IsPresent = false` وبس (مفيش هارد خالص)،
          مجموعة «الصفوف الموجودة» بتبقى فاضية و`All` بترجّع `true` —
          فالغياب مثبت. المشروع القديم كان فيه شرط `otherSaysAbsent`
          منفصل واتشال لما التخريب أثبت إنه **مابيأثرش في أي مدخل**.
        */
        bool otherFullyRead = other
            .Where(o => o.Type == type && o.IsPresent)
            .All(HasStrongIdentity);

        /*
          ⚠️ **وشرط «الفئة اتفحصت» مش مكرّر هنا — خرج فوق.**

          القديم كان بيحسب `otherEnumerated && !FiltersRowsOut && otherFullyRead`
          **بعد** ما يرجّع بدري على `!otherEnumerated`، فالحد الأول
          كان صح دايماً. نقلناه لفوق ومشيناه من هنا: نفس النتيجة،
          وشرط واحد أقل يضلّل اللي بيقرا. (والمسخ أثبت إن الحدّين
          الباقيين الاتنين لسه محسوبين.)
        */
        bool canProveAbsence = !FiltersRowsOut.Contains(type) && otherFullyRead;

        if (!HasStrongIdentity(c) || !canProveAbsence)
        {
            return Soft(type, c, isLeft, ChangeKind.IdentityUncertain,
                HasStrongIdentity(c)
                    ? "الفئة دي القارئ بيستبعد منها صفوف، "
                      + "فاختفاء الصف مش دليل إن القطعة اتشالت."
                    : "مفيش سيريال فردي للقطعة دي، "
                      + "فمينفعش نتتبّعها بين اللقطتين.");
        }

        return new ComponentDiff
        {
            Type = type,
            Kind = isLeft ? ChangeKind.Removed : ChangeKind.Added,
            A = isLeft ? c : null,
            B = isLeft ? null : c,
            MatchedBy = "سيريال",
            StrongBothSides = true,
            Explanation = isLeft
                ? "القطعة دي كانت موجودة بسيريالها، ومش موجودة في اللقطة الأحدث."
                : "قطعة بسيريال جديد ظهرت، وماكانتش موجودة في اللقطة الأقدم.",
        };
    }

    /// <summary>
    /// ⚠️ حكم غير قاطع: <c>StrongBothSides = false</c> و
    /// <c>MatchedBy</c> فاضي — مفيش مطابقة حصلت أصلاً.
    /// </summary>
    private static ComponentDiff Soft(
        int type, ReportSnapshotComponent c, bool isLeft,
        ChangeKind kind, string explanation) =>
        new()
        {
            Type = type,
            Kind = kind,
            A = isLeft ? c : null,
            B = isLeft ? null : c,
            MatchedBy = "",
            Explanation = explanation,
        };

    // =================================================================
    //  المفاتيح
    // =================================================================

    private static bool HasStrongIdentity(ReportSnapshotComponent c) =>
        c.IsPresent
        && c.IdentityConfidence == ConfidenceA
        && !string.IsNullOrWhiteSpace(c.ManufacturerSerial);

    private static bool SameSerial(ReportSnapshotComponent a, ReportSnapshotComponent b) =>
        string.Equals(
            Norm(a.ManufacturerSerial), Norm(b.ManufacturerSerial), StringComparison.Ordinal);

    private static string WeakKey(ReportSnapshotComponent c)
    {
        if (!string.IsNullOrWhiteSpace(c.HardwareFingerprint))
            return "fp:" + Norm(c.HardwareFingerprint);

        if (!string.IsNullOrWhiteSpace(c.Model)) return "model:" + Norm(c.Model);

        return "";
    }

    /// <summary>
    /// مفتاح المكان — <b>بس لو كان مكان حقيقي</b>.
    ///
    /// <para>⚠️ <c>SlotOrPosition</c> مش موثوق لوحده: القارئ بيرجع
    /// لـ<c>"DIMM " + index</c> لما WMI ما تدّيش مكان، وده <b>ترتيب
    /// تعداد</b> مش مكان. فالذاكرة بناخد المكان من
    /// <c>AttributesJson</c> (<c>bank</c> و<c>locator</c>) اللي جاي
    /// من SMBIOS مباشرةً.</para>
    ///
    /// <para>⚠️ والأنواع التانية مالهاش مكان حقيقي أصلاً: التخزين
    /// <c>"Disk " + index</c> (ترقيم النظام)، والشبكة والجرافيك اسم
    /// الكارت، والشاشة رقم تسلسلي. مفيش فيهم مكان فيزيائي ثابت،
    /// فمفيش مفتاح مكان.</para>
    /// </summary>
    private static string LocationKey(int type, ReportSnapshotComponent c)
    {
        if (type != ComponentType.Memory) return "";

        if (string.IsNullOrWhiteSpace(c.AttributesJson)) return "";

        try
        {
            using var doc = JsonDocument.Parse(c.AttributesJson);

            if (doc.RootElement.ValueKind != JsonValueKind.Object) return "";

            string bank = Text(doc.RootElement, "bank");
            string locator = Text(doc.RootElement, "locator");

            // ⚠️ الاتنين فاضيين = القارئ استعمل البديل المبني على
            // الترتيب، وده مش مكان.
            if (bank.Length == 0 && locator.Length == 0) return "";

            return Norm(bank) + "|" + Norm(locator);
        }
        catch (JsonException)
        {
            // ⚠️ JSON بايظ مايوقّعش المقارنة — بيبقى «مفيش مكان» وخلاص.
            return "";
        }
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    /// <summary>
    /// المكان ينفع كمفتاح؟ لازم يكون موجود <b>وفريد</b> جوّه
    /// اللقطة.
    ///
    /// <para>🔴 الشرط ده بيقع فعلاً على عتاد حقيقي: اللاب اللي اتفحص
    /// بيرجّع <c>DeviceLocator = "DIMM 0"</c> <b>للشريحتين</b>،
    /// والفرق في <c>BankLabel</c> بس. من غير فحص التفرّد كنا هنطابق
    /// شريحة بشريحة تانية بالصدفة.</para>
    /// </summary>
    private static bool HasUsableLocationKey(
        int type, IReadOnlyList<ReportSnapshotComponent> set)
    {
        var keys = set.Select(c => LocationKey(type, c)).ToList();

        if (keys.Count == 0 || keys.Any(k => k.Length == 0)) return false;

        return keys.Distinct(StringComparer.Ordinal).Count() == keys.Count;
    }

    private static string Norm(string? value) => (value ?? "").Trim().ToUpperInvariant();

    // =================================================================
    //  تحذير على مستوى المقارنة كلها
    // =================================================================

    /// <summary>
    /// <para>⚠️ تغيّر مرساة النظام أو اللوحة الأم حاجة كبيرة بتتعرض
    /// بوضوح — <b>من غير</b> ما نقول إنه جهاز تاني. تبديل بوردة أو
    /// صيانة من الوكيل بيغيّروا المعرّفات دي بشكل شرعي
    /// تماماً.</para>
    /// </summary>
    public static string? MajorIdentityWarning(IEnumerable<ComponentDiff> diffs)
    {
        var major = diffs
            .Where(d => (d.Type == ComponentType.System || d.Type == ComponentType.Motherboard)
                        && d.Kind is ChangeKind.SerialChanged
                                  or ChangeKind.Added
                                  or ChangeKind.Removed)
            .ToList();

        if (major.Count == 0) return null;

        string types = string.Join("، ",
            major.Select(d => ComponentType.Arabic(d.Type)).Distinct(StringComparer.Ordinal));

        return "تغيّر في هوية الجهاز الأساسية (" + types + "). "
             + "ده ممكن يكون تبديل بوردة أو صيانة وكيل — محتاج مراجعة بني آدم، "
             + "ومش معناه تلقائياً إن ده جهاز تاني.";
    }
}
