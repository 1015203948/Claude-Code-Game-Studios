---
name: localize
description: "Run the localization workflow: extract strings, validate localization readiness, check for hardcoded text, and generate translation-ready string tables."
argument-hint: "[scan|extract|validate|status]"
user-invocable: true
agent: localization-lead
allowed-tools: Read, Glob, Grep, Write, Bash
---

## Phase 1: Parse Subcommand

Determine the mode from the argument:

- `scan` — Scan for localization issues (hardcoded strings, missing keys)
- `extract` — Extract new strings and generate/update string tables
- `validate` — Validate existing translations for completeness and format
- `status` — Report overall localization status

If no subcommand is provided, output usage and stop. Verdict: **FAIL** — missing required subcommand.

---

## Phase 2A: Scan Mode

Search `src/` for hardcoded user-facing strings:

- String literals in UI code not wrapped in a localization function
- Concatenated strings that should be parameterized
- Strings with positional placeholders (`%s`, `%d`) instead of named ones (`{playerName}`)

Search for localization anti-patterns:

- Date/time formatting not using locale-aware functions
- Number formatting without locale awareness
- Text embedded in images or textures (flag asset files)
- Strings that assume left-to-right text direction

Report all findings with file paths and line numbers. This mode is read-only — no files are written.

---

## Phase 2B: Extract Mode

- Scan all source files for localized string references
- Compare against the existing string table (if any) in `assets/data/`
- Generate new entries for strings that don't have keys yet
- Suggest key names following the convention: `[category].[subcategory].[description]`
- Output a diff of new strings to add to the string table

Present the diff to the user. Ask: "May I write these new entries to `assets/data/strings/strings-[locale].json`?"

If yes, write only the diff (new entries), not a full replacement. Verdict: **COMPLETE** — strings extracted and written.

If no, stop here. Verdict: **BLOCKED** — user declined write.

---

## Phase 2C: Validate Mode

- Read all string table files in `assets/data/`
- Check each entry for:
  - Missing translations (key exists but no translation for a locale)
  - Placeholder mismatches (source has `{name}` but translation is missing it)
  - String length violations (exceeds character limits for UI elements)
  - Orphaned keys (translation exists but nothing references the key in code)
- Report validation results grouped by locale and severity. This mode is read-only — no files are written.

---

## Phase 2D: Status Mode

- Count total localizable strings
- Per locale: count translated, untranslated, and stale (source changed since translation)
- Generate a coverage matrix:

```markdown
## Localization Status
Generated: [Date]

| Locale | Total | Translated | Missing | Stale | Coverage |
|--------|-------|-----------|---------|-------|----------|
| en (source) | [N] | [N] | 0 | 0 | 100% |
| [locale] | [N] | [N] | [N] | [N] | [X]% |

### Issues
- [N] hardcoded strings found in source code
- [N] strings exceeding character limits
- [N] placeholder mismatches
- [N] orphaned keys (can be cleaned up)
```

This mode is read-only — no files are written.

---

## Phase 3: Next Steps

- If scan found hardcoded strings: run `/localize extract` to begin extracting them.
- If validate found missing translations: share the report with the translation team.
- If approaching launch: run `/asset-audit` to verify all localized assets are present.

### Rules
- English (en) is always the source locale
- Every string table entry must include a translator comment explaining context
- Never modify translation files directly — generate diffs for review
- Character limits must be defined per-UI-element and enforced automatically
- Right-to-left (RTL) language support should be considered from the start, not bolted on later
