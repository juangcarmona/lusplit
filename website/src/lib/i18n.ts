export const SUPPORTED_LANGS = ['en', 'es', 'fr', 'de', 'pt', 'it'] as const;
export type Lang = typeof SUPPORTED_LANGS[number];

export const DEFAULT_LANG: Lang = 'en';

export const PAGES = ['features', 'how-it-works', 'privacy', 'support'] as const;
export type PageSlug = typeof PAGES[number];

export const LANG_NAMES: Record<Lang, string> = {
  en: 'English',
  es: 'Español',
  fr: 'Français',
  de: 'Deutsch',
  pt: 'Português',
  it: 'Italiano',
};

export interface PageFrontmatter {
  title: string;
  description: string;
  [key: string]: unknown;
}

export type UiStrings = Record<string, string>;

// Resolved at build time by Vite - paths are relative to this file (src/lib/)
const mdModules = import.meta.glob<{
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  default: any;
  frontmatter: PageFrontmatter;
}>('../content/pages/**/*.md', { eager: true });

const uiModules = import.meta.glob<{ default: UiStrings }>(
  '../content/ui/*.json',
  { eager: true },
);

export function getPageContent(
  lang: Lang,
  slug: string,
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
): { Content: any; frontmatter: PageFrontmatter; isFallback: boolean } {
  const key = `../content/pages/${lang}/${slug}.md`;
  const enKey = `../content/pages/en/${slug}.md`;

  if (mdModules[key]) {
    const mod = mdModules[key];
    return { Content: mod.default, frontmatter: mod.frontmatter, isFallback: false };
  }
  if (mdModules[enKey]) {
    const mod = mdModules[enKey];
    return { Content: mod.default, frontmatter: mod.frontmatter, isFallback: true };
  }
  throw new Error(`Content not found for slug "${slug}" in lang "${lang}" or "en"`);
}

export function getUiStrings(lang: Lang): UiStrings {
  const key = `../content/ui/${lang}.json`;
  const enKey = `../content/ui/en.json`;
  const mod = uiModules[key] ?? uiModules[enKey];
  return mod?.default ?? {};
}

export function isValidLang(value: unknown): value is Lang {
  return SUPPORTED_LANGS.includes(value as Lang);
}

export function localizedHref(lang: Lang, path: string): string {
  // For home, return /{lang} (no trailing slash)
  if (path === '/' || path === '') return `/${lang}`;
  return `/${lang}${path.startsWith('/') ? path : `/${path}`}`;
}
