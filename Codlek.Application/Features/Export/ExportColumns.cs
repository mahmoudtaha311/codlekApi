using Codlek.Core.Spreadsheets;

namespace Codlek.Application.Features.Export;

/// <summary>
/// أعمدة كل ملف تصدير.
///
/// <para>🔴 <b>الترتيب هنا لازم يطابق ترتيب القيم في المعالج
/// بالحرف</b> — عمود زيادة في واحد من غير التاني بيزحلق كل الشيت،
/// والمدير بيقرا «الماركة» تحت عنوان «الموديل» ومايلاحظش.</para>
///
/// <para>⚠️ وفيه فحص بيقارن <b>عدد</b> الأعمدة بعدد القيم في كل
/// صف — هو اللي بيمسك الزحلقة دي.</para>
/// </summary>
internal static class ExportColumns
{
    public static readonly SheetColumn[] Reports =
    [
        new("كود الجهاز", 16), new("الماركة", 14), new("الموديل", 26),
        new("المعالج", 24), new("الرامات", 14), new("التخزين", 20),
        new("كارت الشاشة", 22), new("الشاشة", 20), new("السيريال", 18),
        new("الفني", 18), new("كود الفني", 12), new("المحطة", 12),
        new("بدأ", 18), new("المدة (دقيقة)", 13),
        new("نجح", 8), new("فشل", 8), new("تعذّر", 8), new("اتخطّى", 10),
        new("النتيجة", 12),
    ];

    public static readonly SheetColumn[] Devices =
    [
        new("الكود", 16), new("الماركة", 14), new("الموديل", 26),
        new("الحاوية", 16),
        new("الحالة", 14), new("الثقة", 10),
        new("المرحلة", 14), new("المكان", 18),
        new("عدد الفحوص", 12),
        new("أول ظهور", 18), new("آخر ظهور", 18),
    ];

    public static readonly SheetColumn[] Repairs =
    [
        new("رقم الأمر", 14), new("كود اللاب", 16), new("العطل", 36),
        new("الحالة", 16), new("الفني", 18),
        new("اتفتح", 18), new("بدأ", 18), new("خلص", 18),
    ];

    public static readonly SheetColumn[] Productivity =
    [
        new("الفني", 22), new("الكود", 12),
        new("الفحوصات", 10), new("متوسط الفحص (د)", 16),
        new("الصيانات", 10), new("متوسط الصيانة (د)", 17),
        new("آخر نشاط", 18),
        new("ناجح", 9), new("فاشل", 9), new("تعذّر", 9),
        new("غير موجود", 11), new("متخطّى", 10),
    ];

    public static readonly SheetColumn[] TechnicianSummary =
    [
        new("الفني", 20), new("الكود", 12), new("عدد الفحوص", 12),
        new("نجح", 10), new("فشل", 10), new("تعذّر", 10),
        new("متوسط المدة (دقيقة)", 18), new("آخر نشاط", 18),
    ];

    public static readonly SheetColumn[] TechnicianAccounts =
    [
        new("الاسم", 20), new("الكود", 12), new("اسم المستخدم", 16),
        new("الحالة", 10), new("التخصص", 12), new("القسم", 16),
        new("يفحص", 8), new("يصلّح", 8), new("سبب الإيقاف", 26),
        new("لازم يغيّر الباسورد", 16), new("آخر دخول", 18),
    ];

    public static readonly SheetColumn[] TechnicianReports =
    [
        new("كود الجهاز", 16), new("الماركة", 14), new("الموديل", 26),
        new("بدأ", 18), new("المدة (دقيقة)", 13),
        new("نجح", 8), new("فشل", 8), new("تعذّر", 8), new("اتخطّى", 10),
        new("المحطة", 12),
    ];

    public static readonly SheetColumn[] Racks =
    [
        new("الكود", 12), new("الاسم", 18), new("المكان", 18),
        new("الحالة", 12), new("النسخة", 14),
        new("اتعملت", 18), new("اتسجّلت", 18), new("آخر ظهور", 18),
        new("فحوص وصلت", 12), new("سبب الإلغاء", 26),
    ];

    public static readonly SheetColumn[] Audit =
    [
        new("الوقت", 18), new("اللي عمل", 20), new("النوع", 12),
        new("الإجراء", 20), new("نوع الكيان", 16), new("الكود", 16),
        new("الملخّص", 50),
    ];
}
