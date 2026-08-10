using System.Collections.Generic;
using System.Text;
using TMPro;

/// <summary>
/// Shapes Arabic text for TextMeshPro.
///
/// TextMeshPro does not run an OpenType shaping engine — <c>isRightToLeftText</c>
/// only flips the advance direction and never substitutes the contextual
/// (initial / medial / final) Arabic letterforms that make letters connect.
/// This helper does the shaping TMP won't: it replaces each base Arabic letter
/// with its presentation form based on its neighbours, merges lam-alef
/// ligatures, and preserves digit/Latin runs.
///
/// Use <see cref="ApplyToTmp"/> for TextMeshPro labels: it reverses each
/// WRAPPED LINE individually (a whole-string reversal would place the last
/// logical line on top when the text wraps). Strings without Arabic letters
/// pass through untouched, so it is safe to apply to every localized string.
///
/// This class is dependency-free by design (only TMP + BCL) so it can live in
/// a package and be reused across projects. The host application sets
/// <see cref="IsRtlLocale"/> once at boot and whenever the locale changes.
/// </summary>
public static class ArabicShaper
{
    // ── Presentation forms ───────────────────────────────────────────
    // Indexed by (letter - 0x0621). Each row: [isolated, final, initial, medial].
    // A 0 in a slot means that form does not exist for that letter.
    private static readonly int[,] Forms =
    {
        // 0621 hamza                       (never joins)
        { 0xFE80, 0xFE80, 0xFE80, 0xFE80 },
        // 0622 alef madda
        { 0xFE81, 0xFE82, 0,      0      },
        // 0623 alef hamza above
        { 0xFE83, 0xFE84, 0,      0      },
        // 0624 waw hamza
        { 0xFE85, 0xFE86, 0,      0      },
        // 0625 alef hamza below
        { 0xFE87, 0xFE88, 0,      0      },
        // 0626 yeh hamza
        { 0xFE89, 0xFE8A, 0xFE8B, 0xFE8C },
        // 0627 alef
        { 0xFE8D, 0xFE8E, 0,      0      },
        // 0628 beh
        { 0xFE8F, 0xFE90, 0xFE91, 0xFE92 },
        // 0629 teh marbuta
        { 0xFE93, 0xFE94, 0,      0      },
        // 062A teh
        { 0xFE95, 0xFE96, 0xFE97, 0xFE98 },
        // 062B theh
        { 0xFE99, 0xFE9A, 0xFE9B, 0xFE9C },
        // 062C jeem
        { 0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0 },
        // 062D hah
        { 0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4 },
        // 062E khah
        { 0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8 },
        // 062F dal
        { 0xFEA9, 0xFEAA, 0,      0      },
        // 0630 thal
        { 0xFEAB, 0xFEAC, 0,      0      },
        // 0631 reh
        { 0xFEAD, 0xFEAE, 0,      0      },
        // 0632 zain
        { 0xFEAF, 0xFEB0, 0,      0      },
        // 0633 seen
        { 0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4 },
        // 0634 sheen
        { 0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8 },
        // 0635 sad
        { 0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC },
        // 0636 dad
        { 0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0 },
        // 0637 tah
        { 0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4 },
        // 0638 zah
        { 0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8 },
        // 0639 ain
        { 0xFEC9, 0xFECA, 0xFECB, 0xFECC },
        // 063A ghain
        { 0xFECD, 0xFECE, 0xFECF, 0xFED0 },
        // 063B..063F unassigned — never shaped
        { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 },
        // 0640 tatweel (kashida) — joining, its own form
        { 0x0640, 0x0640, 0x0640, 0x0640 },
        // 0641 feh
        { 0xFED1, 0xFED2, 0xFED3, 0xFED4 },
        // 0642 qaf
        { 0xFED5, 0xFED6, 0xFED7, 0xFED8 },
        // 0643 kaf
        { 0xFED9, 0xFEDA, 0xFEDB, 0xFEDC },
        // 0644 lam
        { 0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0 },
        // 0645 meem
        { 0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4 },
        // 0646 noon
        { 0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8 },
        // 0647 heh
        { 0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC },
        // 0648 waw
        { 0xFEED, 0xFEEE, 0,      0      },
        // 0649 alef maksura
        { 0xFEEF, 0xFEF0, 0,      0      },
        // 064A yeh
        { 0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4 },
    };

