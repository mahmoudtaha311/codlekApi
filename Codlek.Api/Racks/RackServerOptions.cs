namespace Codlek.Api.Racks;

/// <summary>
/// إعدادات السيرفر اللي سطح الراكة بيقراها.
///
/// <para>🔴 <b>كلها من الإعدادات، ولا واحدة من الطلب.</b> العنوان
/// اللي بيتبعت للراكة وقت التسجيل بتفضل عليه <b>شهور</b> — فبناؤه
/// من ترويسة <c>Host</c> معناه إن اللي بيسجّل بيحدّد فين الراكة
/// هترفع شغلها.</para>
/// </summary>
public sealed class RackServerOptions
{
    public const string Section = "Server";

    /// <summary>
    /// العنوان الكامل اللي الراكة بترفع عليه.
    ///
    /// <para>⚠️ <b>كامل بالمسار</b> (<c>…/api/sync/reports</c>) —
    /// الراكة بتخزّنه زي ما هو.</para>
    /// </summary>
    public string SyncUrl { get; set; } = "";

    /// <summary>
    /// 🔴 <b>«أنا اتنقلت» — ويتحط على السيرفر <u>القديم</u>
    /// بس.</b>
    ///
    /// <para>لو الاتنين بيعلنوا، الراكات بتفضل تلف بينهم
    /// للأبد.</para>
    /// </summary>
    public string MovedTo { get; set; } = "";
}
