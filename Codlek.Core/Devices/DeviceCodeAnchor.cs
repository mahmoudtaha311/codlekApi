using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Text;

namespace Codlek.Core.Devices;

/// <summary>
/// كود الجهاز <b>كمرساة هوية</b> — مش كعمود بس.
///
/// <para>🔴 <b>ليه الكود بيتسجّل مرتين.</b> العمود بيقول «كوده
/// دلوقتي إيه»، والمرساة بتقول «أنهي أكواد عدّت على اللاب ده». ومن
/// غير التاريخ ده، أول ما الكود يتغيّر القديم <b>بيختفي من
/// البحث</b> — واللي ماسك استيكر قديم في إيده مالوش أي طريقة يلاقي
/// اللاب.</para>
///
/// <para>⚠️ <b>والراكات القديمة مابتبعتش المرساة دي</b>، وأجهزة كتير
/// في القاعدة أكوادها اتكتبت قبل ما الفكرة توجد أصلاً. فالتسجيل
/// بيحصل هنا على <b>أي</b> كود بيعدّي — والتاريخ بيتبني لوحده مع كل
/// مزامنة.</para>
/// </summary>
public static class DeviceCodeAnchor
{
    /// <summary>اسم المصدر اللي بيتكتب على المرساة.</summary>
    public const string Source = "كود المخزن";

    /// <summary>
    /// بيتأكد إن الكود الحالي متسجّل كمرساة، والقديم اتعلّم مش نشط.
    ///
    /// <para>⚠️ <b>بيشتغل على المراسي المحمّلة بالفعل</b> وبيضيف في
    /// القايمة بس — مفيش أي استعلام زيادة على كل جهاز بيتزامن.</para>
    ///
    /// <para>🔴 <b>ومفيش أي لمسة تانية على الصف الموجود عن قصد.</b>
    /// تحديث «آخر ظهور» هنا بوقت دلوقتي كان بيخلّي المتتبّع يشوف
    /// الصف <b>متغيّر</b> في <u>كل</u> مزامنة — فجهاز ماتغيّرش فيه
    /// حاجة يرجع «اتحدّث» بدل «زي ما هو»، والعدّاد اللي المدير بيقيس
    /// بيه النشاط يبقى وهم.</para>
    /// </summary>
    public static void Ensure(Device device, DateTime nowUtc)
    {
        string code = (device.PublicCode ?? "").Trim();

        if (code.Length == 0) return;

        string normalized = ArabicText.Normalize(code);

        if (normalized.Length == 0) return;

        var existing = device.Identifiers.FirstOrDefault(
            i => i.Kind == DeviceIdentifierKind.CompanyCode
              && i.NormalizedValue == normalized);

        if (existing is null)
        {
            device.Identifiers.Add(new DeviceIdentifierRow
            {
                TenantId = device.TenantId,
                DeviceId = device.Id,
                Kind = DeviceIdentifierKind.CompanyCode,
                RawValue = code,
                NormalizedValue = normalized,
                Source = Source,
                Confidence = DeviceIdentityConfidence.A,
                IsActive = true,
                FirstSeenAtUtc = nowUtc,
                LastSeenAtUtc = nowUtc,
            });
        }
        else if (!existing.IsActive)
        {
            // ⚠️ الكود رجع للاب تاني بعد ما كان اتوقف — بنرجّعه نشط.
            existing.IsActive = true;
            existing.SupersededAtUtc = null;
        }

        // 🔴 والأكواد القديمة بتتعلّم مش نشطة — **ومابتتمسحش**.
        foreach (var old in device.Identifiers.Where(
                     i => i.Kind == DeviceIdentifierKind.CompanyCode
                       && i.NormalizedValue != normalized
                       && i.IsActive))
        {
            old.IsActive = false;
            old.SupersededAtUtc = nowUtc;
        }
    }

    /// <summary>
    /// نص البحث لجهاز — <b>وبكل كود عدّى عليه، مش الحالي بس</b>.
    ///
    /// <para>🔴 <b>وده كان بيقرا الكود الحالي لوحده.</b> يعني أول ما
    /// الكود يتغيّر، القديم بيختفي من البحث — واللي ماسك استيكر قديم
    /// مالوش أي طريقة يلاقي اللاب. وصاحب الشغل قال إن ده «مهم جدا
    /// جدا جدا».</para>
    /// </summary>
    public static string SearchText(Device device)
    {
        string codes = string.Join(
            " ",
            device.Identifiers
                .Where(i => i.Kind == DeviceIdentifierKind.CompanyCode)
                .Select(i => i.RawValue)
                .Concat([device.PublicCode])
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        return ArabicText.Combine(
            codes, device.LastKnownManufacturer, device.LastKnownModel,
            device.IdentityBasis);
    }
}
