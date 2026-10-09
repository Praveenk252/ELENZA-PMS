using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Dependency-free HTML to PDF renderer used for report mail attachments.
/// Supports the report mail HTML subset: headings, paragraphs, nested divs,
/// tables with styled header/body rows, and inline bold/colored/background spans.
/// </summary>
public static class ReportPdfRenderer
{
    public static byte[] Render(string html, string title)
    {
        var r = new Renderer();
        return r.Execute(html, title);
    }

    private sealed class Renderer
    {
        private const float PageW = 595.28f;
        private const float PageH = 841.89f;
        private const float MarginL = 36f;
        private const float MarginR = 36f;
        private const float MarginT = 40f;
        private const float MarginB = 44f;

        // ------------------------------------------------------------ model

        private sealed class Node
        {
            public string Tag;
            public string Text;
            public Dictionary<string, string> Attrs;
            public List<Node> Children = new List<Node>();
            public Node Parent;
            public bool IsText { get { return Tag == "#text"; } }
            public string Class
            {
                get { return Attrs != null && Attrs.ContainsKey("class") ? Attrs["class"] : ""; }
            }
            public string GetAttr(string name)
            {
                return Attrs != null && Attrs.ContainsKey(name) ? Attrs[name] : null;
            }
        }

        private sealed class StyleRule
        {
            public string Selector;
            public Dictionary<string, string> Props;
        }

        private sealed class Style
        {
            public float Size = 10f;
            public bool Bold;
            public float[] Color = new float[] { 0.06f, 0.09f, 0.16f };
            public float[] Bg;
            public bool Upper;
            public float MarginTop = float.NaN;
            public float MarginBottom = float.NaN;
            public float LineHeight = 1.35f;

            public Style Clone()
            {
                var s = new Style();
                s.Size = Size;
                s.Bold = Bold;
                s.Color = (float[])Color.Clone();
                s.Bg = Bg == null ? null : (float[])Bg.Clone();
                s.Upper = Upper;
                s.MarginTop = MarginTop;
                s.MarginBottom = MarginBottom;
                s.LineHeight = LineHeight;
                return s;
            }
        }

        private sealed class Run
        {
            public string Text;
            public Style Style;
            public float Width;
            public bool SpaceAfter;
        }

        private sealed class Line
        {
            public readonly List<Run> Runs = new List<Run>();
            public float Width;
            public float Height;
            public float Ascent;
        }

        private sealed class Page
        {
            public readonly StringBuilder Ops = new StringBuilder();
        }

        // ------------------------------------------------------------ state

        private static readonly Regex TagRegex = new Regex(
            @"<(\/?)([a-zA-Z][a-zA-Z0-9]*)((?:[^>]|""[^""]*"")*)>", RegexOptions.Compiled);

        private static readonly Regex CssRuleRegex = new Regex(
            @"([^{}@]+)\{([^{}]*)\}", RegexOptions.Compiled);

        private static readonly HashSet<string> VoidTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "br", "hr", "img", "meta", "link", "input", "col", "source"
        };

