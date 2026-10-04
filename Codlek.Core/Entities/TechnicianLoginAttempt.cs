using System.ComponentModel.DataAnnotations;

namespace Codlek.Core.Entities;

/// <summary>
/// محاولة دخول فني من محطة فحص — للتدقيق ولتقييد التخمين.
///
/// <para>⚠️ الباسورد نفسه عمره ما بيتكتب هنا ولا في أي سجل. الصف ده
/// بيقول «مين حاول، من أنهي محطة، ونجح ولا لأ» وبس.</para>
///
/// <para>وبنسجّل الاسم اللي اتكتب حتى لو مش موجود: محاولات كتير على
/// أسماء مش موجودة من نفس المحطة دي إشارة تخمين، والإشارة دي
/// بتضيع لو سجّلنا الصفوف اللي لقينا لها حساب بس.</para>
/// </summary>
public class TechnicianLoginAttempt
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>المحطة اللي المحاولة جت منها — دايماً متحققة بمفتاحها.</summary>
    public Guid RackId { get; set; }

    /// <summary>ممكن يبقى فاضي لو الاسم مش موجود أصلاً.</summary>
    public Guid? TechnicianId { get; set; }

    [MaxLength(60)]
    public string AttemptedUsername { get; set; } = "";

    public bool Success { get; set; }

    /// <summary>سبب الرفض بالعربي — من غير أي تلميح عن الباسورد.</summary>
    [MaxLength(200)]
    public string Reason { get; set; } = "";

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
