using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// توكن تجديد مصدَّر لمستخدم — <b>صف عشان ينفع يتلغي</b>.
///
/// <para>🔴 <b>ليه جدول وهو ممكن يبقى توكن موقّع وبس.</b> التوكن
/// الموقّع مالوش «إلغاء»: هو ورقة، واللي معاه الورقة بيدخل لحد ما
/// تنتهي. فلو حد سرق توكن تجديد عمره أسبوع، مفيش طريقة توقّفه غير
/// إنك تغيّر باسورد صاحبه — وساعتها بتقطع كل أجهزته هو كمان.</para>
///
/// <para>بالصف ده، الإلغاء بيبقى <b>سطر واحد</b>: علّم الصف
/// <see cref="RevokedAtUtc"/> وخلاص. الجلسة دي بس اللي بتقع.</para>
///
/// <para>🔴 <b>والتوكن نفسه عمره ما بيتخزّن — بصمته بس.</b> نفس قاعدة
/// الباسوردات. لو حد قرا الجدول، مايقدرش يستعمل اللي فيه.</para>
///
/// <para>⚠️ <b>جدول جديد، مش تعديل على جدول موجود.</b> الزيادة آمنة:
/// بتتعمل على الإنتاج وقت التحويل في دقيقة ومفيش صف قديم بيتلمس.</para>
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// بصمة التوكن (SHA-256 بالـbase64) — <b>مش التوكن</b>.
    ///
    /// <para>⚠️ الطول ٤٤ حرف ثابت، وده ناتج base64 لـ٣٢ بايت.</para>
    /// </summary>
    [MaxLength(64)]
    public string TokenHash { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// اتلغى إمتى — <c>null</c> يعني لسه شغّال.
    ///
    /// <para>⚠️ الصف مابيتمسحش. سجل «الجلسة دي اتقفلت الساعة كذا»
    /// بيفرّق بين «انتهت لوحدها» و«حد قفلها»، والفرق ده بيتسأل عنه
    /// لما يحصل حاجة.</para>
    /// </summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// التوكن اللي حلّ محلّه لما اتجدّد.
    ///
    /// <para>🔴 <b>ده اللي بيكشف التوكن المسروق.</b> التجديد بيلغي
    /// القديم ويعمل جديد. فلو التوكن القديم اتقدّم تاني بعد ما
    /// اتبدّل، يبقى فيه نسختين منه — واحدة عند صاحبه وواحدة عند حد
    /// تاني. الحالة دي معناها تسريب، والرد الصح إن السلسلة كلها
    /// تتقفل.</para>
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }

    /// <summary>سبب الإلغاء — للسجل.</summary>
    [MaxLength(80)]
    public string RevokedReason { get; set; } = "";
}
