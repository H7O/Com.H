using Com.H.Net.Mail;

namespace Com.H.Tests;

public class MailMessageTests
{
    [Fact]
    public void ToStr_Setter_ParsesCommaSeparatedEmails()
    {
        using var msg = new Message();
        msg.ToStr = "a@test.com,b@test.com";

        Assert.Equal(2, msg.To.Count);
        Assert.Contains("a@test.com", msg.To);
        Assert.Contains("b@test.com", msg.To);
    }

    [Fact]
    public void ToStr_Setter_ParsesSemicolonSeparatedEmails()
    {
        using var msg = new Message();
        msg.ToStr = "a@test.com;b@test.com";

        Assert.Equal(2, msg.To.Count);
    }

    [Fact]
    public void ToStr_Setter_EmailsWithSpaces_TrimsCorrectly()
    {
        // Exercises StringSplitOptions.TrimEntries path
        using var msg = new Message();
        msg.ToStr = " a@test.com , b@test.com ";

        Assert.Equal(2, msg.To.Count);
        Assert.Contains("a@test.com", msg.To);
        Assert.Contains("b@test.com", msg.To);
    }

    [Fact]
    public void CcStr_Setter_ParsesEmails()
    {
        using var msg = new Message();
        msg.CcStr = "cc1@test.com,cc2@test.com";

        Assert.Equal(2, msg.Cc.Count);
        Assert.Contains("cc1@test.com", msg.Cc);
    }

    [Fact]
    public void CcStr_Setter_EmailsWithSpaces_TrimsCorrectly()
    {
        using var msg = new Message();
        msg.CcStr = " cc1@test.com ; cc2@test.com ";

        Assert.Equal(2, msg.Cc.Count);
        Assert.Contains("cc1@test.com", msg.Cc);
        Assert.Contains("cc2@test.com", msg.Cc);
    }

    [Fact]
    public void BccStr_Setter_ParsesEmails()
    {
        using var msg = new Message();
        msg.BccStr = "bcc1@test.com;bcc2@test.com";

        Assert.Equal(2, msg.Bcc.Count);
        Assert.Contains("bcc1@test.com", msg.Bcc);
    }

    [Fact]
    public void BccStr_Setter_EmailsWithSpaces_TrimsCorrectly()
    {
        using var msg = new Message();
        msg.BccStr = " bcc1@test.com , bcc2@test.com ";

        Assert.Equal(2, msg.Bcc.Count);
        Assert.Contains("bcc1@test.com", msg.Bcc);
        Assert.Contains("bcc2@test.com", msg.Bcc);
    }

    [Fact]
    public void ToStr_Getter_ReturnsCommaSeparated()
    {
        using var msg = new Message();
        msg.ToStr = "a@test.com,b@test.com";

        Assert.Equal("a@test.com,b@test.com", msg.ToStr);
    }

    [Fact]
    public void ToStr_SetNull_ClearsList()
    {
        using var msg = new Message();
        msg.ToStr = "a@test.com";
        msg.ToStr = null;

        Assert.Empty(msg.To);
    }
}
