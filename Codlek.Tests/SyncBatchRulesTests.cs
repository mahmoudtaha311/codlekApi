using System.Text.Json;
using Codlek.Api.Controllers;
using Codlek.Api.Racks;
using Codlek.Application.Contracts.Sync;
using Codlek.Core.Entities;
using Codlek.Core.Enums;
using Codlek.Core.Sync;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Codlek.Tests;

/// <summary>
/// قواعد دفعة المزامنة اللي مش محتاجة سيرفر — <b>الافتراضيات والنسخ
/// والترتيب</b>.
/// </summary>
public class SyncBatchRulesTests
{
    // =================================================================
    //  الافتراضي المقفول
    // =================================================================

    /// <summary>
    /// 🔴 <b>نتيجة الصف بتتولد «مرفوضة وقابلة للإعادة».</b> ده حزام
    /// الأمان: لو أي مسار نسي يحسم الصف، الراكة بتعيده — في القديم
    /// الافتراضي كان «اتطبّق» يعني الراكة تمسحه وهو ماوصلش.
    /// </summary>
    [Fact]
    public void A_fresh_row_result_is_a_retryable_rejection()
    {
        var result = new SyncItemResult();

        Assert.Equal(SyncItemStatus.Rejected, result.Status);
        Assert.NotNull(result.Error);
        Assert.Equal(SyncBatchCodes.NotProcessed, result.Error!.Code);
        Assert.True(result.Error.Retryable);
        Assert.False(string.IsNullOrWhiteSpace(result.Error.Message));
    }

    /// <summary>
    /// ⚠️ <b>والقيم دي نصوص على السلك</b> — الراكة بتقارنها بالحرف
    /// (من غير حساسية لحالة الأحرف).
    /// </summary>
    [Fact]
    public void The_statuses_are_the_frozen_wire_words()
    {
        Assert.Equal("Applied", SyncItemStatus.Applied);
        Assert.Equal("Unchanged", SyncItemStatus.Unchanged);
        Assert.Equal("Rejected", SyncItemStatus.Rejected);
    }

    // =================================================================
    //  نسخة البرنامج
    // =================================================================

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("1.0.0.0", true)]
    [InlineData(" 2.3.1.0 ", true)]
    [InlineData("10.0", true)]
    [InlineData("0.9.9", false)]
    [InlineData("0.9.9.9", false)]
    [InlineData("1", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Only_versions_from_the_minimum_up_are_supported(string? version, bool supported)
    {
        Assert.Equal(supported, ClientVersions.IsSupported(version));
    }

    // =================================================================
    //  الأنواع والترتيب
    // =================================================================

    /// <summary>
    /// 🔴 <b>الجهاز الأول، بعده الفحص، بعده الأمر، بعده الحركة — والمش
    /// معروف في الآخر.</b> ومن غير حساسية لحالة الأحرف: الراكات
    /// القديمة بتبعت <c>Device</c>.
    /// </summary>
    [Fact]
    public void The_apply_order_puts_devices_first_and_strangers_last()
    {
        string[] types = ["banana", "workflow", "Report", "workitem", "DEVICE", ""];

        var ordered = types.OrderBy(SyncEntityTypes.ApplyOrder).ToArray();

        Assert.Equal(["DEVICE", "Report", "workitem", "workflow", "banana", ""], ordered);
        Assert.Equal(99, SyncEntityTypes.ApplyOrder(null));
    }

    [Fact]
    public void Every_supported_type_is_known_and_the_lists_ignore_case()
    {
        Assert.Subset(
            SyncEntityTypes.Known.ToHashSet(StringComparer.OrdinalIgnoreCase),
            SyncEntityTypes.Supported.ToHashSet(StringComparer.OrdinalIgnoreCase));

        Assert.Contains("DEVICE", SyncEntityTypes.Known);
        Assert.Contains("WorkFlow", SyncEntityTypes.Supported);
        Assert.DoesNotContain("banana", SyncEntityTypes.Known);
    }

    // =================================================================
    //  الحارس اللي مابيتوصلش من الأنبوب
    // =================================================================

    /// <summary>
    /// ⚠️ <b>المحطة الملغية بتاخد <c>403</c> برسالة — لو وصلت.</b>
    /// التحقق بالمفتاح بيقبل الشغّالة بس، فالحارس ده مابيتوصلش النهارده
    /// من الأنبوب. الفحص بيناديه مباشرةً عشان لو التحقق اتغيّر يوم ما،
    /// الحارس يكون شغّال فعلاً مش مكتوب وبس.
    /// </summary>
    [Fact]
    public async Task A_revoked_rack_that_slips_through_gets_a_403_with_a_code()
    {
        var context = new DefaultHttpContext();

        context.Items[RackKeyFilter.ItemKey] = new Rack { Status = RackStatus.Revoked };

        var controller = new RackSyncController(
            sender: null!, racks: null!, Options.Create(new RackServerOptions()))
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

        var result = Assert.IsType<JsonResult>(await controller.Batch(CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);

        var body = JsonDocument.Parse(JsonSerializer.Serialize(result.Value)).RootElement;

        Assert.Equal("RackRevoked", body.GetProperty("code").GetString());
    }
}
