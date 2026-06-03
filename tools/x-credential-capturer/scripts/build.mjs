import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { build } from "esbuild";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const dist = resolve(root, "dist");
const generated = resolve(root, "popup-react", "generated");
const buildInfoFile = resolve(generated, "build-info.ts");
const execFileAsync = promisify(execFile);

function formatBuildStamp(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  const hours = String(date.getHours()).padStart(2, "0");
  const minutes = String(date.getMinutes()).padStart(2, "0");
  const seconds = String(date.getSeconds()).padStart(2, "0");

  return `${year}${month}${day}-${hours}${minutes}${seconds}`;
}

async function tryReadManifestVersion() {
  try {
    const manifestPath = resolve(root, "manifest.json");
    const raw = await readFile(manifestPath, "utf8");
    const parsed = JSON.parse(raw);
    return typeof parsed.version === "string" && parsed.version.trim() ? parsed.version : "0.0.0";
  } catch {
    return "0.0.0";
  }
}

async function tryReadGitCommit() {
  try {
    const { stdout } = await execFileAsync("git", ["rev-parse", "--short", "HEAD"], {
      cwd: root
    });
    const value = stdout.trim();
    return value || null;
  } catch {
    return null;
  }
}

async function writeBuildInfo() {
  const now = new Date();
  const manifestVersion = await tryReadManifestVersion();
  const gitCommit = await tryReadGitCommit();
  const buildVersion = `${manifestVersion}+${formatBuildStamp(now)}${
    gitCommit ? `.${gitCommit}` : ""
  }`;
  const source = `export const buildInfo = ${JSON.stringify(
    {
      buildVersion,
      builtAt: now.toISOString(),
      gitCommit
    },
    null,
    2
  )} as const;\n`;

  await mkdir(generated, { recursive: true });
  await writeFile(buildInfoFile, source, "utf8");
}

await mkdir(dist, { recursive: true });
await writeBuildInfo();

const commonConfig = {
  bundle: true,
  format: "esm",
  platform: "browser",
  target: "es2022",
  sourcemap: false,
  logLevel: "info"
};

await Promise.all([
  build({
    ...commonConfig,
    entryPoints: [resolve(root, "background.ts")],
    outfile: resolve(dist, "background.js")
  }),
  build({
    ...commonConfig,
    entryPoints: [resolve(root, "content-script.ts")],
    outfile: resolve(dist, "content-script.js")
  }),
  build({
    ...commonConfig,
    entryPoints: [resolve(root, "inpage-bridge.ts")],
    outfile: resolve(dist, "inpage-bridge.js")
  }),
  build({
    ...commonConfig,
    entryPoints: [resolve(root, "popup-react/main.tsx")],
    outfile: resolve(dist, "popup.js")
  })
]);
