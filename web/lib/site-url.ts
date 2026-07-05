const DEFAULT_SITE_URL = "https://citymajor.apps.softblaze.net";

/** Canonical origin for sitemap, robots, and absolute metadata URLs. */
export function getSiteUrl(): string {
  const fromEnv = process.env.NEXT_PUBLIC_SITE_URL?.trim();
  if (fromEnv) return fromEnv.replace(/\/$/, "");
  return DEFAULT_SITE_URL;
}
