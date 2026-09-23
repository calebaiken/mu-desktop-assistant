using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;
using MdBlock = Markdig.Syntax.Block;
using WpfBlock = System.Windows.Documents.Block;

namespace Companion.Desktop;

/// <summary>Untrusted Markdown is translated directly into a fixed, bounded WPF vocabulary.
/// There is no HTML renderer, XAML loader, URI navigation, external image or content file access.</summary>
internal static class SafeChatDocumentRenderer
{
    internal const int MaximumInputBytes = 48 * 1024;
    internal const int MaximumNodes = 2000;
    internal const int MaximumDepth = 16;
    internal const int MaximumMathCharacters = 512;
    private const int MaximumMathExpressions = 32;
    private static readonly FontFamily CodeFont = new("Consolas");
    private static readonly HashSet<string> MathCommands = new(StringComparer.Ordinal)
    {
        "frac", "sqrt", "sum", "prod", "int", "lim", "sin", "cos", "tan", "log", "ln", "exp", "min", "max",
        "alpha", "beta", "gamma", "delta", "epsilon", "theta", "lambda", "mu", "pi", "rho", "sigma", "tau", "phi", "psi", "omega",
        "Gamma", "Delta", "Theta", "Lambda", "Xi", "Pi", "Sigma", "Phi", "Psi", "Omega", "infty",
        "cdot", "times", "div", "pm", "mp", "le", "leq", "ge", "geq", "ne", "neq", "approx", "equiv",
        "to", "rightarrow", "leftarrow", "in", "notin", "subset", "subseteq", "cup", "cap", "forall", "exists",
        "partial", "nabla", "overline", "underline", "vec", "hat", "bar", "left", "right", "text"
    };
    private static readonly HashSet<string> CodeKeywords = new(StringComparer.Ordinal)
    {
        "as", "async", "await", "bool", "break", "case", "catch", "class", "const", "continue", "def", "default", "do", "else", "enum",
        "except", "false", "False", "finally", "float", "for", "foreach", "from", "function", "if", "import", "in", "int", "interface",
        "internal", "is", "let", "namespace", "new", "None", "null", "private", "protected", "public", "raise", "readonly", "return",
        "static", "string", "struct", "switch", "throw", "true", "True", "try", "using", "var", "void", "while", "with", "yield"
    };

