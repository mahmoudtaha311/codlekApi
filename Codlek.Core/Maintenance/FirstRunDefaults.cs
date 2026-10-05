namespace Codlek.Core.Maintenance;

/// <summary>
/// أول تشغيل على قاعدة فاضية: شركة واحدة وحساب مدير عام.
///
/// <para>🔴 <b>نفس قيم القديم بالحرف</b> (<c>DbSeeder.cs:16-18</c>):
/// <c>admin / admin</c> زي البرنامج المكتبي، عشان أول دخول على قاعدة
/// جديدة مايحتاجش حد يفتح القاعدة بإيده.</para>
///
/// <para>⚠️ <b>والباسورد ده معروف للكل</b> — فالحساب بيتعمل وعليه
/// «لازم يغيّر الباسورد»، وبوابة التغيير الإجباري مابتسيبوش يعمل أي
/// حاجة تانية قبل ما يغيّره.</para>
/// </summary>
public static class FirstRunDefaults
{
    public const string TenantName = "CODLEK";

    public const string OwnerUsername = "admin";

    public const string OwnerPassword = "admin";

    public const string OwnerDisplayName = "مدير";
}
