#!/usr/bin/env python3
"""Mirror of ArabicShaper.cs to validate the shaping + RTL reversal logic.

SYNC NOTE: this file is the package copy — keep it identical to
<project>/mcp/arabic_mirror_test.py when the C# algorithm changes.

Runs the exact same algorithm on sample strings and prints the resulting
codepoints so we can verify:
  1. Contextual forms are chosen correctly (isolated/initial/medial/final)
  2. Lam-alef ligatures merge
  3. Digit/Latin runs are preserved
  4. English-only strings pass through untouched

Run from anywhere:  python3 arabic_mirror_test.py
"""

# Forms table indexed by (letter - 0x0621): [isolated, final, initial, medial]
FORMS = [
    [0xFE80, 0xFE80, 0xFE80, 0xFE80],  # 0621 hamza
    [0xFE81, 0xFE82, 0,      0      ],  # 0622 alef madda
    [0xFE83, 0xFE84, 0,      0      ],  # 0623 alef hamza above
    [0xFE85, 0xFE86, 0,      0      ],  # 0624 waw hamza
    [0xFE87, 0xFE88, 0,      0      ],  # 0625 alef hamza below
    [0xFE89, 0xFE8A, 0xFE8B, 0xFE8C ],  # 0626 yeh hamza
    [0xFE8D, 0xFE8E, 0,      0      ],  # 0627 alef
    [0xFE8F, 0xFE90, 0xFE91, 0xFE92 ],  # 0628 beh
    [0xFE93, 0xFE94, 0,      0      ],  # 0629 teh marbuta
    [0xFE95, 0xFE96, 0xFE97, 0xFE98 ],  # 062A teh
    [0xFE99, 0xFE9A, 0xFE9B, 0xFE9C ],  # 062B theh
    [0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0 ],  # 062C jeem
    [0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4 ],  # 062D hah
    [0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8 ],  # 062E khah
    [0xFEA9, 0xFEAA, 0,      0      ],  # 062F dal
    [0xFEAB, 0xFEAC, 0,      0      ],  # 0630 thal
    [0xFEAD, 0xFEAE, 0,      0      ],  # 0631 reh
    [0xFEAF, 0xFEB0, 0,      0      ],  # 0632 zain
    [0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4 ],  # 0633 seen
    [0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8 ],  # 0634 sheen
    [0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC ],  # 0635 sad
    [0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0 ],  # 0636 dad
    [0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4 ],  # 0637 tah
    [0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8 ],  # 0638 zah
    [0xFEC9, 0xFECA, 0xFECB, 0xFECC ],  # 0639 ain
    [0xFECD, 0xFECE, 0xFECF, 0xFED0 ],  # 063A ghain
    [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0],  # 063B-063F
    [0x0640, 0x0640, 0x0640, 0x0640],  # 0640 tatweel
    [0xFED1, 0xFED2, 0xFED3, 0xFED4 ],  # 0641 feh
    [0xFED5, 0xFED6, 0xFED7, 0xFED8 ],  # 0642 qaf
    [0xFED9, 0xFEDA, 0xFEDB, 0xFEDC ],  # 0643 kaf
    [0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0 ],  # 0644 lam
    [0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4 ],  # 0645 meem
    [0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8 ],  # 0646 noon
    [0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC ],  # 0647 heh
    [0xFEED, 0xFEEE, 0,      0      ],  # 0648 waw
    [0xFEEF, 0xFEF0, 0,      0      ],  # 0649 alef maksura
    [0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4 ],  # 064A yeh
]

NON_JOINING = {0x0621, 0x0622, 0x0623, 0x0624, 0x0625, 0x0627, 0x0629,
               0x062F, 0x0630, 0x0631, 0x0632, 0x0648, 0x0649}

LAM_ALEF = {
    0x0622: (0xFEF9, 0xFEFA),  # لا آ
    0x0623: (0xFEF5, 0xFEF6),  # لأ
    0x0625: (0xFEF7, 0xFEF8),  # لإ
    0x0627: (0xFEFB, 0xFEFC),  # لا
}


def is_base_arabic(c):
    return 0x0621 <= c <= 0x064A


def is_joining(c):
    return is_base_arabic(c) and c not in NON_JOINING


def is_diacritic(c):
    return (0x064B <= c <= 0x0655) or c == 0x0670


def is_ltr_run_char(c):
    return ('a' <= c <= 'z' or 'A' <= c <= 'Z' or '0' <= c <= '9' or
            0x0660 <= ord(c) <= 0x0669 or  # Arabic-Indic digits
            c in '.,/%+-:')


def prev_significant(text, i):
    for j in range(i - 1, -1, -1):
        if not is_diacritic(ord(text[j])):
            return j
    return -1


def next_significant(text, i):
    for j in range(i + 1, len(text)):
        if not is_diacritic(ord(text[j])):
            return j
    return len(text)


def shape_logical(text):
    out = []
    n = len(text)
    i = 0
    while i < n:
        c = ord(text[i])
        if not is_base_arabic(c):
            out.append(text[i])
            i += 1
            continue

        nxt = next_significant(text, i)

        # Lam-alef ligature
        if c == 0x0644 and nxt < n and ord(text[nxt]) in LAM_ALEF:
            iso, fin = LAM_ALEF[ord(text[nxt])]
            p = prev_significant(text, i)
            prev_joins = p >= 0 and is_joining(ord(text[p]))
            out.append(chr(fin if prev_joins else iso))
            # carry diacritics between lam and alef
            for d in range(i + 1, nxt):
                out.append(text[d])
            i = nxt + 1
            continue

        p = prev_significant(text, i)
        joins_from_prev = p >= 0 and is_joining(ord(text[p]))
        joins_to_next = is_joining(c) and nxt < n and is_base_arabic(ord(text[nxt]))

        idx = 0
        if joins_from_prev:
            idx += 1
        if joins_to_next:
            idx += 2

        form = FORMS[c - 0x0621][idx]
        out.append(chr(form if form else FORMS[c - 0x0621][0]))
        i += 1
    return "".join(out)


