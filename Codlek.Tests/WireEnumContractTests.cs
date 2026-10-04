using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// الأرقام اللي بتعدّي على السلك — <b>مجمّدة</b>.
///
/// <para>🔴 <b>ليه الملف ده أول حاجة اتكتبت في المشروع الجديد.</b>
/// الراكة (تطبيق الويندوز في الورشة) بتقرا القيم دي <b>كأرقام</b> مش
/// كأسماء. لو <c>InProgress</c> بقت ٣ بدل ٢، الراكة هتفهم «قيد
/// الصيانة» على إنها «تمت الصيانة» — من غير أي خطأ، ومن غير ما حد
/// ياخد باله، والشغل في الورشة يتسجّل غلط.</para>
///
/// <para>⚠️ <b>والنقل من المشروع القديم هو بالظبط اللحظة اللي الغلط
/// ده بيحصل فيها:</b> حد بيرتّب الأسماء أبجدياً، أو بيشيل قيمة مش
/// مستعملة من النص، أو بيسيب المترجم يرقّم لوحده. الفحص ده بيوقّع
/// البناء وقتها.</para>
///
/// <para>⚠️ وفيه توأم ليه على الراكة
/// (<c>tools\SpicsHarness\RepairChecks.cs</c>) وفي فحوص المشروع
/// القديم (<c>OperationalPayloadContractTests</c>). التلاتة بيقولوا
/// نفس الحاجة عن قصد — السلك ليه طرفين.</para>
/// </summary>
public class WireEnumContractTests
{
    /// <summary>
    /// 🔴 الأرقام بالظبط زي المشروع القديم.
    ///
    /// <para>كل قيمة مكتوبة بالاسم والرقم. فحص بيقارن العدد بس كان
    /// بيعدّي على إعادة ترتيب.</para>
    /// </summary>
    [Fact]
    public void Every_value_keeps_the_number_the_rack_reads()
    {
        Assert.Equal(0, (int)UserRole.Technician);
        Assert.Equal(1, (int)UserRole.Manager);
        Assert.Equal(2, (int)UserRole.Owner);
        Assert.Equal(3, (int)UserRole.FloorManager);
        Assert.Equal(4, (int)UserRole.Accountant);

        Assert.Equal(0, (int)RackStatus.PendingPairing);
        Assert.Equal(1, (int)RackStatus.Active);
        Assert.Equal(2, (int)RackStatus.Suspended);
        Assert.Equal(3, (int)RackStatus.Revoked);

        Assert.Equal(0, (int)DeviceIdentityConfidence.None);
        Assert.Equal(1, (int)DeviceIdentityConfidence.A);
        Assert.Equal(2, (int)DeviceIdentityConfidence.B);
        Assert.Equal(3, (int)DeviceIdentityConfidence.C);

        Assert.Equal(0, (int)DeviceIdentifierKind.SystemUuid);
        Assert.Equal(1, (int)DeviceIdentifierKind.BiosSerial);
        Assert.Equal(2, (int)DeviceIdentifierKind.BoardSerial);
        Assert.Equal(3, (int)DeviceIdentifierKind.DiskSerial);
        Assert.Equal(4, (int)DeviceIdentifierKind.MacAddress);
        Assert.Equal(5, (int)DeviceIdentifierKind.PanelEdidSerial);
        Assert.Equal(6, (int)DeviceIdentifierKind.BatterySerial);
        Assert.Equal(7, (int)DeviceIdentifierKind.CompanyCode);

        Assert.Equal(0, (int)DeviceLifecycleStatus.Active);
        Assert.Equal(1, (int)DeviceLifecycleStatus.DuplicateSuspected);
        Assert.Equal(2, (int)DeviceLifecycleStatus.Merged);
        Assert.Equal(3, (int)DeviceLifecycleStatus.Retired);

        Assert.Equal(0, (int)DeviceCodeLeaseStatus.Open);
        Assert.Equal(1, (int)DeviceCodeLeaseStatus.Exhausted);
        Assert.Equal(2, (int)DeviceCodeLeaseStatus.Cancelled);

        Assert.Equal(0, (int)TechnicianSpecialty.None);
        Assert.Equal(1, (int)TechnicianSpecialty.Testing);
        Assert.Equal(2, (int)TechnicianSpecialty.Boards);
        Assert.Equal(3, (int)TechnicianSpecialty.Batteries);
        Assert.Equal(4, (int)TechnicianSpecialty.Screens);
        Assert.Equal(5, (int)TechnicianSpecialty.Refinishing);
        Assert.Equal(6, (int)TechnicianSpecialty.Grading);

        Assert.Equal(0, (int)DeviceOperationalStage.Unknown);
        Assert.Equal(1, (int)DeviceOperationalStage.Received);
        Assert.Equal(2, (int)DeviceOperationalStage.Testing);
        Assert.Equal(3, (int)DeviceOperationalStage.Tested);
        Assert.Equal(4, (int)DeviceOperationalStage.NeedsRepair);
        Assert.Equal(5, (int)DeviceOperationalStage.UnderRepair);
        Assert.Equal(6, (int)DeviceOperationalStage.Ready);
        Assert.Equal(7, (int)DeviceOperationalStage.WithSales);
        Assert.Equal(8, (int)DeviceOperationalStage.AtPointOfSale);
        Assert.Equal(9, (int)DeviceOperationalStage.Returned);

        Assert.Equal(0, (int)LocationKind.Warehouse);
        Assert.Equal(1, (int)LocationKind.Testing);
        Assert.Equal(2, (int)LocationKind.Repair);
        Assert.Equal(3, (int)LocationKind.Sales);
        Assert.Equal(4, (int)LocationKind.PointOfSale);

        Assert.Equal(0, (int)RepairStatus.New);
        Assert.Equal(1, (int)RepairStatus.WaitingForRepair);
        Assert.Equal(2, (int)RepairStatus.InProgress);
        Assert.Equal(3, (int)RepairStatus.Completed);
        Assert.Equal(4, (int)RepairStatus.UnableToRepair);
        Assert.Equal(5, (int)RepairStatus.Cancelled);

        Assert.Equal(0, (int)RepairApproval.Pending);
        Assert.Equal(1, (int)RepairApproval.Approved);
        Assert.Equal(2, (int)RepairApproval.Rejected);

        Assert.Equal(0, (int)DeviceWorkflowEventType.StageChanged);
        Assert.Equal(1, (int)DeviceWorkflowEventType.CustodyHandoff);
        Assert.Equal(2, (int)DeviceWorkflowEventType.LocationMoved);
        Assert.Equal(3, (int)DeviceWorkflowEventType.SentToRepair);
        Assert.Equal(4, (int)DeviceWorkflowEventType.RepairStarted);
        Assert.Equal(5, (int)DeviceWorkflowEventType.RepairCompleted);
        Assert.Equal(6, (int)DeviceWorkflowEventType.DispatchedToSales);
        Assert.Equal(7, (int)DeviceWorkflowEventType.DispatchedToPointOfSale);
        Assert.Equal(8, (int)DeviceWorkflowEventType.ReturnReceived);
        Assert.Equal(9, (int)DeviceWorkflowEventType.CodeChanged);
    }

