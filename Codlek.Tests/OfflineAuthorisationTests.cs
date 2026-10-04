using Codlek.Core.Sync;

namespace Codlek.Tests;

/// <summary>
/// «الفني كان مصرّح له وقت الحركة؟» — <b>كل فرع لوحده</b>.
///
/// <para>🔴 <b>القاعدة: سحب الصلاحية بيمنع الشغل الجاي، مابيلغيش اللي
/// خلص.</b> من غير التفرقة، سحب صلاحية بيمسح شغل حصل فعلاً — جهاز
/// اتصلّح ومفيش سجل بيقول مين صلّحه.</para>
/// </summary>
public class OfflineAuthorisationTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>حالة سليمة كاملة — وكل فحص بيغيّر حاجة واحدة بس.</summary>
    private static string? Deny(
        DateTime? occurred = null,
        DateTime? lastLogin = null,
        bool noLogin = false,
        int days = 7,
        bool isActive = true,
        DateTime? suspendedAt = null,
        bool capability = true,
        DateTime? capabilityChangedAt = null) =>
        OfflineAuthorisation.Deny(
            occurred ?? Now.AddHours(-1),
            Now,
            noLogin ? null : lastLogin ?? Now.AddDays(-1),
            days,
            isActive,
            suspendedAt,
            capability,
            capabilityChangedAt);

    [Fact]
    public void A_logged_in_capable_active_technician_is_authorised()
    {
        Assert.Null(Deny());
    }

    // =================================================================
    //  ١ · الوقت
    // =================================================================

    /// <summary>
    /// ⚠️ <b>سماحية ٦ ساعات لفرق الساعة</b> — جهاز في ورشة ممكن
    /// يتظبط غلط، بس مش باب لتاريخ مستقبلي مخترع.
    /// </summary>
    [Fact]
    public void A_clock_slightly_ahead_is_tolerated()
    {
        Assert.Null(Deny(occurred: Now.AddHours(5), lastLogin: Now.AddDays(-1)));
    }

    [Fact]
    public void An_event_from_the_far_future_is_refused()
    {
        Assert.Equal(
            OfflineAuthorisation.FutureMessage,
            Deny(occurred: Now.AddHours(6).AddMinutes(1)));
    }

    [Fact]
    public void The_skew_boundary_is_inclusive()
    {
        Assert.Null(Deny(occurred: Now.Add(OfflineAuthorisation.ClockSkew), lastLogin: Now));
    }

    // =================================================================
    //  ٢ · الدخول المسجّل
    // =================================================================

    /// <summary>
    /// 🔴 <b>من غير دخول مسجّل على السيرفر = رفض.</b> وده الفرق بين
    /// قاعدة حقيقية وقاعدة على النية: من غيره، الراكة كانت تقدر تكتب
    /// أي تاريخ قديم في الحمولة وتعدّي.
    /// </summary>
    [Fact]
    public void No_recorded_login_means_no_authorisation()
    {
        Assert.Equal(OfflineAuthorisation.NoLoginMessage, Deny(noLogin: true));
    }

    /// <summary>
    /// 🔴 <b>وحتى لو مفيش صلاحية مطلوبة أصلاً</b> — الدخول المسجّل
    /// شرط قبل الصلاحية في الترتيب.
    /// </summary>
    [Fact]
    public void The_login_check_comes_before_the_capability_check()
    {
        Assert.Equal(
            OfflineAuthorisation.NoLoginMessage,
            Deny(noLogin: true, capability: true));
    }

    // =================================================================
    //  ٣ · النافذة
    // =================================================================

    [Fact]
    public void An_event_after_the_window_closed_is_refused()
    {
        var login = Now.AddDays(-10);

        Assert.Equal(
            OfflineAuthorisation.ExpiredMessage,
            Deny(occurred: login.AddDays(7).AddMinutes(1), lastLogin: login));
    }

    [Fact]
    public void The_window_edge_is_inside()
    {
        var login = Now.AddDays(-10);

        Assert.Null(Deny(occurred: login.AddDays(7), lastLogin: login));
    }

    /// <summary>
    /// ⚠️ <b>والنافذة السالبة بتتصفّر</b> — إعداد غلط مابيفتحش نافذة
    /// في الماضي، بيقفلها على لحظة الدخول.
    /// </summary>
    [Fact]
    public void A_negative_window_clamps_to_the_login_moment()
    {
        var login = Now.AddHours(-2);

        Assert.Null(Deny(occurred: login, lastLogin: login, days: -5));

        Assert.Equal(
            OfflineAuthorisation.ExpiredMessage,
            Deny(occurred: login.AddMinutes(1), lastLogin: login, days: -5));
    }

    // =================================================================
    //  ٤ · الإيقاف
    // =================================================================

    /// <summary>
    /// 🔴 <b>الإيقاف بيقطع النافذة من ساعته.</b> نافذة قديمة مابتديش
    /// حق شغل بعد الإيقاف.
    /// </summary>
    [Fact]
    public void Work_after_a_suspension_is_refused()
    {
        var suspended = Now.AddHours(-3);

        Assert.Equal(
            OfflineAuthorisation.SuspendedMessage,
            Deny(occurred: suspended.AddMinutes(1), isActive: false, suspendedAt: suspended));
    }

    /// <summary>
    /// 🔴 <b>والشغل قبل الإيقاف بيعدّي.</b> الإيقاف بيمنعه يدخل من
    /// بكرة؛ الصيانة اللي عملها إمبارح حصلت.
    /// </summary>
    [Fact]
    public void Work_before_a_suspension_still_counts()
    {
        var suspended = Now.AddHours(-3);

        Assert.Null(Deny(
            occurred: suspended.AddMinutes(-1), lastLogin: Now.AddDays(-1),
            isActive: false, suspendedAt: suspended));
    }

    [Fact]
    public void The_suspension_moment_itself_is_refused()
    {
        var suspended = Now.AddHours(-3);

        Assert.Equal(
            OfflineAuthorisation.SuspendedMessage,
            Deny(occurred: suspended, isActive: false, suspendedAt: suspended));
    }

    /// <summary>
    /// ⚠️ <b>وموقوف من غير وقت إيقاف مسجّل مابيترفضش بالإيقاف</b> —
    /// مفيش نقطة نقطع عندها.
    /// </summary>
    [Fact]
    public void Inactive_without_a_suspension_time_is_not_a_suspension()
    {
        Assert.Null(Deny(isActive: false, suspendedAt: null));
    }

    /// <summary>⚠️ ووقت إيقاف على فني شغّال (اتفك إيقافه) مالوش أثر.</summary>
    [Fact]
    public void A_lifted_suspension_does_not_bite()
    {
        Assert.Null(Deny(isActive: true, suspendedAt: Now.AddHours(-3)));
    }

    // =================================================================
    //  ٥ · الصلاحية
    // =================================================================

    /// <summary>
    /// 🔴 <b>الصلاحية مسحوبة بس الحركة حصلت قبل السحب = تعدّي.</b>
    /// الفني كان مصرّح له فعلاً ساعتها والشغل حصل.
    /// </summary>
    [Fact]
    public void Work_before_the_capability_was_revoked_still_counts()
    {
        var revoked = Now.AddHours(-1);

        Assert.Null(Deny(
            occurred: revoked.AddMinutes(-10), capability: false,
            capabilityChangedAt: revoked));
    }

    [Fact]
    public void Work_after_the_capability_was_revoked_is_refused()
    {
        var revoked = Now.AddHours(-2);

        Assert.Equal(
            OfflineAuthorisation.NoCapabilityMessage,
            Deny(occurred: revoked.AddMinutes(10), capability: false,
                capabilityChangedAt: revoked));
    }

    /// <summary>
    /// 🔴 <b>والصلاحية مسحوبة ومفيش وقت سحب = رفض</b> — مفيش دليل
    /// إنها كانت شغّالة يوم من الأيام.
    /// </summary>
    [Fact]
    public void A_missing_capability_with_no_history_is_refused()
    {
        Assert.Equal(
            OfflineAuthorisation.NoCapabilityMessage,
            Deny(capability: false, capabilityChangedAt: null));
    }

    [Fact]
    public void The_revocation_moment_itself_is_refused()
    {
        var revoked = Now.AddHours(-2);

        Assert.Equal(
            OfflineAuthorisation.NoCapabilityMessage,
            Deny(occurred: revoked, capability: false, capabilityChangedAt: revoked));
    }

    // =================================================================
    //  الترتيب
    // =================================================================

    /// <summary>
    /// ⚠️ <b>والترتيب نفسه عقد:</b> الإيقاف قبل الصلاحية — موقوف ومن
    /// غير صلاحية بياخد رسالة الإيقاف، لأنها اللي بتقول للمدير يعمل
    /// إيه.
    /// </summary>
    [Fact]
    public void Suspension_is_reported_before_capability()
    {
        var suspended = Now.AddHours(-3);

        Assert.Equal(
            OfflineAuthorisation.SuspendedMessage,
            Deny(occurred: suspended.AddMinutes(1), isActive: false,
                suspendedAt: suspended, capability: false));
    }

    [Fact]
    public void The_future_check_beats_everything()
    {
        Assert.Equal(
            OfflineAuthorisation.FutureMessage,
            Deny(occurred: Now.AddDays(2), noLogin: true, capability: false));
    }
}
