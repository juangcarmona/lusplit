/**
 * Generates the ProductShape product-definition snapshot HTML.
 *
 * Runs `prodshape graph --format html` from the repo root, then copies the
 * generated `.product/generated/snapshot.html` into
 * `website/public/product-snapshot.html` so Astro serves it as a standalone
 * static file at `/product-snapshot.html`.
 *
 * This script is wired as a `prebuild` / `predev` hook in package.json so the
 * snapshot is always fresh on every local dev session, CI build, and deploy.
 *
 * The snapshot is a self-contained HTML document (its own <!DOCTYPE html>,
 * <html>, <head>, <body>) — it is NOT wrapped in the Astro BaseLayout.
 */
import { execSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, "..", "..");

const snapshotSource = resolve(repoRoot, ".product", "generated", "snapshot.html");
const snapshotDest = resolve(__dirname, "..", "public", "product-snapshot.html");

console.log("▸ Generating ProductShape snapshot…");

try {
  execSync("npx --no-install prodshape graph --format html", {
    cwd: repoRoot,
    stdio: "inherit",
  });
} catch {
  console.error("✗ prodshape graph --format html failed");
  process.exit(1);
}

if (!existsSync(snapshotSource)) {
  console.error(`✗ Expected snapshot at ${snapshotSource} but it was not created`);
  process.exit(1);
}

mkdirSync(dirname(snapshotDest), { recursive: true });
copyFileSync(snapshotSource, snapshotDest);

console.log(`✓ Snapshot copied to ${snapshotDest}`);