def try_match_tag_span(text, start):
    """Mirror of C# TryMatchTagSpan: returns the balanced <open>…</open> span
    with the inner content reversed, or None if not a matchable tag."""
    open_end = text.find('>', start + 1)
    if open_end < 0:
        return None
    tag = text[start + 1:open_end].strip()
    if not tag or tag[0] == '/':
        return None
    eq = tag.find('=')
    name = (tag[:eq] if eq >= 0 else tag).split(' ')[0].strip()
    if not name:
        return None
    if name in ('sprite', 'br'):
        return text[start:open_end + 1]
    close_tag = '</' + name + '>'
    close = text.find(close_tag, open_end + 1)
    if close < 0:
        return None
    inner = text[open_end + 1:close]
    # Reverse only RTL content — pure-Latin spans keep order. Matches the C#
    # ContainsRtlScript: base letters OR shaped presentation forms.
    if any((0x0621 <= ord(ch) <= 0x064A) or
           (0xFB50 <= ord(ch) <= 0xFDFF) or
           (0xFE70 <= ord(ch) <= 0xFEFF) for ch in inner):
        inner = reverse_for_rtl(inner)
    return text[start:open_end + 1] + inner + close_tag


def reverse_for_rtl(shaped):
    units = []
    n = len(shaped)
    i = 0
    while i < n:
        c = shaped[i]
        span = try_match_tag_span(shaped, i) if c == '<' else None
        if span is not None:
            units.append(span)
            i += len(span)
            continue
        if is_ltr_run_char(c):
            start = i
            while i < n and is_ltr_run_char(shaped[i]):
                i += 1
            units.append(shaped[start:i])
        else:
            start = i
            i += 1
            while i < n and is_diacritic(ord(shaped[i])):
                i += 1
            units.append(shaped[start:i])
    units.reverse()
    return "".join(units)


def shape(text):
    if not text or not any(0x0621 <= ord(ch) <= 0x064A for ch in text):
        return text
    return reverse_for_rtl(shape_logical(text))


def cps(s):
    return " ".join(f"U+{ord(c):04X}" for c in s)


SAMPLES = [
    "مرحبا",                 # marhaba — full joining test
    "السلام عليكم",          # as-salam alaykum — lam-alef + word spacing
    "المستوى 5",             # level 5 — digit preservation
    "المستوى 15",            # level 15 — multi-digit run preservation
    "لا",                    # lam-alef ligature isolated
    "السلام",                # lam-alef inside word
    "Hello World",           # pure English → unchanged
    "Level 5",               # English + digit → unchanged
    "قائمة المستويات",       # list of levels
    "مستوى",                 # level
    "لَا",                   # lam + fatha + alef — diacritics preserved through ligature
    "المستوى ١٥",            # level 15 with Arabic-Indic digits — run preserved
    "<color=red>مرحبا</color>",   # rich-text span: tags travel with reversed content
    "<b>مستوى</b> 15",            # tag span + digit run ordering
    "<sprite=0>مرحبا",            # void tag stays atomic
]

print("=== ARABIC SHAPER MIRROR VALIDATION ===\n")
for s in SAMPLES:
    out = shape(s)
    changed = out != s
    print(f"IN : {s}")
    print(f"    logical: {cps(s)}")
    print(f"OUT: {out}   {'<-- unchanged (expected)' if not changed else ''}")
    if changed:
        print(f"    shaped : {cps(out)}")
    print()

# Structural checks
print("=== CHECKS ===")
assert shape("Hello World") == "Hello World", "English must pass through"
assert shape("Level 5") == "Level 5", "English+digits must pass through"
assert shape("لا") == chr(0xFEFB), "isolated lam-alef ligature"
assert "15" in shape("المستوى 15"), "digit run 15 must be preserved in order"
assert "5" in shape("المستوى 5"), "digit 5 preserved"
assert chr(0x064E) in shape("لَا"), "fatha preserved through lam-alef"
assert "١٥" in shape("المستوى ١٥"), "Arabic-Indic digit run preserved"

# Rich-text tag awareness: tags stay wrapped around their (reversed) content
color_out = shape("<color=red>مرحبا</color>")
expected_color = "<color=red>" + "".join(chr(c) for c in [0xFE8E, 0xFE92, 0xFEA3, 0xFEAE, 0xFEE3]) + "</color>"
assert color_out == expected_color, "Arabic inside a color tag is shaped + reversed"
assert color_out.startswith("<color=red>") and color_out.endswith("</color>"), "color tag span preserved"

bold_out = shape("<b>مستوى</b> 15")
assert bold_out.startswith("15 <b>") and bold_out.endswith("</b>"), "tag span moves to the RTL end; digits first"
assert bold_out.count("<b>") == 1 and bold_out.count("</b>") == 1, "tags not duplicated"

sprite_out = shape("<sprite=0>مرحبا")
assert sprite_out.endswith("<sprite=0>"), "void sprite tag stays atomic and moves to the RTL end"

# Tags wrapping Latin content: content must stay in order inside the span
latin_tag_out = shape("المستوى <b>Level 15</b>")
assert "<b>Level 15</b>" in latin_tag_out, "Latin content inside a tag is not scrambled"
print("all assertions passed ✓")
