using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace PushoverDesktopClient;

// ==========================================
// HELPER: RICH TEXT LINK PARSER
// ==========================================
public static class RichTextHelper
{
    private static readonly Regex UrlRegex = new(
        @"(https?://[^\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public static void ParseTextWithLinks(RichTextBlock richTextBlock, string text)
    {
        richTextBlock.Blocks.Clear();
        var paragraph = new Paragraph();

        if (string.IsNullOrEmpty(text))
        {
            richTextBlock.Blocks.Add(paragraph);
            richTextBlock.UpdateLayout();
            return;
        }

        var tokens = UrlRegex.Split(text);

        foreach (var token in tokens)
        {
            if (UrlRegex.IsMatch(token))
            {
                try
                {
                    var hyperlink = new Hyperlink
                    {
                        NavigateUri = new Uri(token),
                        UnderlineStyle = UnderlineStyle.Single
                    };
                    hyperlink.Inlines.Add(new Run { Text = token });
                    paragraph.Inlines.Add(hyperlink);
                }
                catch
                {
                    paragraph.Inlines.Add(new Run { Text = token });
                }
            }
            else
            {
                paragraph.Inlines.Add(new Run { Text = token });
            }
        }

        richTextBlock.Blocks.Add(paragraph);
        richTextBlock.UpdateLayout();
    }
}
