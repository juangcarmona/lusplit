# LuSplit Website

Public product website for LuSplit. Built with [Astro](https://astro.build) - static, multilingual, and minimal.

---

## Getting started

```sh
# Install dependencies
npm install

# Run locally
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview
```

The dev server starts at `http://localhost:4321`.

---

## Project structure

```text
website/
├── public/                  Static assets (logo, favicon)
├── src/
│   ├── components/          Reusable Astro components
│   ├── content/
│   │   ├── pages/           Markdown content, one folder per language
│   │   │   ├── en/          English (canonical)
│   │   │   ├── es/
│   │   │   ├── fr/
│   │   │   ├── de/
│   │   │   ├── pt/
│   │   │   └── it/
│   │   └── ui/              UI string translations (JSON), one file per language
│   │       ├── en.json
│   │       ├── es.json
│   │       └── ...
│   ├── layouts/
│   │   └── BaseLayout.astro Base HTML layout (metadata, header, footer, theme)
│   ├── lib/
│   │   └── i18n.ts          Language helpers and content loading with fallback
│   ├── pages/
│   │   ├── index.astro      Root redirect → /en
│   │   └── [lang]/
│   │       ├── index.astro  Homepage per language
│   │       └── [page].astro Content pages per language
│   └── styles/
│       └── global.css       Design tokens and global styles
├── astro.config.mjs
├── package.json
└── tsconfig.json
```

---

## Content organisation

### Pages (Markdown)

Each page is a `.md` file under `src/content/pages/{lang}/{slug}.md`.

Available slugs:

| Slug            | Description            |
|-----------------|------------------------|
| `home`          | Homepage               |
| `features`      | Feature list           |
| `how-it-works`  | Usage walkthrough      |
| `privacy`       | Privacy information    |
| `support`       | Help and contributions |

English (`en/`) is the canonical source. All slugs must exist in English.

Other languages may provide partial translations. Pages with no translation fall back to English content automatically (see below).

### UI strings (JSON)

UI strings live in `src/content/ui/{lang}.json`. These cover navigation labels, button text, hero copy, footer text, and the fallback notice. `en.json` is the canonical reference.

---

## Language fallback

If a requested page does not exist in the selected language, the site renders the English content instead. The URL stays on the localized route (e.g. `/pt/features`). A small notice banner is shown at the top of the page informing the reader that English content is being displayed.

This fallback is handled in `src/lib/i18n.ts` by `getPageContent(lang, slug)`.

---

## Adding a translated page

1. Create `src/content/pages/{lang}/{slug}.md` with the same frontmatter fields:

   ```yaml
   ---
   title: "Page title in target language"
   description: "Meta description in target language"
   ---
   ```

2. Write the translated body content in Markdown below the frontmatter.
3. The route `/{lang}/{slug}` will now serve the translated content without a fallback notice.

---

## Adding a new language

1. Add the language code to `SUPPORTED_LANGS` in `src/lib/i18n.ts`.
2. Add its display name to `LANG_NAMES` in the same file.
3. Create `src/content/ui/{lang}.json` (copy `en.json` as a starting point and translate the values).
4. Optionally add translated page files under `src/content/pages/{lang}/`.

Pages without a translation fall back to English automatically.

---

## Theming

The site supports light and dark mode.

- System preference is respected on first visit.
- The user can toggle manually using the sun/moon button in the header.
- The choice is persisted to `localStorage` under the key `lusplit-theme`.
- Theme initialisation runs inline before first paint to avoid flash.

CSS design tokens are in `src/styles/global.css` under `:root` (light) and `html.dark` (dark).

---

## Brand

Primary colours: `#2CB1A1` (teal) and `#5A74FF` (indigo).
Logo files: `public/lusplit-logo.svg` and `public/lusplit-logo.png`.
Font: Nunito (Google Fonts), falling back to system sans-serif.

See `docs/brand/` in the repository root for full brand guidelines.

> 🧑‍🚀 **Seasoned astronaut?** Delete this file. Have fun!

## 🚀 Project Structure

Inside of your Astro project, you'll see the following folders and files:

```text
/
├── public/
├── src/
│   └── pages/
│       └── index.astro
└── package.json
```

Astro looks for `.astro` or `.md` files in the `src/pages/` directory. Each page is exposed as a route based on its file name.

There's nothing special about `src/components/`, but that's where we like to put any Astro/React/Vue/Svelte/Preact components.

Any static assets, like images, can be placed in the `public/` directory.

## 🧞 Commands

All commands are run from the root of the project, from a terminal:

| Command                   | Action                                           |
| :------------------------ | :----------------------------------------------- |
| `npm install`             | Installs dependencies                            |
| `npm run dev`             | Starts local dev server at `localhost:4321`      |
| `npm run build`           | Build your production site to `./dist/`          |
| `npm run preview`         | Preview your build locally, before deploying     |
| `npm run astro ...`       | Run CLI commands like `astro add`, `astro check` |
| `npm run astro -- --help` | Get help using the Astro CLI                     |

## 👀 Want to learn more?

Feel free to check [our documentation](https://docs.astro.build) or jump into our [Discord server](https://astro.build/chat).
