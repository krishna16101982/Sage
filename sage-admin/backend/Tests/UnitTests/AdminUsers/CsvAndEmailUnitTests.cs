using System.Text;
using SageAdminApi.AppFunctions;
using Shouldly;

namespace SageAdminApi.Tests.UnitTests.AdminUsers;

/// <summary>The CSV writer used by the sales exports and the password-reset email.</summary>
public class CsvAndEmailUnitTests
{
    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+91 98765", "'+91 98765")]
    [InlineData("-5", "'-5")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("Acme Ltd", "Acme Ltd")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Text_PrefixesFormulaLikeValues(string? value, string expected)
        => CsvBuilder.Text(value!).ShouldBe(expected);

    [Fact]
    public void Rows_AreQuotedOnlyWhenNeeded_AndNumbersAreInvariant()
    {
        var csv = new CsvBuilder("Name", "Amount");
        csv.Row(CsvBuilder.Text("Plain"), CsvBuilder.Number(1234.5m));
        csv.Row(CsvBuilder.Text("A, \"B\"\nC"), CsvBuilder.Number(-10m));
        csv.ToString().ShouldBe("Name,Amount\r\nPlain,1234.50\r\n\"A, \"\"B\"\"\nC\",-10.00\r\n");
        csv.RowCount.ShouldBe(2);
        csv.ToBytes().Take(3).ShouldBe(Encoding.UTF8.GetPreamble());
    }

    [Fact]
    public void ResetEmail_HasTheLink_TheExpiry_AndEscapesTheName()
    {
        var now = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
        var mail = EmailPasswordResetNotifier.Build("asha@example.com", "<b>Asha</b>", "a+b/c=", now.AddMinutes(30), "http://localhost:4200/", now);

        mail.To.ShouldBe("asha@example.com");
        mail.Subject.ShouldBe("Reset your Sage Admin password");
        mail.TextBody.ShouldContain("http://localhost:4200/reset-password?token=a%2Bb%2Fc%3D");
        mail.TextBody.ShouldContain("expires in 30 minutes");
        mail.HtmlBody.ShouldContain("href=\"http://localhost:4200/reset-password?token=a%2Bb%2Fc%3D\"");
        mail.HtmlBody.ShouldContain("Hello &lt;b&gt;Asha&lt;/b&gt;,");
        mail.HtmlBody.ShouldNotContain("<b>Asha</b>");
    }

    [Theory]
    [InlineData("kumar@example.com", "k***@example.com")]
    [InlineData("@example.com", "***")]
    [InlineData("nobody", "***")]
    public void Logs_MaskTheRecipient(string email, string expected)
        => EmailBackgroundService.MaskEmail(email).ShouldBe(expected);
}
