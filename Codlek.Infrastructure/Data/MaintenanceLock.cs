using System.Data;
using Codlek.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Codlek.Infrastructure.Data;

/// <summary>
/// <inheritdoc cref="IMaintenanceLock"/>
///
/// <para>🔴 <b><c>sp_getapplock</c> على اتصال لوحده.</b> القفل ملك
/// «الجلسة»، فلازم الاتصال يفضل مفتوح طول الصيانة. اتصال الـ<c>DbContext</c>
/// مايتضمنش: الإعادة التلقائية بعد خطأ عابر بتفتح اتصال جديد — والقفل
/// بيضيع في صمت والعملية التانية تدخل.</para>
///
/// <para>⚠️ <b>والسيب صريح قبل القفل.</b> الاتصال بيرجع للمجمّع لما
/// يتقفل والجلسة مابتتصفّرش غير أول ما حد يستعمله تاني — فمن غير
/// <c>sp_releaseapplock</c> القفل ممكن يفضل ماسك والإقلاع الجاي يفتكر
/// إن فيه عملية تانية شغّالة.</para>
/// </summary>
public sealed class MaintenanceLock(AppDbContext db) : IMaintenanceLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string name, CancellationToken ct = default)
    {
        string connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("مفيش نص اتصال للقاعدة.");

        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Resource", name);
            command.Parameters.AddWithValue("@LockMode", "Exclusive");
            command.Parameters.AddWithValue("@LockOwner", "Session");

            // ⚠️ من غير استنّى: اللي ماسكه بيعمل نفس الشغل دلوقتي.
            command.Parameters.AddWithValue("@LockTimeout", 0);

            var status = command.Parameters.Add("@Result", SqlDbType.Int);
            status.Direction = ParameterDirection.ReturnValue;

            await command.ExecuteNonQueryAsync(ct);

            // صفر أو واحد = اتاخد. السالب = ماسكه حد تاني أو خطأ.
            if ((int)status.Value! >= 0) return new Held(connection, name);

            await connection.DisposeAsync();
            return null;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Held(SqlConnection connection, string name) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "sp_releaseapplock";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@Resource", name);
                command.Parameters.AddWithValue("@LockOwner", "Session");
                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException)
            {
                // ⚠️ الاتصال وقع؟ القفل وقع معاه — مفيش حاجة تتساب.
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
