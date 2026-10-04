namespace Codlek.Application.Contracts.Hardware;

/// <summary>
/// العدّادات — <b>خمس خانات</b>.
///
/// <para>🔴 «مش مؤكّدة» و«ماتفحصتش» ليهم خانة لوحدهم. ضمّهم
/// لـ«اتغيّرت» بيخلّي الرقم يشيل حالات إحنا صراحةً مش قادرين
/// نثبتها — يعني اتهام من غير دليل. وضمّهم لـ«زي ما هي» بيخفي نقص
/// القراءة.</para>
/// </summary>
public sealed record CompareSummary(
    int Unchanged,
    int Changed,
    int Added,
    int Removed,
    int Uncertain);
