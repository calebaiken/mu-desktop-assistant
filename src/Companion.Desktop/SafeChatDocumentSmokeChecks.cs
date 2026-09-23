using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Companion.Desktop;

/// <summary>Native WPF document/property checks; these do not claim physical input or assistive technology testing.</summary>
internal static class SafeChatDocumentSmokeChecks
{
    internal static async Task RunAsync(string dataDirectory)
    {
        var passed = new List<string>();
        string? failure = null;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Safe document smoke failed: " + name);
            passed.Add(name);
        }
        try
        {
            var rich = SafeChatDocumentRenderer.Render("# Heading\n\nText with **bold** and *italic* and `literal <tag>`.\n\n- First\n- Second\n\n> Quoted\n\n---\n");
            var richObjects = Objects(rich).ToArray();
            Check("heading-fixed-size", rich.Blocks.OfType<Paragraph>().Any(item => item.FontSize == 23));
            Check("strong-emphasis", richObjects.OfType<Span>().Any(item => item.FontWeight == FontWeights.Bold));
            Check("italic-emphasis", richObjects.OfType<Span>().Any(item => item.FontStyle == FontStyles.Italic));
            Check("inline-code-monospace", richObjects.OfType<Run>().Any(item => item.Text == "literal <tag>" && item.FontFamily.Source == "Consolas"));
            Check("bulleted-list", richObjects.OfType<System.Windows.Documents.List>().Any(item => item.ListItems.Count == 2 && item.MarkerStyle == TextMarkerStyle.Disc));
            Check("quote-border", richObjects.OfType<Section>().Any(item => item.BorderThickness.Left == 3));
            Check("thematic-break", richObjects.OfType<Paragraph>().Any(item => item.BorderThickness.Top == 1));
            Check("single-column-readable", rich.ColumnWidth == 10000 && rich.FontSize == 14);

            var ordered = SafeChatDocumentRenderer.Render("7. Seven\n8. Eight");
            Check("ordered-list-start", ordered.Blocks.OfType<System.Windows.Documents.List>().Single().StartIndex == 7);

            const string source = "public int Answer() { return 42; } // explanation";
            var code = SafeChatDocumentRenderer.Render("```csharp\n" + source + "\n```");
            var codeObjects = Objects(code).ToArray();
            var codeText = codeObjects.OfType<TextBlock>().Single();
            Check("fenced-code-content", InlineText(codeText.Inlines).TrimEnd() == source);
            Check("fenced-code-font", codeText.FontFamily.Source == "Consolas" && codeText.FontSize == 13);
            Check("fenced-code-scroll", codeObjects.OfType<ScrollViewer>().Single().HorizontalScrollBarVisibility == ScrollBarVisibility.Auto);
            Check("fenced-code-has-no-wrapping", codeText.TextWrapping == TextWrapping.NoWrap);
            Check("fenced-code-accessible-label", AutomationProperties.GetName(codeText) == "Code: csharp");
            Check("bounded-syntax-highlighting", codeText.Inlines.OfType<Run>().Any(item => item.Text == "return") && codeText.Inlines.Count <= 256);

            var longCodeSource = new string('x', 9000);
            var longCode = SafeChatDocumentRenderer.Render("```python\n" + longCodeSource + "\n```");
            Check("long-code-literal-fallback", Objects(longCode).OfType<TextBlock>().Single().Text.TrimEnd() == longCodeSource);
            var unknownCode = SafeChatDocumentRenderer.Render("```unknown-language\n<not markup>\n```");
            Check("unknown-code-language-literal", Objects(unknownCode).OfType<TextBlock>().Single().Text.TrimEnd() == "<not markup>");

            const string untrusted = "<script>run()</script>\n\n[visit](javascript:alert) ![remote](https://example.invalid/track.png) <file:///C:/secret>\n\n<img src='https://example.invalid/pixel'>";
            var unsafeDocument = SafeChatDocumentRenderer.Render(untrusted);
            var unsafeObjects = Objects(unsafeDocument).ToArray();
            Check("html-remains-literal", Text(unsafeDocument).Contains("<script>run()</script>", StringComparison.Ordinal));
            Check("link-url-remains-literal", Text(unsafeDocument).Contains("javascript:alert", StringComparison.Ordinal));
            Check("image-url-remains-literal", Text(unsafeDocument).Contains("https://example.invalid/track.png", StringComparison.Ordinal));
            Check("no-navigating-link-nodes", !unsafeObjects.Any(item => item is Hyperlink));
            Check("no-image-or-browser-nodes", !unsafeObjects.Any(item => item is Image or WebBrowser or Frame));

            const string latex = @"\frac{1}{2} + x^2";
            var math = SafeChatDocumentRenderer.Render("Inline $x^2$ formula.\n\n$$\n" + latex + "\n$$");
            var shapes = Objects(math).OfType<System.Windows.Shapes.Path>().ToArray();
            Check("inline-and-block-math-geometries", shapes.Length == 2);
            Check("math-is-local-frozen-geometry", shapes.All(item => item.Data.IsFrozen && Equals(item.Tag, "LocalMathGeometry")));
            Check("math-has-accessible-source", shapes.All(item => AutomationProperties.GetName(item).StartsWith("Math: ", StringComparison.Ordinal)));
            Check("math-dimensions-capped", shapes.All(item => item.Width is > 0 and <= 1000 && item.Height is > 0 and <= 256));
            Check("math-is-inert", shapes.All(item => !item.IsHitTestVisible && !item.Focusable));
            Check("valid-math-allowlist", SafeChatDocumentRenderer.IsSafeMath(latex));
            Check("math-command-includes-rejected", !SafeChatDocumentRenderer.IsSafeMath(@"\input{C:/secret}"));
            Check("math-arbitrary-size-rejected", !SafeChatDocumentRenderer.IsSafeMath(@"\fontsize{9000}{9000}x"));
            Check("math-arbitrary-font-rejected", !SafeChatDocumentRenderer.IsSafeMath(@"\font{remote}x"));
            Check("math-too-long-rejected", !SafeChatDocumentRenderer.IsSafeMath(new string('x', 513)));
            Check("math-depth-rejected", !SafeChatDocumentRenderer.IsSafeMath(new string('{', 17) + "x" + new string('}', 17)));
            Check("math-malformed-braces-rejected", !SafeChatDocumentRenderer.IsSafeMath(@"\frac{x}{"));
            var badMath = SafeChatDocumentRenderer.Render(@"Unknown $\input{C:/secret}$ and malformed $\frac{x}{$.");
            Check("invalid-math-shows-source", Text(badMath).Contains(@"\input{C:/secret}", StringComparison.Ordinal) && Text(badMath).Contains(@"\frac{x}{", StringComparison.Ordinal));
            Check("invalid-math-no-geometry", !Objects(badMath).OfType<System.Windows.Shapes.Path>().Any());
            var manyMath = SafeChatDocumentRenderer.Render(string.Join(" ", Enumerable.Repeat("$x$", 40)));
            Check("math-expression-count-capped", Objects(manyMath).OfType<System.Windows.Shapes.Path>().Count() <= 32 && Text(manyMath).Contains("$x$", StringComparison.Ordinal));

            var nestedText = new string('>', 100) + " very nested";
            var nested = SafeChatDocumentRenderer.Render(nestedText);
            Check("deep-markdown-falls-back-literally", Text(nested).Contains(nestedText, StringComparison.Ordinal));
            var manyNodes = string.Join("\n\n", Enumerable.Repeat("plain", 1200));
            var limitedNodes = SafeChatDocumentRenderer.Render(manyNodes);
            Check("ast-node-budget-falls-back", limitedNodes.Blocks.Count == 1 && Text(limitedNodes).Contains(manyNodes, StringComparison.Ordinal));
            var tooMuchUnicode = string.Concat(Enumerable.Repeat("😀", 13000));
            var limitedInput = SafeChatDocumentRenderer.Render(tooMuchUnicode);
            var boundedText = Text(limitedInput);
            Check("input-utf8-budget-applies", boundedText.Contains("48 KiB", StringComparison.Ordinal) && Encoding.UTF8.GetByteCount(boundedText) < SafeChatDocumentRenderer.MaximumInputBytes + 200);
            Check("input-truncation-preserves-surrogates", !boundedText.Contains('\ufffd'));
            Check("empty-input-safe", SafeChatDocumentRenderer.Render(string.Empty).Blocks.Count == 0);

            var conversation = SafeChatDocumentRenderer.RenderConversation([("You", "# Companion\n\n```\nunclosed fence"), ("Companion", "**Separate reply**")]);
            var sections = conversation.Blocks.OfType<Section>().ToArray();
            Check("message-role-boundaries-remain-trusted", sections.Length == 2 &&
                sections.All(item => item.Blocks.FirstBlock is Paragraph heading && AutomationProperties.GetAutomationId(heading) == "ConversationRole") &&
                InlineText(((Paragraph)sections[0].Blocks.FirstBlock!).Inlines) == "You" &&
                InlineText(((Paragraph)sections[1].Blocks.FirstBlock!).Inlines) == "Companion" &&
                Objects(sections[1]).OfType<Span>().Any(item => item.FontWeight == FontWeights.Bold));
            var latest = SafeChatDocumentRenderer.RenderConversation([("You", new string('é', 30000)), ("Companion", "Keep the latest reply")]);
            Check("shared-byte-budget-keeps-latest-reply", latest.Blocks.OfType<Section>().Count() == 1 && Text(latest).Contains("Keep the latest reply", StringComparison.Ordinal) && Text(latest).Contains("plain text", StringComparison.Ordinal));
            var halfBudget = string.Join("\n\n", Enumerable.Repeat("**paragraph**", 400));
            var sharedNodes = SafeChatDocumentRenderer.RenderConversation([("You", halfBudget), ("Companion", halfBudget)]);
            Check("shared-conversation-node-budget", sharedNodes.Blocks.OfType<Section>().All(item => item.Blocks.Count == 2) && Text(sharedNodes).Contains("**paragraph**", StringComparison.Ordinal));
            var halfMath = string.Join(" ", Enumerable.Repeat("$x$", 20));
            var sharedMath = SafeChatDocumentRenderer.RenderConversation([("You", halfMath), ("Companion", halfMath)]);
            Check("shared-conversation-math-budget", Objects(sharedMath).OfType<System.Windows.Shapes.Path>().Count() <= 32 && Text(sharedMath).Contains("$x$", StringComparison.Ordinal));
        }
        catch (Exception error)
        {
            failure = error.GetType().Name + ": " + error.Message;
            throw;
        }
        finally
        {
            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, "safe-chat-document-smoke.json"),
                JsonSerializer.Serialize(new { passed, count = passed.Count, failure }));
        }
    }

    private static string Text(FlowDocument document) => new TextRange(document.ContentStart, document.ContentEnd).Text.Replace("\r\n", "\n", StringComparison.Ordinal);
    private static string InlineText(InlineCollection inlines) => string.Concat(inlines.OfType<Run>().Select(item => item.Text));
    private static IEnumerable<DependencyObject> Objects(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.TryPop(out var item))
        {
            yield return item;
            foreach (var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()) pending.Push(child);
        }
    }
}