    /// <summary>
    /// Letters that never connect FORWARD (they only have isolated/final forms):
    /// alef and its variants, dal/dhal, ra, zain, waw, teh marbuta, alef maksura,
    /// and hamza.
    /// </summary>
    private static readonly HashSet<int> NonJoining =
        new HashSet<int> { 0x0621, 0x0622, 0x0623, 0x0624, 0x0625, 0x0627, 0x0629,
                           0x062F, 0x0630, 0x0631, 0x0632, 0x0648, 0x0649 };

    /// <summary>Lam-alef ligatures: alef family → (isolated, final) form.</summary>
    private static readonly Dictionary<int, (int iso, int fin)> LamAlef =
        new Dictionary<int, (int, int)>
        {
            { 0x0622, (0xFEF9, 0xFEFA) }, // لا آ  (lam + alef madda)
            { 0x0623, (0xFEF5, 0xFEF6) }, // لأ    (lam + alef hamza above)
            { 0x0625, (0xFEF7, 0xFEF8) }, // لإ    (lam + alef hamza below)
            { 0x0627, (0xFEFB, 0xFEFC) }, // لا    (lam + alef)
        };

    // ── Host configuration ───────────────────────────────────────────
    // The shaper has no dependency on any game code — the host application
    // (e.g. your localization bootstrap) sets IsRtlLocale once at boot and
    // again whenever the locale changes. If your project is Arabic-only you
    // can leave it false and always pass forceRtlShaping:true, or simply not
    // use ApplyToTmp's auto-gating at all.

    /// <summary>
    /// True when the currently active language is an Arabic-script language
    /// (ar, fa, ur, ps, ku, …). Set it from your localization setup when the
    /// locale is restored or changed — <see cref="ApplyToTmp"/> uses it to
    /// decide whether to shape text automatically. Defaults to false.
    /// </summary>
    public static bool IsRtlLocale { get; set; }

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Shapes Arabic letters into contextual presentation forms and reverses the
    /// string so it reads right-to-left when rendered left-to-right. Strings
    /// without Arabic letters are returned unchanged.
    ///
    /// NOTE: this reverses the WHOLE string, so it is only correct for text that
    /// fits on one line. For multi-line TextMeshPro labels use
    /// <see cref="ApplyToTmp"/>, which reverses each wrapped line individually.
    /// </summary>
    public static string Shape(string text)
    {
        if (string.IsNullOrEmpty(text) || !ContainsArabic(text))
            return text;

        string shaped = ShapeLogical(text);
        return ReverseForRtl(shaped);
    }

    /// <summary>
    /// Shapes <paramref name="text"/> and assigns it to <paramref name="tmp"/>,
    /// reversing each WRAPPED LINE individually so multi-line text keeps the
    /// correct reading order (whole-string reversal would put the last logical
    /// line on top). Works in two passes:
    ///   1. Shape in logical order and let TMP wrap it.
    ///   2. Read the real line breaks from <c>textInfo.lineInfo</c> and reverse
    ///      each line's characters separately, joined with newlines.
    ///
    /// Returns the final string (already assigned). Callers that post-process
    /// the text with rich-text tags (e.g. <c>$word$</c> → &lt;color&gt;) should
    /// apply them to the RETURN value AFTER this call.
    ///
    /// Known limitation: an authored rich-text tag that TMP splits exactly at a
    /// wrap boundary leaves an unbalanced tag in its line segment; it is then
    /// treated as plain characters and may render literally. Rare in practice.
    /// </summary>
    public static string ApplyToTmp(TMP_Text tmp, string text, bool forceRtlShaping = false)
    {
        if (tmp == null)
            return text ?? "";

        // The shaper owns RTL: it shapes and reverses the string itself, so TMP
        // must always lay out LTR. Reset the flag unconditionally so a prefab
        // left with native RTL on (e.g. from earlier testing) cannot corrupt
        // ANY locale's layout, not just Arabic.
        tmp.isRightToLeftText = false;

        // Only shape for Arabic-script locales — unless forced (e.g. a language
        // row showing "العربية" while English is selected).
        if (string.IsNullOrEmpty(text) || !ContainsArabic(text) ||
            (!forceRtlShaping && !IsRtlLocale))
        {
            tmp.text = text;
            return text;
        }

        // Pass 1: shape in logical order and let TMP compute real line breaks.
        string shaped = ShapeLogical(text);
        tmp.text = shaped;
        tmp.ForceMeshUpdate(true);

        TMP_TextInfo info = tmp.textInfo;
        if (info == null || info.lineInfo == null || info.lineCount <= 1 ||
            info.characterInfo == null)
        {
            string single = ReverseForRtl(shaped);
            tmp.text = single;
            return single;
        }

        // Pass 2: reverse each line individually, preserving line order.
        var sb = new StringBuilder(shaped.Length + info.lineCount);
        for (int l = 0; l < info.lineCount; l++)
        {
            TMP_LineInfo line = info.lineInfo[l];
            int first = line.firstVisibleCharacterIndex;
            int last = line.lastVisibleCharacterIndex;

            if (first >= 0 && last >= 0 &&
                first < info.characterInfo.Length && last < info.characterInfo.Length)
            {
                int startIdx = info.characterInfo[first].index;
                int endIdx = info.characterInfo[last].index + info.characterInfo[last].stringLength;
                if (startIdx >= 0 && endIdx > startIdx && endIdx <= shaped.Length)
                    sb.Append(ReverseForRtl(shaped.Substring(startIdx, endIdx - startIdx)));
            }

            if (l < info.lineCount - 1)
                sb.Append('\n');
        }

        string result = sb.ToString();
        tmp.text = result;
        return result;
    }

