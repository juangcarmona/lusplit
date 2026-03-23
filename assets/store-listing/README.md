# Store Listing — Google Play Localization Assets

This folder contains Google Play Store listing localization assets for LuSplit.

## Structure

```
/assets/store-listing
  /{lang}
    descriptions.json        # Short and full descriptions for the Play Store
    /screenshots
      /mobile                # Phone screenshots (drop PNG files here)
      /tablet                # Tablet screenshots (drop PNG files here)
```

## Languages

| Folder  | Language              |
|---------|-----------------------|
| `en`    | English (source)      |
| `es`    | Spanish               |
| `fr`    | French                |
| `de`    | German                |
| `it`    | Italian               |
| `pt`    | Portuguese            |
| `ja`    | Japanese              |
| `ko`    | Korean                |
| `zh-CN` | Chinese (Simplified)  |
| `zh-TW` | Chinese (Traditional) |
| `hi`    | Hindi                 |
| `id`    | Indonesian            |
| `tr`    | Turkish               |
| `ru`    | Russian               |
| `ar`    | Arabic                |

## descriptions.json

Each language folder contains a `descriptions.json` file with this exact shape:

```json
{
  "shortDescription": "...",
  "fullDescription": "..."
}
```

- `shortDescription` — Maps to the Google Play short description field (≤ 80 characters).
- `fullDescription` — Maps to the Google Play full description field (≤ 4000 characters).
- `appName` is not included here. The app name is fixed as **LuSplit**.

**English (`en`) is the canonical source.** All other languages are derived from it and must stay
consistent with the English text in terminology, tone, and structure.

## Screenshots

Screenshot files should be dropped directly into the appropriate language subfolder:

- `screenshots/mobile/` — Phone screenshots
- `screenshots/tablet/` — Tablet screenshots

Each folder contains a `.gitkeep` file to preserve the empty directory in version control.
Remove it once real screenshots are added (or leave it — it causes no harm).

Naming screenshots with a numeric prefix keeps them ordered predictably:

```
01_groups.png
02_add_expense.png
03_balances.png
04_settle_up.png
```

## Updating copy

When the English source text changes:

1. Update `en/descriptions.json` first.
2. Update all other language files to reflect the change.
3. Keep terminology consistent with the website translations in `website/src/content/ui/`.
