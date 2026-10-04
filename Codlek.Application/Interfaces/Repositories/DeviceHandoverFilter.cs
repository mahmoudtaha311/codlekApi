namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// اتسلّم ولا لسه — <b>ووجود مكان حالي هو العلامة</b>.
///
/// <para>⚠️ مفيش عمود «اتسلّم» في القاعدة؛ المكان هو اللي بيقول.
/// وعشان كده مسح المكان بيرجّع اللاب للمخزن تلقائياً من غير أي
/// عمود تاني يتحدّث.</para>
/// </summary>
public enum DeviceHandoverFilter
{
    Any,

    /// <summary><c>handover=yes</c> — عنده مكان.</summary>
    HandedOver,

    /// <summary><c>handover=no</c> — لسه في الورشة.</summary>
    InWorkshop
}