    /// <summary>True if the string contains any Arabic base letter (U+0621–U+064A).</summary>
    public static bool ContainsArabic(string text)
    {
        if (text == null) return false;
        for (int i = 0; i < text.Length; i++)
        {
            int c = text[i];
            if (c >= 0x0621 && c <= 0x064A)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True if the string contains Arabic base letters OR their shaped
    /// presentation forms (U+FB50–U+FDFF, U+FE70–U+FEFF). Used where the text
    /// may already have been through <see cref="ShapeLogical"/>.
    /// </summary>
    private static bool ContainsRtlScript(string text)
    {
        if (text == null) return false;
        for (int i = 0; i < text.Length; i++)
        {
            int c = text[i];
            if ((c >= 0x0621 && c <= 0x064A) ||
                (c >= 0xFB50 && c <= 0xFDFF) ||
                (c >= 0xFE70 && c <= 0xFEFF))
                return true;
        }
        return false;
    }

    // ── Shaping pass (logical order) ─────────────────────────────────

    private static string ShapeLogical(string text)
    {
        var sb = new StringBuilder(text.Length);
        int n = text.Length;

        for (int i = 0; i < n; i++)
        {
            char c = text[i];

            if (!IsBaseArabic(c))
            {
                sb.Append(c);
                continue;
            }

            int next = NextSignificantIndex(text, i, n);

            // Lam-alef ligature: ل followed directly (ignoring diacritics) by an alef.
            if (c == 0x0644 && next < n && LamAlef.TryGetValue(text[next], out var lig))
            {
                int prev = PrevSignificantIndex(text, i, n);
                bool prevJoins = prev >= 0 && IsJoining(text[prev]);
                sb.Append((char)(prevJoins ? lig.fin : lig.iso));

                // Carry any diacritics sitting between the lam and the alef
                // (e.g. fatha on ل in لَا) so they are not silently dropped.
                for (int d = i + 1; d < next; d++)
                    sb.Append(text[d]);

                i = next; // consume the alef
                continue;
            }

            int p = PrevSignificantIndex(text, i, n);
            bool joinsFromPrev = p >= 0 && IsJoining(text[p]);
            bool joinsToNext = IsJoining(c) && next < n && IsBaseArabic(text[next]);

            int idx = 0;
            if (joinsFromPrev) idx += 1; // final
            if (joinsToNext)   idx += 2; // initial  (both → medial = 3)

            int form = Forms[c - 0x0621, idx];
            sb.Append((char)(form != 0 ? form : Forms[c - 0x0621, 0]));
        }

        return sb.ToString();
    }

    // ── RTL reversal (keeps digit/Latin runs in order) ───────────────

    private static string ReverseForRtl(string shaped)
    {
        var units = new List<string>();
        int n = shaped.Length;
        int i = 0;

        while (i < n)
        {
            char c = shaped[i];

            // Rich-text span: <name=value>content</name> stays ONE unit so the
            // tags travel with their content while the content is reversed
            // (bidi-correct for inline markup). Void tags like <sprite=0> and
            // <br> are atomic units kept verbatim.
            if (c == '<' && TryMatchTagSpan(shaped, i, out string span))
            {
                units.Add(span);
                i += span.Length;
                continue;
            }

            // Consecutive LTR-safe characters (Latin, digits, common inline
            // punctuation) stay together so "15" and "Level" are not scrambled.
            if (IsLtrRunChar(c))
            {
                int start = i;
                while (i < n && IsLtrRunChar(shaped[i])) i++;
                units.Add(shaped.Substring(start, i - start));
            }
            else
            {
                // Base character + any attached diacritics form one unit.
                int start = i;
                i++;
                while (i < n && IsDiacritic(shaped[i])) i++;
                units.Add(shaped.Substring(start, i - start));
            }
        }

        units.Reverse();
        return string.Concat(units);
    }

    /// <summary>
    /// If <paramref name="text"/> at <paramref name="start"/> begins an opening
    /// rich-text tag, returns the whole "&lt;open&gt;content&lt;/open&gt;" span as a
    /// single unit with the content reversed (recursively). Returns false for
    /// closing tags and unmatched opens, which are then processed as ordinary
    /// characters. Void tags (<c>sprite</c>, <c>br</c>) are atomic units kept
    /// verbatim.
    /// </summary>
    private static bool TryMatchTagSpan(string text, int start, out string span)
    {
        span = null;
        int openEnd = text.IndexOf('>', start + 1);
        if (openEnd < 0) return false;

        string tag = text.Substring(start + 1, openEnd - start - 1).Trim();
        if (tag.Length == 0 || tag[0] == '/') return false; // closing or empty tag

        // Tag name is the first token before '=' or whitespace ("color", "size", …).
        int eq = tag.IndexOf('=');
        string name = (eq >= 0 ? tag.Substring(0, eq) : tag).Split(' ')[0].Trim();
        if (name.Length == 0) return false;

        // Void tags carry no inner text — atomic unit, kept verbatim.
        if (name == "sprite" || name == "br")
        {
            span = text.Substring(start, openEnd - start + 1);
            return true;
        }

        // Pair with the closing tag and reverse the inner content recursively.
        string closeTag = "</" + name + ">";
        int close = text.IndexOf(closeTag, openEnd + 1, System.StringComparison.Ordinal);
        if (close < 0) return false;

        string inner = text.Substring(openEnd + 1, close - openEnd - 1);

        // Reverse the inner content only when it actually contains Arabic — a
        // pure-Latin span (e.g. <b>Level 15</b>) is an LTR run and must keep
        // its order (bidi: strong LTR characters get their own level). Uses the
        // RTL-script check (not just base letters) because by the time reversal
        // runs, the content is already shaped into presentation forms.
        span = text.Substring(start, openEnd - start + 1)
             + (ContainsRtlScript(inner) ? ReverseForRtl(inner) : inner)
             + closeTag;
        return true;
    }

    // ── Character classification ─────────────────────────────────────

    private static bool IsBaseArabic(char c) => c >= 0x0621 && c <= 0x064A;

    private static bool IsJoining(char c) =>
        IsBaseArabic(c) && !NonJoining.Contains(c);

    private static bool IsDiacritic(char c) =>
        (c >= 0x064B && c <= 0x0655) || c == 0x0670;

    private static bool IsLtrRunChar(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
        (c >= 0x0660 && c <= 0x0669) || // Arabic-Indic digits keep their order too
        c == '.' || c == ',' || c == '/' || c == '%' || c == '+' || c == '-' || c == ':';

    private static int PrevSignificantIndex(string text, int i, int n)
    {
        for (int j = i - 1; j >= 0; j--)
        {
            if (!IsDiacritic(text[j]))
                return j;
        }
        return -1;
    }

    private static int NextSignificantIndex(string text, int i, int n)
    {
        for (int j = i + 1; j < n; j++)
        {
            if (!IsDiacritic(text[j]))
                return j;
        }
        return n;
    }
}
