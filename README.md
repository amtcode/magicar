# Magicar — Arabic / RTL Text Shaping for Unity TextMeshPro

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
[![Unity](https://img.shields.io/badge/Unity-6%2B-black)](https://unity.com/)
[![UPM](https://img.shields.io/badge/install-UPM%20git%20URL-orange)](#installation)

**Arabic, Farsi, and Urdu text shaping and right-to-left (RTL) layout for Unity TextMeshPro.**

From `ه ي ب ر ع ل ا` to `العربيه` **(because Unity won't do it for you)**.

TextMeshPro gives you `isRightToLeftText`, but that just flips text—it doesn't actually shape Arabic characters. Magicar handles the contextual shaping, multi-line wraps, mixed languages, numbers, and rich-text tags, so your UI doesn't look like a ransom note. Unlike most Arabic shaping packages, it also **properly handles multi-line text**—each wrapped line is reversed independently, so your Arabic paragraphs actually read correctly instead of top-to-bottom backwards.

> **Status:** v0.1 — Production-tested, still a young API.

## Requirements

- Unity 6+
- TextMeshPro
- `com.unity.ugui`
- Zero runtime dependencies (as it should be).

## Installation

### Git
Magicar is a UPM package. 
**Window → Package Manager → + → Add package from git URL:**
`https://github.com/amtcode/magicar.git`

Or just shove it in your `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.amtcode.magicar": "https://github.com/amtcode/magicar.git"
  }
}
```
*(Append `#1.0.0` if you want a specific release).*

### Local
Dump the folder into `Packages/com.amtcode.magicar/`. Make sure `package.json` is at the root.

## Features

- **Contextual Shaping:** Automatically turns base letters into isolated, initial, medial, or final forms.
- **Lam-Alef Ligatures:** Handles `لا`, `لأ`, `لإ`, and `لآ` perfectly, keeping diacritics intact.
- **Mixed Content:** "المستوى 15" keeps `15` where it belongs.
- **Arabic-Indic Digits:** `٠–٩` won't scramble.
- **Multi-line RTL:** Wraps and reverses lines individually. Multi-line Arabic won't read from bottom to top.
- **Rich-text aware:** Tags like `<color=red>` or `<br>` are preserved and stay where they belong.
- **Safe for non-Arabic:** Latin strings just pass through, so you can safely feed it all your localized text without breaking English.

## Font Requirement: ⚠️ The Presentation-Form Trap

Magicar uses Unicode **presentation forms** (e.g., `U+FB50–U+FDFF`). 

If your font only has the basic Arabic block (`U+0600–U+06FF`), Magicar will shape the text flawlessly, and your font will just render a bunch of missing glyph squares (`□`).

Make sure your TMP Font Asset actually includes these presentation characters. 
*(Hint: Baloo Bhaijaan 2, Cairo, and Noto Kufi Arabic usually do).*

> **Bonus:** Instead of hunting down every single Text component and swapping its font, add the Arabic SDF as a **fallback** in your main TMP font asset. TMP will automatically pull glyphs from it when needed, and you keep your sanity intact.

## Usage

### 1. Set the active RTL locale
Hook this into your localization system:
```csharp
ArabicShaper.IsRtlLocale = locale == "ar" || locale == "fa" || locale == "ur";
```
Magicar doesn't care what localization framework you use, it just needs to know if RTL is currently active.

### 2. Shape a string (Single line)
```csharp
label.text = ArabicShaper.Shape(myText);
```

### 3. Apply to a TextMeshPro label (Multi-line)
For wrapping text, pass the component:
```csharp
ArabicShaper.ApplyToTmp(label, myText);
```

### 4. Force RTL shaping
To display "العربية" on an English UI screen:
```csharp
ArabicShaper.ApplyToTmp(rowLabel, "العربية", forceRtlShaping: true);
```

## API

### `ArabicShaper.IsRtlLocale`
`public static bool IsRtlLocale { get; set; }`
Tells `ApplyToTmp` whether to actually do its job. Update this when the language changes.

### `ArabicShaper.Shape`
`public static string Shape(string text);`
Returns the shaped string.

### `ArabicShaper.ApplyToTmp`
`public static string ApplyToTmp(TMP_Text text, string value, bool forceRtlShaping = false);`
Shapes, wraps, and assigns text to a TMP component. Returns the final string assigned, in case you still need to process rich text further.

## How It Works

1. **Shape:** Contextual presentation forms.
2. **Process:** Maintain mixed-script / digit / tag order.
3. **Reverse:** RTL layout applied per horizontal line.
4. **Assign:** Hand it to TextMeshPro.

## Rich Text

Formatting stays wrapped around the text: `<color=red>مرحبا</color>`. 
Void tags (`<sprite=0>`, `<br>`) are treated as single units.

*Known limitation:* If TMP wraps a line exactly in the middle of a rich-text tag, that slice might get treated as plain text and render the literal tag. It's rare, but physics dictates it will happen eventually.

## Validation

Need to verify the C# algorithm? There's a Python mirror.

```bash
python3 Tests~/arabic_mirror_test.py
```
It validates ligatures, wrapping, digits, and tags. If you touch the C# shaping logic, update the Python script too.

## Project Structure

Everything important is in `Runtime/`. 
`Tests~/` uses Unity's tilde convention so it doesn't pollute your project, though Git still tracks it.

## Design Decisions

- **No localization framework lock-in:** Keep `IsRtlLocale` updated, and Magicar handles the rest.
- **Global API:** No namespace (yet) because you mostly just want your text to work without extra `using` statements.

## Roadmap

- [ ] Add a namespace and formally review API
- [ ] Automated TMP integration tests
- [ ] NUnit tests
- [ ] Diacritic reordering 
- [ ] Tagged UPM releases

## License & Contributing

MIT License. See `LICENSE.md`.

Found a bug where text looks wrong? Please include:
1. Original string
2. Actual output
3. Expected output
4. Unity & TMP version
5. Font details

Because we really can't fix a text-shaping bug if we don't know what text you were shaping.

---

⭐ Don't forget to star the repo so other fellow developers know this exists!
