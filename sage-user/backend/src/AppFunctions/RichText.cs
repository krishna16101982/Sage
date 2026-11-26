using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SageUserApi.AppFunctions
{
    /// <summary>
    /// The order form wording's simple formatting ("Edit wording by section"). A section is text with new lines and only
    /// these tags:
    ///   &lt;b&gt; &lt;i&gt; &lt;u&gt;, &lt;span style="color:#rrggbb"&gt;, &lt;span style="background-color:#rrggbb"&gt;
    /// and the entities &amp;amp; &amp;lt; &amp;gt; &amp;quot; &amp;#39;. Anything else is plain text, so old plain wording
    /// still reads the same. The User Panel's editor (shared/utils/rich-text.ts) writes the same format.
    /// </summary>
    public static class RichText
    {
        /// <summary>A piece of text with one formatting.</summary>
        public sealed record Run(string Text, bool Bold, bool Italic, bool Underline, string Color, string Background)
        {
            public bool SameFormat(Run _other) => Bold == _other.Bold && Italic == _other.Italic && Underline == _other.Underline
                && Color == _other.Color && Background == _other.Background;
        }

        private static readonly Regex Token = new Regex(
            "<(?<close>/?)(?<tag>b|i|u)>|<span style=\"(?<prop>color|background-color):\\s*(?<hex>#[0-9a-fA-F]{6});?\">|(?<endspan></span>)|&(?<ent>amp|lt|gt|quot|#39);",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>The section as lines of runs (a line may be empty).</summary>
        public static List<List<Run>> Parse(string _markup)
        {
            var _lines = new List<List<Run>> { new List<Run>() };
            if (string.IsNullOrEmpty(_markup)) return _lines;
            string _text = _markup.Replace("\r\n", "\n");
            int _bold = 0, _italic = 0, _underline = 0;
            var _spans = new List<(string Prop, string Hex)>();

            void Add(string _piece)
            {
                string[] _parts = _piece.Split('\n');
                for (int i = 0; i < _parts.Length; i++)
                {
                    if (i > 0) _lines.Add(new List<Run>());
                    if (_parts[i].Length == 0) continue;
                    string _color = _spans.LastOrDefault(s => s.Prop == "color").Hex;
                    string _bg = _spans.LastOrDefault(s => s.Prop == "background-color").Hex;
                    _lines[^1].Add(new Run(_parts[i], _bold > 0, _italic > 0, _underline > 0, _color, _bg));
                }
            }

            int _at = 0;
            foreach (Match _m in Token.Matches(_text))
            {
                if (_m.Index > _at) Add(_text[_at.._m.Index]);
                _at = _m.Index + _m.Length;
                if (_m.Groups["tag"].Success)
                {
                    int _step = _m.Groups["close"].Value == "/" ? -1 : 1;
                    switch (_m.Groups["tag"].Value.ToLowerInvariant())
                    {
                        case "b": _bold = Math.Max(0, _bold + _step); break;
                        case "i": _italic = Math.Max(0, _italic + _step); break;
                        default: _underline = Math.Max(0, _underline + _step); break;
                    }
                }
                else if (_m.Groups["prop"].Success) _spans.Add((_m.Groups["prop"].Value.ToLowerInvariant(), _m.Groups["hex"].Value.ToLowerInvariant()));
                else if (_m.Groups["endspan"].Success) { if (_spans.Count > 0) _spans.RemoveAt(_spans.Count - 1); }
                else Add(WebUtility.HtmlDecode(_m.Value));
            }
            if (_at < _text.Length) Add(_text[_at..]);
            return _lines;
        }

        /// <summary>Lines of runs back to the canonical markup (runs with the same formatting merged).</summary>
        public static string Serialize(IEnumerable<List<Run>> _lines)
        {
            var _out = new StringBuilder();
            bool _first = true;
            foreach (List<Run> _line in _lines)
            {
                if (!_first) _out.Append('\n');
                _first = false;
                foreach (Run _run in Merge(_line))
                {
                    string _t = Escape(_run.Text);
                    if (_run.Underline) _t = $"<u>{_t}</u>";
                    if (_run.Italic) _t = $"<i>{_t}</i>";
                    if (_run.Bold) _t = $"<b>{_t}</b>";
                    if (_run.Background != null) _t = $"<span style=\"background-color:{_run.Background}\">{_t}</span>";
                    if (_run.Color != null) _t = $"<span style=\"color:{_run.Color}\">{_t}</span>";
                    _out.Append(_t);
                }
            }
            return _out.ToString();
        }

        /// <summary>
        /// What the API saves: the canonical markup (only the allowed tags; any other tag is kept as visible text), each
        /// line's ends trimmed and the section's empty first / last lines dropped. Null when there is no text.
        /// </summary>
        public static string Clean(string _markup)
        {
            if (string.IsNullOrWhiteSpace(_markup)) return null;
            List<List<Run>> _lines = Parse(_markup).Select(TrimLine).ToList();
            while (_lines.Count > 0 && _lines[0].Count == 0) _lines.RemoveAt(0);
            while (_lines.Count > 0 && _lines[^1].Count == 0) _lines.RemoveAt(_lines.Count - 1);
            return _lines.Count == 0 ? null : Serialize(_lines);
        }

        /// <summary>The words only (for lengths and empty checks).</summary>
        public static string Plain(string _markup)
            => string.Join("\n", Parse(_markup).Select(l => string.Concat(l.Select(r => r.Text))));

        /// <summary>The non-empty lines, each as runs (signature clauses, footer: one per line).</summary>
        public static List<List<Run>> Lines(string _markup)
            => Parse(_markup).Select(TrimLine).Where(l => l.Count > 0).ToList();

        /// <summary>#rrggbb to red, green, blue.</summary>
        public static (byte R, byte G, byte B) Rgb(string _hex)
            => (byte.Parse(_hex.AsSpan(1, 2), NumberStyles.HexNumber), byte.Parse(_hex.AsSpan(3, 2), NumberStyles.HexNumber), byte.Parse(_hex.AsSpan(5, 2), NumberStyles.HexNumber));

        private static string Escape(string _text)
            => _text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static List<Run> Merge(List<Run> _line)
        {
            var _merged = new List<Run>();
            foreach (Run _run in _line.Where(r => r.Text.Length > 0))
            {
                if (_merged.Count > 0 && _merged[^1].SameFormat(_run)) _merged[^1] = _merged[^1] with { Text = _merged[^1].Text + _run.Text };
                else _merged.Add(_run);
            }
            return _merged;
        }

        /// <summary>Removes the spaces at both ends of a line (across runs); a line of spaces becomes empty.</summary>
        private static List<Run> TrimLine(List<Run> _line)
        {
            List<Run> _runs = Merge(_line);
            while (_runs.Count > 0)
            {
                string _t = _runs[0].Text.TrimStart();
                if (_t.Length > 0) { _runs[0] = _runs[0] with { Text = _t }; break; }
                _runs.RemoveAt(0);
            }
            while (_runs.Count > 0)
            {
                string _t = _runs[^1].Text.TrimEnd();
                if (_t.Length > 0) { _runs[^1] = _runs[^1] with { Text = _t }; break; }
                _runs.RemoveAt(_runs.Count - 1);
            }
            return _runs;
        }
    }
}