    /// <summary>
    /// 🔴 <b>ومحدش زوّد قيمة في النص.</b>
    ///
    /// <para>الفحص اللي فوق بيثبّت اللي موجود. ده بيمسك الحالة
    /// التانية: حد ضاف قيمة جديدة <b>في وسط</b> الـenum، فكل اللي
    /// بعدها اتزحزح رقم. العدد بيقع فيبان.</para>
    ///
    /// <para>⚠️ ولو زوّدت قيمة جديدة عن قصد، لازم تبقى <b>في الآخر</b>
    /// وتعدّل الرقم هنا — والراكة لازم تتحدّث قبل ما تستعملها.</para>
    /// </summary>
    [Theory]
    [InlineData(typeof(UserRole), 5)]
    [InlineData(typeof(RackStatus), 4)]
    [InlineData(typeof(DeviceIdentityConfidence), 4)]
    [InlineData(typeof(DeviceIdentifierKind), 8)]
    [InlineData(typeof(DeviceLifecycleStatus), 4)]
    [InlineData(typeof(DeviceCodeLeaseStatus), 3)]
    [InlineData(typeof(TechnicianSpecialty), 7)]
    [InlineData(typeof(DeviceOperationalStage), 10)]
    [InlineData(typeof(LocationKind), 5)]
    [InlineData(typeof(RepairStatus), 6)]
    [InlineData(typeof(RepairApproval), 3)]
    [InlineData(typeof(DeviceWorkflowEventType), 10)]
    public void The_count_is_frozen_too(Type wireEnum, int expected) =>
        Assert.Equal(expected, Enum.GetValues(wireEnum).Length);
}
