using EciSupportCentre.Core.Presentation;

namespace EciSupportCentre.Core.Tests;

public class HtmlTextTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("plain text", "plain text")]
    [InlineData("<p>On 12 November 2026, the organisers&nbsp;‘Demand’  submitted it</p>\n", "On 12 November 2026, the organisers ‘Demand’ submitted it")]
    [InlineData("<p>First</p><p>Second</p>", "First\nSecond")]
    [InlineData("<p>First</p>\n\n<p>  Second </p>", "First\nSecond")]
    [InlineData("a<br>b<br/>c<BR />d", "a\nb\nc\nd")]
    [InlineData("<ul><li>one</li><li>two</li></ul>", "• one\n• two")]
    [InlineData("<strong>Bold</strong> and <a href=\"x\">link</a>", "Bold and link")]
    [InlineData("x <script>alert(1)</script><style>p{}</style><!-- note --> y", "x y")]
    [InlineData("Fish &amp; chips &lt;3 &#8364;5 &quot;ok&quot;", "Fish & chips <3 €5 \"ok\"")]
    [InlineData("<p>Στις 6 Οκτωβρίου, η Ευρωπαϊκή Επιτροπή καταχώρισε</p>", "Στις 6 Οκτωβρίου, η Ευρωπαϊκή Επιτροπή καταχώρισε")]
    public void Html_becomes_tidy_plain_text(string? html, string expected) =>
        Assert.Equal(expected, HtmlText.ToPlainText(html));
}
