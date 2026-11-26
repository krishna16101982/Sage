using SageUserApi.AppFunctions;
using Shouldly;

namespace SageUserApi.Tests.UnitTests;

/// <summary>The order form wording's formatting (bold, italic, underline, colour, highlight) as the API saves and prints it.</summary>
public class RichTextTests
{
    [Fact]
    public void Clean_KeepsTheEditorsFormatting_InOneWay()
    {
        // Same formatting written differently (upper-case tags, colour case, split runs) comes out the same.
        RichText.Clean("<B>Bold</B><b> too</b> <span style=\"color: #C62828;\">red</span>")
            .ShouldBe("<b>Bold too</b> <span style=\"color:#c62828\">red</span>");
        RichText.Clean("<span style=\"color:#c62828\"><span style=\"background-color:#fff176\"><b><i><u>all</u></i></b></span></span>")
            .ShouldBe("<span style=\"color:#c62828\"><span style=\"background-color:#fff176\"><b><i><u>all</u></i></b></span></span>");
    }

    [Fact]
    public void Clean_TurnsOtherTagsIntoText_AndKeepsOldPlainWording()
    {
        RichText.Clean("<img src=x onerror=alert(1)> & <a href=\"x\">link</a>")
            .ShouldBe("&lt;img src=x onerror=alert(1)&gt; &amp; &lt;a href=&quot;x&quot;&gt;link&lt;/a&gt;");
        RichText.Plain(RichText.Clean("Terms & Conditions")!).ShouldBe("Terms & Conditions");
        RichText.Clean("   \n  ").ShouldBeNull();
    }

    [Fact]
    public void Lines_SplitOnNewLines_TrimmingEachLine()
    {
        var lines = RichText.Lines("  <b>Order Cancellation:</b> none  \n\n Second ");
        lines.Count.ShouldBe(2);
        lines[0][0].ShouldBe(new RichText.Run("Order Cancellation:", true, false, false, null, null));
        lines[0][1].Text.ShouldBe(" none");
        lines[1][0].Text.ShouldBe("Second");
    }
}