    internal static FlowDocument Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var (text, truncated) = BoundInput(markdown);
        if (truncated) return Plain(text + "\n\n[Formatted view limited to 48 KiB. Use plain text for the complete transcript.]");
        try
        {
            // Markdig's parser has its own nesting guard, before our AST traversal budget.
            var pipeline = new MarkdownPipelineBuilder { MaximumNestingDepth = MaximumDepth, PreciseSourceLocation = true }
                .UseMathematics().Build();
            var ast = Markdown.Parse(text, pipeline);
            var remainingNodes = MaximumNodes;
            if (!WithinBudget(ast, ref remainingNodes)) return Plain(text);
            var document = CreateDocument();
            var context = new RenderContext(text, new RenderBudget());
            foreach (var block in ast) document.Blocks.Add(context.Block(block));
            return document;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Parser/renderer errors are never shown with raw exception details or interpreted as markup.
            return Plain(text);
        }
    }

    internal static FlowDocument RenderConversation(IReadOnlyList<(string Label, string Content)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        // Keep complete newest messages when the shared display budget is exhausted. Only a
        // single oversized latest body is clipped; the plain-text view retains the full transcript.
        var selected = new List<(string Label, string Content)>();
        var remainingBytes = MaximumInputBytes;
        var truncated = entries.Count > 16;
        for (var index = entries.Count - 1; index >= System.Math.Max(0, entries.Count - 16); index--)
        {
            var entry = entries[index];
            var label = BoundInput(entry.Label ?? string.Empty, System.Math.Min(256, remainingBytes));
            var labelBytes = Encoding.UTF8.GetByteCount(label.Text);
            var body = BoundInput(entry.Content ?? string.Empty, remainingBytes - labelBytes);
            if (body.Truncated && selected.Count != 0) { truncated = true; break; }
            selected.Add((label.Text, body.Text));
            remainingBytes -= labelBytes + Encoding.UTF8.GetByteCount(body.Text);
            truncated |= label.Truncated || body.Truncated;
            if (body.Truncated) break;
        }
        selected.Reverse();
        try
        {
            var pipeline = new MarkdownPipelineBuilder { MaximumNestingDepth = MaximumDepth, PreciseSourceLocation = true }
                .UseMathematics().Build();
            var parsed = new List<MarkdownDocument>();
            var remainingNodes = MaximumNodes - selected.Count * 2; // include trusted section/header nodes
            foreach (var entry in selected)
            {
                var ast = Markdown.Parse(entry.Content, pipeline);
                if (!WithinBudget(ast, ref remainingNodes)) return PlainConversation(selected, truncated);
                parsed.Add(ast);
            }
            var document = ConversationDocument(truncated);
            var budget = new RenderBudget();
            for (var index = 0; index < selected.Count; index++)
            {
                var section = MessageSection(selected[index].Label);
                var context = new RenderContext(selected[index].Content, budget);
                foreach (var block in parsed[index]) section.Blocks.Add(context.Block(block));
                document.Blocks.Add(section);
            }
            return document;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return PlainConversation(selected, truncated);
        }
    }

    private static FlowDocument ConversationDocument(bool truncated)
    {
        var document = CreateDocument();
        if (truncated)
            document.Blocks.Add(new Paragraph(new Run("Formatted view is limited to 48 KiB and the newest 16 messages. Some text is omitted; use plain text for the complete transcript."))
            { FontSize = 12, Foreground = SystemColors.GrayTextBrush, Margin = new(0, 0, 0, 12) });
        return document;
    }

    private static Section MessageSection(string label)
    {
        var section = new Section { Margin = new(0, 0, 0, 12), BorderBrush = SystemColors.ActiveBorderBrush, BorderThickness = new(0, 0, 0, 1), Padding = new(0, 0, 0, 8) };
        var heading = new Paragraph(new Run(label)) { FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 7) };
        AutomationProperties.SetAutomationId(heading, "ConversationRole");
        section.Blocks.Add(heading);
        return section;
    }

    private static FlowDocument PlainConversation(IReadOnlyList<(string Label, string Content)> entries, bool truncated)
    {
        var document = ConversationDocument(truncated);
        foreach (var entry in entries)
        {
            var section = MessageSection(entry.Label);
            section.Blocks.Add(LiteralBlock(entry.Content));
            document.Blocks.Add(section);
        }
        return document;
    }

    private static FlowDocument CreateDocument() => new()
    {
        FontFamily = new("Segoe UI"), FontSize = 14, Foreground = SystemColors.WindowTextBrush,
        Background = SystemColors.WindowBrush, PagePadding = new(12), ColumnWidth = 10000,
        IsHyphenationEnabled = false
    };

    private static FlowDocument Plain(string text)
    {
        var document = CreateDocument();
        document.Blocks.Add(new Paragraph(new Run(text)) { Margin = new(0) });
        return document;
    }

    private static (string Text, bool Truncated) BoundInput(string input, int maximumBytes = MaximumInputBytes)
    {
        // Do not allocate or scan an arbitrarily large caller string. Rune boundaries preserve emoji.
        var bytes = 0;
        var characters = 0;
        foreach (var rune in input.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > maximumBytes) break;
            bytes += rune.Utf8SequenceLength;
            characters += rune.Utf16SequenceLength;
        }
        return (input[..characters], characters != input.Length);
    }

    private static bool WithinBudget(MarkdownDocument document, ref int remainingNodes)
    {
        var pending = new Stack<(MarkdownObject Node, int Depth)>();
        pending.Push((document, 0));
        while (pending.TryPop(out var item))
        {
            if (--remainingNodes < 0 || item.Depth > MaximumDepth) return false;
            if (item.Node is ContainerBlock blocks)
                foreach (var block in blocks) pending.Push((block, item.Depth + 1));
            if (item.Node is LeafBlock { Inline: { } inline }) pending.Push((inline, item.Depth + 1));
            if (item.Node is ContainerInline inlines)
                for (var child = inlines.FirstChild; child is not null; child = child.NextSibling)
                    pending.Push((child, item.Depth + 1));
        }
        return true;
    }

    private sealed class RenderBudget
    {
        internal int MathCount;
    }

    private sealed class RenderContext(string source, RenderBudget budget)
    {

        internal WpfBlock Block(MdBlock node)
        {
            switch (node)
            {
                case MathBlock math:
                    var latex = math.Lines.ToString();
                    if (Math(latex, true) is { } shape)
                        return new BlockUIContainer(shape) { Margin = new(0, 6, 0, 10) };
                    return LiteralBlock(Raw(node));
                case FencedCodeBlock fenced:
                    return CodeBlock(fenced.Lines.ToString(), fenced.Info);
                case Markdig.Syntax.CodeBlock code:
                    return CodeBlock(code.Lines.ToString(), null);
                case HeadingBlock heading:
                    var title = Paragraph(heading.Inline);
                    title.FontWeight = FontWeights.SemiBold;
                    title.FontSize = heading.Level switch { 1 => 23, 2 => 20, 3 => 17, _ => 15 };
                    title.Margin = new(0, 10, 0, 6);
                    return title;
                case ParagraphBlock paragraph:
                    return Paragraph(paragraph.Inline);
                case QuoteBlock quote:
                    var section = new Section { BorderBrush = SystemColors.ActiveBorderBrush, BorderThickness = new(3, 0, 0, 0), Padding = new(12, 3, 0, 3), Margin = new(0, 6, 0, 10) };
                    foreach (var child in quote) section.Blocks.Add(Block(child));
                    return section;
                case ListBlock list:
                    var rendered = new System.Windows.Documents.List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new(0, 0, 0, 10), Padding = new(22, 0, 0, 0) };
                    if (list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var start))
                        rendered.StartIndex = System.Math.Clamp(start, 1, 1000000);
                    foreach (var child in list)
                    {
                        var item = new ListItem();
                        if (child is ListItemBlock entries)
                            foreach (var entry in entries) item.Blocks.Add(Block(entry));
                        else item.Blocks.Add(LiteralBlock(Raw(child)));
                        rendered.ListItems.Add(item);
                    }
                    return rendered;
                case ThematicBreakBlock:
                    return new Paragraph { BorderBrush = SystemColors.ActiveBorderBrush, BorderThickness = new(0, 1, 0, 0), Margin = new(0, 10, 0, 10) };
                default:
                    return LiteralBlock(Raw(node));
            }
        }

        private Paragraph Paragraph(ContainerInline? content)
        {
            var paragraph = new Paragraph { Margin = new(0, 0, 0, 10) };
            if (content is not null) AddInlines(content, paragraph.Inlines);
            return paragraph;
        }

        private void AddInlines(ContainerInline content, InlineCollection target)
        {
            for (var child = content.FirstChild; child is not null; child = child.NextSibling)
            {
                switch (child)
                {
                    case LiteralInline literal: target.Add(new Run(literal.Content.ToString())); break;
                    case HtmlEntityInline entity: target.Add(new Run(entity.Transcoded.ToString())); break;
                    case CodeInline code:
                        target.Add(new Run(code.Content) { FontFamily = CodeFont, Background = SystemColors.ControlBrush });
                        break;
                    case LineBreakInline line:
                        target.Add(line.IsHard ? new LineBreak() : new Run(" "));
                        break;
                    case MathInline math:
                        target.Add(Math(math.Content.ToString(), false) is { } shape
                            ? new InlineUIContainer(shape) { BaselineAlignment = BaselineAlignment.Center }
                            : new Run(Raw(math)));
                        break;
                    case EmphasisInline emphasis:
                        var span = new Span();
                        if (emphasis.DelimiterCount == 2) span.FontWeight = FontWeights.Bold;
                        else span.FontStyle = FontStyles.Italic;
                        AddInlines(emphasis, span.Inlines);
                        target.Add(span);
                        break;
                    // Links, autolinks, remote images and HTML are visibly literal, never interactive.
                    default: target.Add(new Run(Raw(child))); break;
                }
            }
        }

        private string Raw(MarkdownObject node)
        {
            var start = System.Math.Clamp(node.Span.Start, 0, source.Length);
            var end = System.Math.Clamp(node.Span.End + 1, start, source.Length);
            return source[start..end];
        }

        private FrameworkElement? Math(string latex, bool display)
        {
            if (++budget.MathCount > MaximumMathExpressions || !IsSafeMath(latex)) return null;
            try
            {
                // WpfMath 2.1.0's static geometry API loads only bundled math fonts and the fixed
                // local Arial text font. Geometry avoids FormulaControl event/error state and bitmaps.
                var formula = WpfTeXFormulaParser.Instance.Parse(latex);
                var scale = display ? 20d : 17d;
                var environment = WpfTeXEnvironment.Create(TexStyle.Display, scale, "Arial");
                var geometry = formula.RenderToGeometry(environment, scale);
                var bounds = geometry.Bounds;
                if (bounds.IsEmpty || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
                    bounds.Width is <= 0 or > 1000 || bounds.Height is <= 0 or > 256) return null;
                geometry.Transform = new TranslateTransform(-bounds.X, -bounds.Y);
                geometry.Freeze();
                var path = new System.Windows.Shapes.Path
                {
                    Data = geometry, Fill = SystemColors.WindowTextBrush, Width = bounds.Width, Height = bounds.Height,
                    Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false,
                    Focusable = false, ToolTip = latex, Tag = "LocalMathGeometry"
                };
                AutomationProperties.SetName(path, "Math: " + latex);
                return path;
            }
            catch (Exception error) when (error is not OutOfMemoryException) { return null; }
        }
    }

    internal static bool IsSafeMath(string latex)
    {
        if (latex.Length is 0 or > MaximumMathCharacters) return false;
        var depth = 0;
        var commands = 0;
        for (var index = 0; index < latex.Length; index++)
        {
            var value = latex[index];
            if (value == '\\')
            {
                if (++commands > 64 || ++index == latex.Length) return false;
                var start = index;
                while (index < latex.Length && char.IsAsciiLetter(latex[index])) index++;
                if (index == start)
                {
                    if ("{}_%#,$;:! ".IndexOf(latex[index]) < 0) return false;
                }
                else
                {
                    if (!MathCommands.Contains(latex[start..index])) return false;
                    index--;
                }
                continue;
            }
            if (value is '{' or '[') { if (++depth > MaximumDepth) return false; }
            else if (value is '}' or ']') { if (--depth < 0) return false; }
            else if (char.IsControl(value) && !char.IsWhiteSpace(value)) return false;
            else if (value is '$' or '`') return false;
        }
        return depth == 0;
    }

    private static Paragraph LiteralBlock(string text) => new(new Run(text)) { Margin = new(0, 0, 0, 10) };

    private static WpfBlock CodeBlock(string code, string? language)
    {
        var text = new TextBlock { FontFamily = CodeFont, FontSize = 13, TextWrapping = TextWrapping.NoWrap, Foreground = SystemColors.WindowTextBrush };
        var normalized = language?.Trim().ToLowerInvariant();
        if (code.Length > 8192 || normalized is not ("cs" or "csharp" or "c#" or "js" or "javascript" or "ts" or "typescript" or "py" or "python" or "json"))
            text.Text = code;
        else
            HighlightCode(code, normalized, text);
        AutomationProperties.SetName(text, string.IsNullOrEmpty(normalized) ? "Code" : "Code: " + normalized);
        return new BlockUIContainer(new Border
        {
            Background = SystemColors.ControlBrush, Padding = new(10), CornerRadius = new(5),
            Child = new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = true }
        }) { Margin = new(0, 4, 0, 12) };
    }

    private static void HighlightCode(string code, string language, TextBlock target)
    {
        var runs = new List<Run>();
        var index = 0;
        while (index < code.Length && runs.Count <= 256)
        {
            var start = index++;
            var value = code[start];
            var color = SystemColors.WindowTextBrush;
            if ((language is "py" or "python") && value == '#' || value == '/' && index < code.Length && code[index] == '/')
            {
                while (index < code.Length && code[index] != '\n') index++;
                color = Brushes.DarkGreen;
            }
            else if (value is '\'' or '"')
            {
                while (index < code.Length)
                {
                    var character = code[index++];
                    if (character == '\\' && index < code.Length) index++;
                    else if (character == value || character == '\n') break;
                }
                color = Brushes.Maroon;
            }
            else if (char.IsAsciiDigit(value))
            {
                while (index < code.Length && (char.IsAsciiLetterOrDigit(code[index]) || code[index] == '.')) index++;
                color = Brushes.DarkCyan;
            }
            else if (char.IsAsciiLetter(value) || value == '_')
            {
                while (index < code.Length && (char.IsAsciiLetterOrDigit(code[index]) || code[index] == '_')) index++;
                if (CodeKeywords.Contains(code[start..index])) color = Brushes.DarkBlue;
            }
            else
            {
                while (index < code.Length && char.IsWhiteSpace(code[index])) index++;
            }
            runs.Add(new Run(code[start..index]) { Foreground = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : color });
        }
        if (index < code.Length || runs.Count > 256) target.Text = code;
        else foreach (var run in runs) target.Inlines.Add(run);
    }
}
