/**
 * Pre-build sync script for Docusaurus.
 *
 * Reads sync-manifest.json and copies sub-project documentation files
 * into the Docusaurus content/ directory with:
 * - YAML frontmatter injection
 * - First H1 heading removal (Docusaurus uses frontmatter title)
 * - Relative link rewriting
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, '..');
const manifest = JSON.parse(readFileSync(resolve(ROOT, 'sync-manifest.json'), 'utf-8'));

function buildFrontmatter(fm) {
  const lines = ['---'];
  for (const [key, value] of Object.entries(fm)) {
    lines.push(`${key}: ${JSON.stringify(value)}`);
  }
  lines.push('---', '');
  return lines.join('\n');
}

function stripExistingFrontmatter(content) {
  return content.replace(/^---[\s\S]*?---\n*/, '');
}

function stripFirstH1(content) {
  return content.replace(/^#\s+.+\n+/, '');
}

function rewriteLinks(content, linkMap) {
  for (const [pattern, replacement] of Object.entries(linkMap)) {
    content = content.replaceAll(pattern, replacement);
  }
  return content;
}

let syncedCount = 0;
let skippedCount = 0;

for (const project of manifest.projects) {
  for (const source of project.sources) {
    const srcPath = resolve(ROOT, source.src);

    if (!existsSync(srcPath)) {
      console.warn(`[sync] SKIP: ${source.src} does not exist`);
      skippedCount++;
      continue;
    }

    let content = readFileSync(srcPath, 'utf-8');

    // Normalize line endings to LF
    content = content.replaceAll('\r\n', '\n');

    // Strip any existing frontmatter
    content = stripExistingFrontmatter(content);

    // Remove first H1 (Docusaurus uses frontmatter title)
    content = stripFirstH1(content);

    // Rewrite relative links
    if (source.linkRewrites) {
      content = rewriteLinks(content, source.linkRewrites);
    }

    // Inject Docusaurus frontmatter
    content = buildFrontmatter(source.frontmatter) + content;

    // Write to destination
    const destPath = resolve(ROOT, source.dest);
    mkdirSync(dirname(destPath), { recursive: true });
    writeFileSync(destPath, content, 'utf-8');

    console.log(`[sync] ${source.src} -> ${source.dest}`);
    syncedCount++;
  }
}

console.log(`[sync] Done: ${syncedCount} synced, ${skippedCount} skipped`);
