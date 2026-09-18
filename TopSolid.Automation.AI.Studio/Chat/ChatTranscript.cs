using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TopSolid.Automation.AI.Studio.Localization;

namespace TopSolid.Automation.AI.Studio.Chat;

public sealed class ChatTranscript : RichTextBox
{
    private int characters;
    public ChatTranscript() => SizeChanged += (_, _) => UpdateMessageWidths();
    public string Text => new TextRange(Document.ContentStart, Document.ContentEnd).Text;
    public void Clear() { Document.Blocks.Clear(); characters = 0; }
    public static string Elapsed(double milliseconds) => (milliseconds / 1000).ToString("F1", CultureInfo.InvariantCulture) + " s";
    public void AppendMessage(string role, string text, double? elapsedMilliseconds = null)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 18), Padding = new Thickness(12, 9, 12, 9), LineHeight = 23 };
        paragraph.Tag = role;
        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
        if (role is "You" or "User") paragraph.SetResourceReference(TextElement.BackgroundProperty, "UserMessageBrush");
        var roleText = new Run(role);
        var roleKey = role switch { "You" => "Role.You", "User" => "Role.User", "Assistant" => "Role.Assistant", "System" => "Role.System", "TopSolid result" => "Role.TopSolidResult", _ => null };
        if (roleKey != null) { roleText.Text = StudioStrings.Get(roleKey); roleText.SetResourceReference(Run.TextProperty, "Ui." + roleKey); }
        var heading = new Bold(roleText) { FontSize = 12 };
        heading.SetResourceReference(TextElement.ForegroundProperty, role == "Assistant" ? "AccentBrush" : "MutedTextBrush");
        paragraph.Inlines.Add(heading);
        if (elapsedMilliseconds.HasValue) paragraph.Inlines.Add(new Run(" · " + Elapsed(elapsedMilliseconds.Value)) { FontSize = 12 });
        paragraph.Inlines.Add(new LineBreak()); paragraph.Inlines.Add(new Run(text));
        Document.Blocks.Add(paragraph); characters += text.Length + role.Length + 32;
        UpdateMessageWidth(paragraph);
        if (characters > 200000) {
            while (characters > 150000 && Document.Blocks.FirstBlock is { } oldest && oldest != paragraph) {
                characters -= new TextRange(oldest.ContentStart, oldest.ContentEnd).Text.Length + 32;
                Document.Blocks.Remove(oldest);
            }
        }
        ScrollToEnd();
    }

    private void UpdateMessageWidths()
    {
        foreach (var paragraph in Document.Blocks.OfType<Paragraph>()) UpdateMessageWidth(paragraph);
    }

    private void UpdateMessageWidth(Paragraph paragraph)
        => paragraph.Margin = new Thickness(paragraph.Tag is "You" or "User" ? Math.Max(0, ActualWidth * .16) : 0, 0, 0, 22);
}