        private static readonly HashSet<string> BlockTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "section",
            "ul", "ol", "li", "table", "thead", "tbody", "tfoot", "tr",
            "blockquote", "pre"
        };

        private static readonly int[] Helv = BuildWidths(false);
        private static readonly int[] HelvBold = BuildWidths(true);

        private readonly List<StyleRule> _rules = new List<StyleRule>();
        private Node _root;
        private readonly List<Page> _pages = new List<Page>();
        private Page _page;
        private float _y;

        private float ContentW { get { return PageW - MarginL - MarginR; } }
        private float BottomY { get { return PageH - MarginB; } }

        // ------------------------------------------------------------ entry

        public byte[] Execute(string html, string title)
        {
            if (string.IsNullOrEmpty(html)) html = "";
            html = StripNoise(html);
            ParseCss(html);
            var body = ExtractBody(html);
            _root = ParseHtml(body);

            NewPage();

            foreach (var child in _root.Children)
                RenderBlock(child, StyleFor(_root, null), MarginL, ContentW);

            return BuildPdf(title);
        }

        private static string StripNoise(string html)
        {
            html = Regex.Replace(html, @"<!--.*?-->", "", RegexOptions.Singleline);
            html = Regex.Replace(html, @"<!DOCTYPE[^>]*>", "", RegexOptions.IgnoreCase);
            return html;
        }

        private void ParseCss(string html)
        {
            var m = Regex.Match(html, @"<style[^>]*>(.*?)</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!m.Success) return;
            var css = RemoveMediaBlocks(m.Groups[1].Value);
            foreach (Match rule in CssRuleRegex.Matches(css))
            {
                var selector = rule.Groups[1].Value.Trim();
                if (selector.Length == 0 || selector.StartsWith("@")) continue;
                var props = ParseDeclarations(rule.Groups[2].Value);
                foreach (var sel in selector.Split(','))
                {
                    var s = sel.Trim();
                    if (s.Length == 0) continue;
                    _rules.Add(new StyleRule { Selector = s, Props = props });
                }
            }
        }

        private static string RemoveMediaBlocks(string css)
        {
            var sb = new StringBuilder(css.Length);
            int i = 0;
            while (i < css.Length)
            {
                if (string.CompareOrdinal(css, i, "@media", 0, 6) == 0)
                {
                    int depth = 0;
                    while (i < css.Length)
                    {
                        if (css[i] == '{') depth++;
                        else if (css[i] == '}')
                        {
                            depth--;
                            if (depth == 0) { i++; break; }
                        }
                        i++;
                    }
                }
                else
                {
                    sb.Append(css[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static Dictionary<string, string> ParseDeclarations(string decls)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in decls.Split(';'))
            {
                var idx = part.IndexOf(':');
                if (idx <= 0) continue;
                var key = part.Substring(0, idx).Trim().ToLowerInvariant();
                var val = part.Substring(idx + 1).Trim();
                if (val.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
                    val = val.Substring(0, val.Length - "!important".Length).Trim();
                if (key.Length > 0) dict[key] = val;
            }
            return dict;
        }

        private static string ExtractBody(string html)
        {
            var m = Regex.Match(html, @"<body[^>]*>(.*)</body\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (m.Success) return m.Groups[1].Value;
            // no body: drop head/style/title then use the rest
            var rest = Regex.Replace(html, @"<head[^>]*>.*?</head>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            rest = Regex.Replace(rest, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return rest;
        }

        private static Node ParseHtml(string html)
        {
            var root = new Node { Tag = "#root" };
            var stack = new Stack<Node>();
            stack.Push(root);
            int pos = 0;
            foreach (Match m in TagRegex.Matches(html))
            {
                var textChunk = html.Substring(pos, m.Index - pos);
                if (textChunk.Length > 0) AddText(stack.Peek(), textChunk);
                pos = m.Index + m.Length;

                var closing = m.Groups[1].Value == "/";
                var tag = m.Groups[2].Value.ToLowerInvariant();
                if (tag == "script" || tag == "title")
                {
                    if (!closing)
                    {
                        var closeIdx = html.IndexOf("</" + tag, pos, StringComparison.OrdinalIgnoreCase);
                        if (closeIdx >= 0)
                        {
                            var closeEnd = html.IndexOf('>', closeIdx);
                            pos = closeEnd >= 0 ? closeEnd + 1 : html.Length;
                        }
                    }
                    continue;
                }
                if (closing)
                {
                    if (stack.Count > 1 && stack.Peek().Tag == tag) stack.Pop();
                    else if (stack.Count > 1)
                    {
                        // unwind to matching ancestor if present
                        var arr = stack.ToArray(); // top first
                        for (int i = 0; i < arr.Length; i++)
                        {
                            if (arr[i].Tag == tag)
                            {
                                for (int j = 0; j < i; j++) stack.Pop();
                                break;
                            }
                        }
                    }
                    continue;
                }
                var node = new Node { Tag = tag, Parent = stack.Peek(), Attrs = ParseAttrs(m.Groups[3].Value) };
                stack.Peek().Children.Add(node);
                if (!VoidTags.Contains(tag)) stack.Push(node);
            }
            var tail = html.Substring(pos);
            if (tail.Length > 0) AddText(stack.Peek(), tail);
            return root;
        }

        private static void AddText(Node parent, string raw)
        {
            var text = DecodeEntities(raw);
            if (string.IsNullOrEmpty(text)) return;
            parent.Children.Add(new Node { Tag = "#text", Text = text, Parent = parent });
        }

        private static Dictionary<string, string> ParseAttrs(string attrText)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(attrText, @"([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*(?:=\s*(""[^""]*""|[^\s]+))?"))
            {
                var name = m.Groups[1].Value.ToLowerInvariant();
                var val = m.Groups[2].Success ? m.Groups[2].Value.Trim() : "";
                if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2)
                    val = val.Substring(1, val.Length - 2);
                dict[name] = DecodeEntities(val);
            }
            return dict;
        }

        private static string DecodeEntities(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<")
                 .Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&#39;", "'")
                 .Replace("&rsquo;", "\u2019").Replace("&lsquo;", "\u2018")
                 .Replace("&rdquo;", "\u201d").Replace("&ldquo;", "\u201c")
                 .Replace("&mdash;", "\u2014").Replace("&ndash;", "\u2013");
            s = Regex.Replace(s, @"&#(\d+);", delegate(Match m)
            {
                int code;
                if (int.TryParse(m.Groups[1].Value, out code) && code >= 32 && code <= 0xFFFF)
                    return char.ConvertFromUtf32(code);
                return "";
            });
            s = Regex.Replace(s, @"&#x([0-9a-fA-F]+);", delegate(Match m)
            {
                int code;
                if (int.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code) && code >= 32 && code <= 0xFFFF)
                    return char.ConvertFromUtf32(code);
                return "";
            });
            return s;
        }

        // ------------------------------------------------------------ style

        private Style StyleFor(Node node, Style inherited)
        {
            var style = inherited != null ? inherited.Clone() : new Style();
            // element defaults
            var tag = node.Tag;
            if (tag == "b" || tag == "strong") style.Bold = true;
            if (tag == "h1") { style.Bold = true; if (inherited == null) style.Size = 24f; style.MarginTop = 10f; style.MarginBottom = 6f; }
            else if (tag == "h2") { style.Bold = true; if (inherited == null) style.Size = 17f; style.MarginTop = 12f; style.MarginBottom = 5f; }
            else if (tag == "h3") { style.Bold = true; if (inherited == null) style.Size = 14f; style.MarginTop = 9f; style.MarginBottom = 4f; }
            else if (tag == "h4" || tag == "h5" || tag == "h6") { style.Bold = true; if (inherited == null) style.Size = 12f; style.MarginTop = 8f; style.MarginBottom = 4f; }
            else if (tag == "p") { if (inherited == null) { style.Size = 10f; style.MarginBottom = 6f; } }
            else if (tag == "li") { style.MarginBottom = 3f; }
            else if (tag == "th") { style.Bold = true; }

            // CSS rules (document order, later wins)
            foreach (var rule in _rules)
            {
                if (MatchesSelector(rule.Selector, node)) ApplyProps(style, rule.Props);
            }
            // inline style wins
            var inline = node.GetAttr("style");
            if (!string.IsNullOrEmpty(inline)) ApplyProps(style, ParseDeclarations(inline));

            if (style.MarginTop <= 0) style.MarginTop = 0;
            if (style.MarginBottom <= 0) style.MarginBottom = 0;
            return style;
        }

        private static void ApplyProps(Style style, Dictionary<string, string> props)
        {
            string v;
            if (props.TryGetValue("font-size", out v)) style.Size = ParsePx(v, style.Size);
            if (props.TryGetValue("font-weight", out v)) style.Bold = IsBoldWeight(v);
            if (props.TryGetValue("color", out v)) { var c = ParseColor(v); if (c != null) style.Color = c; }
            if (props.TryGetValue("background-color", out v)) { var c = ParseColor(v); if (c != null) style.Bg = c; }
            if (props.TryGetValue("background", out v))
            {
                var hex = Regex.Match(v, @"#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})");
                if (hex.Success) { var c = ParseColor("#" + hex.Groups[1].Value); if (c != null && (c[0] + c[1] + c[2]) < 2.95f) style.Bg = c; }
            }
            if (props.TryGetValue("text-transform", out v)) style.Upper = v.Trim().Equals("uppercase", StringComparison.OrdinalIgnoreCase);
            if (props.TryGetValue("line-height", out v))
            {
                float lh;
                if (float.TryParse(v.Replace("px", "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lh))
                {
                    if (lh > 3f) lh = lh / (style.Size <= 0 ? 10f : style.Size); // px line-height
                    if (lh >= 1f && lh <= 3f) style.LineHeight = lh;
                }
            }
            if (props.TryGetValue("margin", out v))
            {
                var parts = v.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float top, bottom;
                if (parts.Length == 1 && TryPx(parts[0], out top)) { style.MarginTop = top; style.MarginBottom = top; }
                else if (parts.Length >= 2)
                {
                    if (TryPx(parts[0], out top)) style.MarginTop = top;
                    if (TryPx(parts[parts.Length - 1], out bottom)) style.MarginBottom = bottom;
                }
            }
            if (props.TryGetValue("margin-top", out v)) { float t; if (TryPx(v, out t)) style.MarginTop = t; }
            if (props.TryGetValue("margin-bottom", out v)) { float b; if (TryPx(v, out b)) style.MarginBottom = b; }
        }

        private static bool IsBoldWeight(string v)
        {
            v = (v ?? "").Trim();
            if (v.Equals("bold", StringComparison.OrdinalIgnoreCase)) return true;
            int n;
            if (int.TryParse(v, out n)) return n >= 600;
            return false;
        }

        private static bool MatchesSelector(string selector, Node node)
        {
            if (node.IsText) return false;
            var parts = selector.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            if (!MatchesSimple(parts[parts.Length - 1], node)) return false;
            var idx = parts.Length - 2;
            var cur = node.Parent;
            while (idx >= 0)
            {
                if (cur == null) return false;
                if (MatchesSimple(parts[idx], cur)) idx--;
                cur = cur.Parent;
            }
            return true;
        }

        private static bool MatchesSimple(string simple, Node node)
        {
            if (node.IsText || node.Tag == "#root") return false;
            simple = simple.Trim();
            if (simple.Length == 0) return false;
            // handle tag.class or .class or tag or tag#id
            var m = Regex.Match(simple, @"^([a-zA-Z][a-zA-Z0-9]*)?(?:\.([-a-zA-Z0-9_]+))?(?:#([-a-zA-Z0-9_]+))?$");
            if (!m.Success) return false;
            if (m.Groups[1].Success && m.Groups[1].Value.Length > 0)
            {
                if (!node.Tag.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (m.Groups[2].Success && m.Groups[2].Value.Length > 0)
            {
                var cls = node.Class.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (!cls.Contains(m.Groups[2].Value)) return false;
            }
            if (m.Groups[3].Success && m.Groups[3].Value.Length > 0)
            {
                if (!string.Equals(node.GetAttr("id"), m.Groups[3].Value, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        private static float ParsePx(string v, float fallback)
        {
            float f;
            if (TryPx(v, out f) && f > 0) return f;
            return fallback;
        }

        private static bool TryPx(string v, out float pt)
        {
            pt = 0f;
            if (string.IsNullOrEmpty(v)) return false;
            v = v.Trim();
            float num;
            if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(v.Substring(0, v.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out num))
                { pt = num * 0.75f; return true; }
                return false;
            }
            if (v.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(v.Substring(0, v.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out num))
                { pt = num; return true; }
                return false;
            }
            if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out num))
            { pt = num * 0.75f; return true; }
            return false;
        }

        private static float[] ParseColor(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            v = v.Trim();
            var m = Regex.Match(v, @"#([0-9a-fA-F]{6})$|#([0-9a-fA-F]{3})$");
            if (m.Success)
            {
                if (m.Groups[1].Success)
                {
                    var hex = m.Groups[1].Value;
                    int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return new float[] { r / 255f, g / 255f, b / 255f };
                }
                var h3 = m.Groups[2].Value;
                int r3 = int.Parse(h3[0].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int g3 = int.Parse(h3[1].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int b3 = int.Parse(h3[2].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return new float[] { (r3 * 17) / 255f, (g3 * 17) / 255f, (b3 * 17) / 255f };
            }
            m = Regex.Match(v, @"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)");
            if (m.Success)
            {
                return new float[]
                {
                    int.Parse(m.Groups[1].Value) / 255f,
                    int.Parse(m.Groups[2].Value) / 255f,
                    int.Parse(m.Groups[3].Value) / 255f
                };
            }
            var named = NamedColor(v);
            return named;
        }

        private static float[] NamedColor(string v)
        {
            switch (v.ToLowerInvariant())
            {
                case "white": return new float[] { 1f, 1f, 1f };
                case "black": return new float[] { 0f, 0f, 0f };
                case "red": return new float[] { 1f, 0f, 0f };
                case "green": return new float[] { 0f, 0.5f, 0f };
                case "blue": return new float[] { 0f, 0f, 1f };
                case "gray":
                case "grey": return new float[] { 0.5f, 0.5f, 0.5f };
                case "silver": return new float[] { 0.75f, 0.75f, 0.75f };
                case "transparent":
                case "none": return null;
                default: return null;
            }
        }

        // ------------------------------------------------------------ measuring

        private static int[] BuildWidths(bool bold)
        {
            // Helvetica / Helvetica-Bold AFM widths (units/1000) for ASCII 32..126
            var regular = new Dictionary<char, int>();
            Action<char, int> set = (c, w) => regular[c] = w;
            set(' ', 278); set('!', 278); set('"', 355); set('#', 556); set('$', 556);
            set('%', 889); set('&', 667); set('\'', 191); set('(', 333); set(')', 333);
            set('*', 389); set('+', 584); set(',', 278); set('-', 333); set('.', 278);
            set('/', 278);
            for (char c = '0'; c <= '9'; c++) set(c, 556);
            set(':', 278); set(';', 278); set('<', 584); set('=', 584); set('>', 584);
            set('?', 556); set('@', 1015);
            var upper = new Dictionary<char, int>
            {
                {'A',667},{'B',667},{'C',722},{'D',722},{'E',667},{'F',611},{'G',778},{'H',722},
                {'I',278},{'J',500},{'K',667},{'L',556},{'M',833},{'N',722},{'O',778},{'P',667},
                {'Q',778},{'R',722},{'S',667},{'T',611},{'U',722},{'V',667},{'W',944},{'X',667},
                {'Y',667},{'Z',611}
            };
            foreach (var kv in upper) set(kv.Key, kv.Value);
            set('[', 278); set('\\', 278); set(']', 278); set('^', 469); set('_', 556);
            set('`', 333);
            var lower = new Dictionary<char, int>
            {
                {'a',556},{'b',556},{'c',500},{'d',556},{'e',556},{'f',278},{'g',556},{'h',556},
                {'i',222},{'j',222},{'k',500},{'l',222},{'m',833},{'n',556},{'o',556},{'p',556},
                {'q',556},{'r',333},{'s',500},{'t',278},{'u',556},{'v',500},{'w',722},{'x',500},
                {'y',500},{'z',500}
            };
            foreach (var kv in lower) set(kv.Key, kv.Value);
            set('{', 334); set('|', 260); set('}', 334); set('~', 584);

            var widths = new int[128];
            for (int i = 0; i < 128; i++) widths[i] = 556;
            foreach (var kv in regular)
            {
                int w = kv.Value;
                if (bold)
                {
                    // Helvetica-Bold is roughly 6-8% wider for most glyphs
                    w = (int)Math.Round(w * (w <= 300 ? 1.10 : 1.06));
                }
                widths[kv.Key] = w;
            }
            return widths;
        }

        private float TextWidth(string text, float size, bool bold)
        {
            var table = bold ? HelvBold : Helv;
            int units = 0;
            foreach (var ch in text)
            {
                units += (ch >= 32 && ch < 127) ? table[ch] : table['?'];
            }
            return units * size / 1000f;
        }

        private string NormalizeText(string text, Style style)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            text = text.Replace("\u20b9", "Rs.").Replace("\u20a8", "Rs.");
            text = text.Replace("\u2014", "-").Replace("\u2013", "-");
            text = text.Replace("\u2018", "'").Replace("\u2019", "'");
            text = text.Replace("\u201c", "\"").Replace("\u201d", "\"");
            text = text.Replace("\u2026", "...");
            text = text.Replace("\u00a0", " ");
            if (style.Upper) text = text.ToUpperInvariant();
            return text;
        }

        // ------------------------------------------------------------ layout

        private void NewPage()
        {
            _page = new Page();
            _pages.Add(_page);
            _y = MarginT;
        }

        private void EnsureSpace(float needed)
        {
            if (_y + needed > BottomY) NewPage();
        }

        private void RenderBlock(Node node, Style inherited, float x, float width)
        {
            if (node.IsText)
            {
                var text = node.Text;
                if (string.IsNullOrWhiteSpace(text)) return;
                var style = StyleFor(node, inherited);
                RenderParagraph(WrapInline(node, style, width), x, width, style);
                return;
            }

            var tag = node.Tag;
            if (tag == "script" || tag == "style" || tag == "head" || tag == "title" || tag == "br") return;

            if (tag == "table")
            {
                RenderTable(node, inherited, x, width);
                return;
            }

            var own = StyleFor(node, inherited);

            if (tag == "hr")
            {
                EnsureSpace(6f);
                _page.Ops.Append(string.Format(CultureInfo.InvariantCulture,
                    "0.8 0.8 0.8 RG 0.5 w {0:0.##} {1:0.##} m {2:0.##} {1:0.##} l S\n",
                    x, PageH - _y, x + width));
                _y += 8f;
                return;
            }

            var hasBlockChild = node.Children.Any(c => !c.IsText && BlockTags.Contains(c.Tag));
            if (!hasBlockChild)
            {
                // leaf-ish: wrap inline content (also applies to empty divs with only inline kids)
                if (node.Children.Count == 0 && tag != "p" && tag != "li")
                {
                    // empty element: nothing (containers keep their margin only if styled text absent)
                    return;
                }
                if (OwnMargin(own)) _y += own.MarginTop;
                var lines = WrapInline(node, own, width);
                if (lines.Count == 0 && node.Children.Count == 0)
                {
                    if (OwnMargin(own)) _y += own.MarginBottom;
                    return;
                }
                RenderParagraph(lines, x, width, own);
                if (OwnMargin(own)) _y += own.MarginBottom;
                _y += own.MarginBottom > 0 && !OwnMargin(own) ? own.MarginBottom : 0;
                return;
            }

            // container: apply own top margin, flow children
            if (OwnMargin(own)) _y += own.MarginTop;
            foreach (var child in node.Children)
            {
                if (child.IsText)
                {
                    if (string.IsNullOrWhiteSpace(child.Text)) continue;
                    RenderBlock(child, own, x, width);
                }
                else
                {
                    RenderBlock(child, own, x, width);
                }
            }
            if (OwnMargin(own)) _y += own.MarginBottom;
        }

        private static bool OwnMargin(Style s)
        {
            return s.MarginTop > 0 || s.MarginBottom > 0;
        }

        private List<Line> WrapInline(Node container, Style baseStyle, float width)
        {
            var runs = new List<Run>();
            CollectRuns(container, baseStyle, runs, false);
            return WrapRuns(runs, width);
        }

        private void CollectRuns(Node node, Style style, List<Run> runs, bool isInline)
        {
            foreach (var child in node.Children)
            {
                if (child.IsText)
                {
                    var text = NormalizeText(child.Text, style);
                    if (text.Length == 0) continue;
                    // split keeping spaces attached to the preceding word for wrapping
                    var parts = Regex.Split(text, @"(\s+)");
                    foreach (var part in parts)
                    {
                        if (part.Length == 0) continue;
                        var st = style;
                        if (part.Trim().Length == 0)
                        {
                            if (runs.Count == 0) continue; // skip leading space
                            runs.Add(new Run { Text = " ", Style = st, SpaceAfter = false });
                        }
                        else
                        {
                            runs.Add(new Run { Text = part, Style = st });
                        }
                    }
                    continue;
                }
                var tag = child.Tag;
                if (tag == "script" || tag == "style" || tag == "br" || tag == "hr") continue;
                if (tag == "table")
                {
                    // tables inside inline context: treat as block boundary marker - flush as separate paragraph handled by caller
                    continue;
                }
                var childStyle = StyleFor(child, style);
                if (tag == "b" || tag == "strong" || tag == "span" || tag == "a" || tag == "small" ||
                    tag == "em" || tag == "i" || tag == "u" || tag == "font" || tag == "label" ||
                    tag == "code" || tag == "s" || tag == "sub" || tag == "sup")
                {
                    CollectRuns(child, childStyle, runs, true);
                }
                else
                {
                    // nested block inside paragraph: collect its inline content as a run stream, mark break before/after
                    runs.Add(new Run { Text = "\n", Style = childStyle });
                    CollectRuns(child, childStyle, runs, true);
                    runs.Add(new Run { Text = "\n", Style = childStyle });
                }
            }
        }

        private List<Line> WrapRuns(List<Run> runs, float maxWidth)
        {
            var lines = new List<Line>();
            var line = new Line();
            float x = 0f;

            Action flush = () =>
            {
                if (line.Runs.Count > 0)
                {
                    lines.Add(line);
                }
                line = new Line();
                x = 0f;
            };

            foreach (var run in runs)
            {
                if (run.Text == "\n") { flush(); continue; }
                if (run.Text == " ")
                {
                    if (line.Runs.Count == 0) continue;
                    var spW = TextWidth(" ", run.Style.Size, run.Style.Bold);
                    if (x + spW > maxWidth) { flush(); continue; }
                    line.Runs.Add(new Run { Text = " ", Style = run.Style, Width = spW });
                    x += spW;
                    continue;
                }
                var w = TextWidth(run.Text, run.Style.Size, run.Style.Bold);
                if (x + w <= maxWidth || line.Runs.Count == 0)
                {
                    if (x + w > maxWidth && line.Runs.Count == 0)
                    {
                        // single word longer than line: hard-break
                        var chunk = FitChunk(run.Text, run.Style, maxWidth);
                        foreach (var part in chunk)
                        {
                            var pw = TextWidth(part, run.Style.Size, run.Style.Bold);
                            if (x + pw > maxWidth && line.Runs.Count > 0) { flush(); }
                            line.Runs.Add(new Run { Text = part, Style = run.Style, Width = pw });
                            x += pw;
                        }
                        continue;
                    }
                    line.Runs.Add(new Run { Text = run.Text, Style = run.Style, Width = w });
                    x += w;
                }
                else
                {
                    flush();
                    w = TextWidth(run.Text, run.Style.Size, run.Style.Bold);
                    if (w > maxWidth)
                    {
                        var chunk = FitChunk(run.Text, run.Style, maxWidth);
                        foreach (var part in chunk)
                        {
                            var pw = TextWidth(part, run.Style.Size, run.Style.Bold);
                            if (x + pw > maxWidth && line.Runs.Count > 0) flush();
                            line.Runs.Add(new Run { Text = part, Style = run.Style, Width = pw });
                            x += pw;
                        }
                    }
                    else
                    {
                        line.Runs.Add(new Run { Text = run.Text, Style = run.Style, Width = w });
                        x = w;
                    }
                }
            }
            flush();
            foreach (var l in lines)
            {
                float maxFs = 0f, ascent = 0f;
                foreach (var r in l.Runs)
                {
                    maxFs = Math.Max(maxFs, r.Style.Size);
                    ascent = Math.Max(ascent, r.Style.Size * 0.80f);
                }
                l.Ascent = ascent;
                l.Height = Math.Max(maxFs * 1.32f, ascent + maxFs * 0.30f);
                l.Width = l.Runs.Sum(r => r.Width);
            }
            return lines;
        }

        private List<string> FitChunk(string text, Style style, float maxWidth)
        {
            var parts = new List<string>();
            var current = "";
            foreach (var ch in text)
            {
                var next = current + ch;
                if (TextWidth(next, style.Size, style.Bold) > maxWidth && current.Length > 0)
                {
                    parts.Add(current);
                    current = ch.ToString();
                }
                else current = next;
            }
            if (current.Length > 0) parts.Add(current);
            return parts;
        }

        private void RenderParagraph(List<Line> lines, float x, float width, Style style)
        {
            if (lines.Count == 0)
            {
                // still consume a line height for empty paragraphs with margins
                if (style.MarginBottom > 0) _y += 0;
                return;
            }
            foreach (var line in lines)
            {
                EnsureSpace(line.Height);
                DrawLine(line, x);
                _y += line.Height;
            }
        }

        private void DrawLine(Line line, float x0)
        {
            float x = x0;
            foreach (var run in line.Runs)
            {
                if (run.Text == " ") { x += run.Width; continue; }
                var baseline = PageH - _y - run.Style.Size * 0.80f;
                if (run.Style.Bg != null)
                {
                    var pad = Math.Max(2f, run.Style.Size * 0.25f);
                    _page.Ops.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0:0.###} {1:0.###} {2:0.###} rg {3:0.##} {4:0.##} {5:0.##} {6:0.##} re f\n",
                        run.Style.Bg[0], run.Style.Bg[1], run.Style.Bg[2],
                        x - pad, baseline - run.Style.Size * 0.30f, run.Width + pad * 2, run.Style.Size * 1.30f));
                }
                DrawText(run.Text, x, baseline, run.Style);
                x += run.Width;
            }
        }

        private void DrawText(string text, float x, float baseline, Style style)
        {
            _page.Ops.Append(string.Format(CultureInfo.InvariantCulture,
                "BT /{0} {1:0.##} Tf {2:0.###} {3:0.###} {4:0.###} rg 1 0 0 1 {5:0.##} {6:0.##} Tm ",
                style.Bold ? "F2" : "F1", style.Size, style.Color[0], style.Color[1], style.Color[2], x, baseline));
            AppendPdfString(_page.Ops, text);
            _page.Ops.Append(" Tj ET\n");
        }

        // ------------------------------------------------------------ table

        private void RenderTable(Node table, Style inherited, float x0, float width)
        {
            var rows = new List<Node>();
            Node headerGroup = null;
            foreach (var group in table.Children)
            {
                if (group.Tag == "thead") { headerGroup = group; }
                foreach (var child in group.Children)
                {
                    if (child.Tag == "tr") rows.Add(child);
                }
                if (group.Tag == "tr") rows.Add(group);
            }
            if (rows.Count == 0) return;

            var headerRows = new List<Node>();
            if (headerGroup != null)
            {
                foreach (var tr in headerGroup.Children)
                    if (tr.Tag == "tr") headerRows.Add(tr);
            }
            var bodyRows = rows.Where(r => !headerRows.Contains(r)).ToList();
            if (bodyRows.Count == 0 && headerRows.Count == 0) return;
            if (bodyRows.Count == 0) { bodyRows = headerRows; headerRows = new List<Node>(); }

            int cols = 0;
            foreach (var tr in rows) cols = Math.Max(cols, CountCells(tr));
            if (cols == 0) return;

            // desired widths from widest word per column
            var desired = new float[cols];
            var floors = new float[cols];
            var styleOfCol = new Style[cols];
            foreach (var tr in rows)
            {
                var cells = Cells(tr);
                for (int i = 0; i < cells.Count && i < cols; i++)
                {
                    var cellStyle = StyleForCell(cells[i], inherited);
                    var cellRuns = new List<Run>();
                    CollectRuns(cells[i], cellStyle, cellRuns, true);
                    float widest = 0f, total = 0f;
                    foreach (var rT in cellRuns)
                    {
                        if (rT.Text == "\n") continue;
                        foreach (var word in Regex.Split(rT.Text, @"\s+"))
                        {
                            if (word.Length == 0) continue;
                            var ww = TextWidth(word, rT.Style.Size, rT.Style.Bold);
                            widest = Math.Max(widest, ww);
                            total += ww + TextWidth(" ", rT.Style.Size, rT.Style.Bold);
                        }
                    }
                    var capLocal = (width / cols) * 2.0f;
                    var widestP = widest + 12f;
                    var desiredW = Math.Max(widestP, Math.Min(total + 12f, capLocal));
                if (widest + 12f > floors[i]) floors[i] = widest + 12f;
                if (desiredW > desired[i]) desired[i] = desiredW;
                if (styleOfCol[i] == null) styleOfCol[i] = cellStyle;
                }
            }
            var cap = (width / cols) * 2.0f;
            var weights = new float[cols];
            float sum = 0f;
            for (int i = 0; i < cols; i++)
            {
                weights[i] = Math.Max(28f, Math.Min(desired[i] > 0 ? desired[i] : cap, cap));
                sum += weights[i];
            }
            var colW = new float[cols];
            if (sum <= 0)
            {
                for (int i = 0; i < cols; i++) colW[i] = width / cols;
            }
            else
            {
                for (int i = 0; i < cols; i++) colW[i] = width * weights[i] / sum;
            }

            for (int guard = 0; guard < 300; guard++)
            {
                int deficitIdx = -1;
                float worstDef = 0f;
                for (int i = 0; i < cols; i++)
                {
                    var d = floors[i] - colW[i];
                    if (d > worstDef) { worstDef = d; deficitIdx = i; }
                }
                if (deficitIdx < 0) break;
                int donorIdx = -1;
                float donorSlack = 0f;
                for (int i = 0; i < cols; i++)
                {
                    if (i == deficitIdx) continue;
                    var slack = colW[i] - floors[i];
                    if (slack > donorSlack) { donorSlack = slack; donorIdx = i; }
                }
                if (donorIdx < 0 || donorSlack <= 0.5f) break;
                var take = Math.Min(donorSlack, worstDef);
                colW[deficitIdx] += take;
                colW[donorIdx] -= take;
            }

            var tableX = x0;
            _hdrRows = headerRows;
            _hdrInherited = inherited;
            _hdrColW = colW;
            _hdrX = tableX;

            if (headerRows.Count > 0)
            {
                foreach (var tr in headerRows)
                    RenderRow(tr, inherited, tableX, colW, true);
            }
            foreach (var tr in bodyRows)
                RenderRow(tr, inherited, tableX, colW, false);

            _hdrRows = null;
            _hdrColW = null;
        }

        private List<Node> _hdrRows;
        private Style _hdrInherited;
        private float[] _hdrColW;
        private float _hdrX;

        private void RenderRow(Node tr, Style inherited, float x0, float[] colW, bool isHeader)
        {
            var cells = Cells(tr);
            var prepared = new List<KeyValuePair<Style, List<Line>>>();
            float rowH = 0f;
            float maxAscent = 0f;
            var padX = 6f;
            var padY = 5f;

            for (int i = 0; i < colW.Length; i++)
            {
                Style cs;
                List<Line> lines;
                if (i < cells.Count)
                {
                    cs = StyleForCell(cells[i], inherited);
                    var innerW = Math.Max(20f, colW[i] - padX * 2);
                    lines = WrapInline(cells[i], cs, innerW);
                }
                else
                {
                    cs = inherited != null ? inherited.Clone() : new Style();
                    lines = new List<Line>();
                }
                float h = 0f;
                foreach (var l in lines) { h += l.Height; maxAscent = Math.Max(maxAscent, l.Ascent); }
                rowH = Math.Max(rowH, h);
                prepared.Add(new KeyValuePair<Style, List<Line>>(cs, lines));
            }
            rowH += padY * 2;
            if (rowH > BottomY - MarginT) rowH = BottomY - MarginT; // clamp pathological rows

            if (_y + rowH > BottomY)
            {
                NewPage();
                if (!isHeader && _hdrRows != null && _hdrRows.Count > 0)
                {
                    var savedRows = _hdrRows;
                    var savedStyle = _hdrInherited;
                    var savedW = _hdrColW;
                    var savedX = _hdrX;
                    _hdrRows = null;
                    _hdrColW = null;
                    foreach (var hr in savedRows)
                        RenderRow(hr, savedStyle, savedX, savedW, true);
                    _hdrRows = savedRows;
                    _hdrInherited = savedStyle;
                    _hdrColW = savedW;
                    _hdrX = savedX;
                }
            }

            // background
            var bgStyle = prepared.Count > 0 ? prepared[0].Key : null;
            if (isHeader || (bgStyle != null && bgStyle.Bg != null))
            {
                var bg = isHeader ? (bgStyle != null && bgStyle.Bg != null ? bgStyle.Bg : new float[] { 0.94f, 0.96f, 0.98f }) : bgStyle.Bg;
                _page.Ops.Append(string.Format(CultureInfo.InvariantCulture,
                    "{0:0.###} {1:0.###} {2:0.###} rg {3:0.##} {4:0.##} {5:0.##} {6:0.##} re f\n",
                    bg[0], bg[1], bg[2], x0, PageH - _y - rowH, colW.Sum(), rowH));
            }

            // cell text
            var cx = x0;
            for (int i = 0; i < colW.Length && i < prepared.Count; i++)
            {
                var cs = prepared[i].Key;
                var lines = prepared[i].Value;
                var ty = _y + padY;
                foreach (var line in lines)
                {
                    // vertical alignment: top
                    var saveY = _y;
                    _y = ty;
                    DrawLineOffset(line, cx + padX, ty);
                    _y = saveY;
                    ty += line.Height;
                }
                cx += colW[i];
            }

            // borders
            var bottomY = PageH - _y - rowH;
            var border = isHeader ? "0.85 0.90 0.95" : "0.93 0.95 0.97";
            _page.Ops.Append(string.Format(CultureInfo.InvariantCulture,
                "{0} RG 0.5 w {1:0.##} {2:0.##} m {3:0.##} {2:0.##} l S\n",
                border, x0, bottomY, x0 + colW.Sum()));

            _y += rowH;
        }

        private void DrawLineOffset(Line line, float x0, float top)
        {
            float x = x0;
            foreach (var run in line.Runs)
            {
                if (run.Text == " ") { x += run.Width; continue; }
                var baseline = PageH - top - run.Style.Size * 0.80f;
                if (run.Style.Bg != null)
                {
                    var pad = Math.Max(2f, run.Style.Size * 0.25f);
                    _page.Ops.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0:0.###} {1:0.###} {2:0.###} rg {3:0.##} {4:0.##} {5:0.##} {6:0.##} re f\n",
                        run.Style.Bg[0], run.Style.Bg[1], run.Style.Bg[2],
                        x - pad, baseline - run.Style.Size * 0.30f, run.Width + pad * 2, run.Style.Size * 1.30f));
                }
                DrawText(run.Text, x, baseline, run.Style);
                x += run.Width;
            }
        }

        private Style StyleForCell(Node cell, Style inherited)
        {
            var s = StyleFor(cell, inherited);
            if (s.Size < 8f) s.Size = 9f;
            return s;
        }

        private static int CountCells(Node tr)
        {
            return tr.Children.Count(c => !c.IsText && c.Tag == "td" || !c.IsText && c.Tag == "th");
        }

        private static List<Node> Cells(Node tr)
        {
            return tr.Children.Where(c => !c.IsText && (c.Tag == "td" || c.Tag == "th")).ToList();
        }

        private string CellPlainText(Node cell, Style baseStyle)
        {
            var runs = new List<Run>();
            CollectRuns(cell, baseStyle, runs, true);
            var sb = new StringBuilder();
            foreach (var r in runs)
            {
                if (r.Text == "\n") sb.Append(' ');
                else sb.Append(r.Text);
            }
            return NormalizeText(sb.ToString(), baseStyle);
        }

        // ------------------------------------------------------------ pdf out

        private static void AppendPdfString(StringBuilder sb, string text)
        {
            sb.Append('(');
            foreach (var ch in text)
            {
                var b = ToWinAnsi(ch);
                if (b == (byte)'(' || b == (byte)')' || b == (byte)'\\')
                {
                    sb.Append('\\').Append((char)b);
                }
                else if (b < 32 || b > 126)
                {
                    sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                }
                else
                {
                    sb.Append((char)b);
                }
            }
            sb.Append(')');
        }

        private static byte ToWinAnsi(char ch)
        {
            if (ch < 128) return (byte)ch;
            try
            {
                var enc = Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
                var bytes = enc.GetBytes(new[] { ch });
                if (bytes.Length == 1 && bytes[0] != 63) return bytes[0];
                if (bytes.Length >= 1) return bytes[0];
            }
            catch { }
            return (byte)'?';
        }

        private byte[] BuildPdf(string title)
        {
            if (_pages.Count == 0) NewPage();

            var objects = new List<byte[]>();
            Action<string> add = s => objects.Add(Encoding.ASCII.GetBytes(s));

            var pageObjIds = new List<int>();
            int totalObjects = 2 /*catalog pages*/ + 2 /*fonts*/ + _pages.Count * 2;
            int firstPageId = 5;

            // 1: catalog
            add("<< /Type /Catalog /Pages 2 0 R >>");
            // 2: pages
            var kids = new StringBuilder();
            for (int i = 0; i < _pages.Count; i++)
            {
                if (i > 0) kids.Append(" ");
                kids.Append((firstPageId + i * 2)).Append(" 0 R");
            }
            add("<< /Type /Pages /Kids [" + kids + "] /Count " + _pages.Count + " >>");
            // 3: Helvetica
            add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            // 4: Helvetica-Bold
            add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

            for (int i = 0; i < _pages.Count; i++)
            {
                int pageId = firstPageId + i * 2;
                int contentId = pageId + 1;
                add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " +
                    PageW.ToString("0.##", CultureInfo.InvariantCulture) + " " +
                    PageH.ToString("0.##", CultureInfo.InvariantCulture) +
                    "] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents " + contentId + " 0 R >>");
                var content = Encoding.ASCII.GetBytes(_pages[i].Ops.ToString());
                var stream = new List<byte>();
                var header = Encoding.ASCII.GetBytes("<< /Length " + content.Length + " >>\nstream\n");
                stream.AddRange(header);
                stream.AddRange(content);
                stream.AddRange(Encoding.ASCII.GetBytes("\nendstream"));
                objects.Add(stream.ToArray());
            }

            using (var ms = new MemoryStream())
            {
                var w = new BinaryWriter(ms);
                var header = Encoding.ASCII.GetBytes("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
                w.Write(header);
                var offsets = new List<long>();
                for (int i = 0; i < objects.Count; i++)
                {
                    offsets.Add(ms.Position);
                    var num = Encoding.ASCII.GetBytes((i + 1) + " 0 obj\n");
                    w.Write(num);
                    w.Write(objects[i]);
                    w.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
                }
                var xrefPos = ms.Position;
                var xref = new StringBuilder();
                xref.Append("xref\n0 ").Append(objects.Count + 1).Append("\n");
                xref.Append("0000000000 65535 f \n");
                foreach (var off in offsets)
                    xref.Append(off.ToString("0000000000", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
                xref.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R");
                if (!string.IsNullOrEmpty(title))
                    xref.Append(" /Info << /Title (").Append(EscapePdfText(title)).Append(") >>");
                xref.Append(" >>\nstartxref\n").Append(xrefPos).Append("\n%%EOF\n");
                w.Write(Encoding.ASCII.GetBytes(xref.ToString()));
                w.Flush();
                return ms.ToArray();
            }
        }

        private static string EscapePdfText(string text)
        {
            var sb = new StringBuilder();
            foreach (var ch in text)
            {
                var b = ToWinAnsi(ch);
                if (b == (byte)'(' || b == (byte)')' || b == (byte)'\\') sb.Append((char)b);
                else if (b >= 32 && b <= 126) sb.Append((char)b);
                else sb.Append('?');
            }
            return sb.ToString();
        }
    }
}
