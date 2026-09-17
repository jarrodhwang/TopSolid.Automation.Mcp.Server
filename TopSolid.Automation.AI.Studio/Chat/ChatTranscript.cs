using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TopSolid.Automation.AI.Studio.Chat;

public sealed class ChatTranscript : RichTextBox
{
    public static readonly Brush AssistantBrush = Brushes.RoyalBlue;
    private int characters;
    public string Text => new TextRange(Document.ContentStart, Document.ContentEnd).Text;
    public void Clear() { Document.Blocks.Clear(); characters = 0; }
    public static string Elapsed(double milliseconds) => (milliseconds / 1000).ToString("F1", CultureInfo.InvariantCulture) + " s";
    public void AppendMessage(string role, string text, double? elapsedMilliseconds = null)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 12), Foreground = role == "Assistant" ? AssistantBrush : SystemColors.WindowTextBrush };
        paragraph.Inlines.Add(new Bold(new Run(role)));
        if (elapsedMilliseconds.HasValue) paragraph.Inlines.Add(new Run(" · " + Elapsed(elapsedMilliseconds.Value)) { FontSize = 12 });
        paragraph.Inlines.Add(new LineBreak()); paragraph.Inlines.Add(new Run(text));
        Document.Blocks.Add(paragraph); characters += text.Length + role.Length + 32;
        if (characters > 200000) {
            while (characters > 150000 && Document.Blocks.FirstBlock is { } oldest && oldest != paragraph) {
                characters -= new TextRange(oldest.ContentStart, oldest.ContentEnd).Text.Length + 32;
                Document.Blocks.Remove(oldest);
            }
        }
        ScrollToEnd();
    }
}
