using System.Data;
using SageCommonLib.AppFunctions;
using SageCommonLib.Models;
using Shouldly;

namespace SageCommonLib.Tests.AppFunctions;

/// <summary>Reading DataTable rows by column name: NULLs, UTC dates, DATE columns, mapping every row.</summary>
public class DataRowExtensionsTests
{
    private static DataTable Table()
    {
        var t = new DataTable();
        t.Columns.Add("Name", typeof(string));
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("Count", typeof(int));
        t.Columns.Add("Big", typeof(long));
        t.Columns.Add("Amount", typeof(decimal));
        t.Columns.Add("Flag", typeof(bool));
        t.Columns.Add("At", typeof(DateTime));
        t.Columns.Add("Day", typeof(DateTime));
        return t;
    }

    [Fact]
    public void Values_AreRead_ByName()
    {
        var t = Table();
        var id = Guid.NewGuid();
        t.Rows.Add("Asha", id, 7, 9_000_000_000L, 12.50m, true, new DateTime(2026, 11, 16, 10, 30, 0), new DateTime(2026, 11, 16));
        DataRow r = t.FirstRow()!;

        r.GetText("Name").ShouldBe("Asha");
        r.GetGuid("Id").ShouldBe(id);
        r.GetNullableGuid("Id").ShouldBe(id);
        r.GetInt("Count").ShouldBe(7);
        r.GetLong("Big").ShouldBe(9_000_000_000L);
        r.GetDecimal("Amount").ShouldBe(12.50m);
        r.GetBool("Flag").ShouldBeTrue();
        r.GetUtc("At").Kind.ShouldBe(DateTimeKind.Utc);
        r.GetUtc("At").Hour.ShouldBe(10);
        r.GetDate("Day").ShouldBe(new DateOnly(2026, 11, 16));
    }

    [Fact]
    public void Nulls_BecomeNull_OrZero_OrFalse()
    {
        var t = Table();
        t.Rows.Add(DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
        DataRow r = t.FirstRow()!;

        r.GetText("Name").ShouldBeNull();
        r.GetNullableGuid("Id").ShouldBeNull();
        r.GetInt("Count").ShouldBe(0);
        r.GetNullableInt("Count").ShouldBeNull();
        r.GetDecimal("Amount").ShouldBe(0m);
        r.GetNullableDecimal("Amount").ShouldBeNull();
        r.GetBool("Flag").ShouldBeFalse();
        r.GetNullableUtc("At").ShouldBeNull();
        r.GetNullableDate("Day").ShouldBeNull();
    }

    [Fact]
    public void FirstRow_IsNull_WhenEmpty_AndMapRows_KeepsOrder()
    {
        var t = Table();
        t.FirstRow().ShouldBeNull();
        t.MapRows(r => r.GetText("Name")).ShouldBeEmpty();

        t.Rows.Add("b", Guid.NewGuid(), 1, 1L, 1m, false, DateTime.UtcNow, DateTime.UtcNow);
        t.Rows.Add("a", Guid.NewGuid(), 2, 2L, 2m, false, DateTime.UtcNow, DateTime.UtcNow);
        t.MapRows(r => r.GetText("Name")).ShouldBe(new[] { "b", "a" });
    }

    [Fact]
    public void EnsureRead_Throws_OnlyForAFailedRead()
    {
        Should.NotThrow(() => Sql.EnsureRead(new FunctionReturn()));
        Should.Throw<InvalidOperationException>(() => Sql.EnsureRead(FunctionReturn.Fail("SYSTEM_ERROR", "A database error occurred.", "500", "test")));
    }
}
