using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// عطل مرتبط بأمر صيانة.
///
/// <para>🔴 <b>الكود والعنوان الاتنين متخزّنين هنا بالقيمة.</b> لو
/// خزّنا مفتاح للكتالوج بس، أول ما مدير يعدّل صياغة عطل في الكتالوج
/// هتتغيّر كل أوامر الصيانة القديمة بأثر رجعي — والسجل اللي بيتغيّر
/// ورا ظهرك مش سجل.</para>
/// </summary>
public class RepairWorkItemIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid WorkItemId { get; set; }
    public RepairWorkItem? WorkItem { get; set; }

    [MaxLength(40)]
    public string IssueCode { get; set; } = "";

    /// <summary>لقطة العنوان وقت الفتح.</summary>
    [MaxLength(160)]
    public string IssueTitleSnapshot { get; set; } = "";

    [MaxLength(40)]
    public string Category { get; set; } = "";

    /// <summary>
    /// العطل ده اتصلّح؟
    ///
    /// <para>⚠️ الإجابة هنا بتخصّ أمر الصيانة ده وبس. الفحص الأصلي
    /// اللي اكتشف العطل مابيتعدّلش — يفضل يقول إن العطل كان موجود.</para>
    /// </summary>
    public bool Resolved { get; set; }
}
