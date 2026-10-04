namespace Codlek.Tests;

/// <summary>
/// قاعدة فحوص مزامنة الأجهزة.
///
/// <para>🔴 <b>وقاعدة لوحدها عن قصد.</b> xunit بيشغّل كلاسات الفحوص
/// بالتوازي، وكل كلاس بيعمل نسخة من الـfixture بتاعته — فلو اتنين
/// شاركوا اسم قاعدة، الاتنين بيعملوا حذف وإنشاء على نفس القاعدة في
/// نفس اللحظة.</para>
///
/// <para>⚠️ وحصل فعلاً: الكلاس ده كان شغّال على قاعدة الاستقبال،
/// فالفحوص بتعدّي لوحدها وبتقع لما المجموعة كلها تتشغّل.</para>
/// </summary>
public sealed class DeviceSyncDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_device_sync_test";
}
