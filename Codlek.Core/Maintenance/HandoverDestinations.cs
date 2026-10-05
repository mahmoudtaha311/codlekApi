using Codlek.Core.Enums;

namespace Codlek.Core.Maintenance;

/// <summary>
/// الجهات الأربعة اللي مدير الدور بيسلّم لها.
///
/// <para>🔴 <b>نفس القديم بالحرف</b> (<c>DbSeeder.cs:85-91</c>) — الكود
/// والاسم والنوع والترتيب. والأربعة دول هما بالظبط اللي صاحب الشغل
/// عدّدهم؛ أي جهة خامسة قرار تشغيلي مكانها شاشة إدارة مش الكود ده.</para>
///
/// <para>⚠️ <b>المطابقة بالكود مش بالاسم.</b> لو حد عدّل الاسم المعروض،
/// التشغيل الجاي مايعملش صف تاني.</para>
/// </summary>
public static class HandoverDestinations
{
    public static readonly IReadOnlyList<(string Code, string Name, LocationKind Kind, int Sort)> All =
    [
        ("WH-5", "مخزن الخامس", LocationKind.Warehouse, 10),
        ("WH-6", "مخزن السادس", LocationKind.Warehouse, 20),
        ("WH-CPU", "مخزن معالجات اللابات", LocationKind.Warehouse, 30),
        ("SALES-LAPS", "مبيعات اللابات", LocationKind.Sales, 40),
    ];
}
