import { mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const dist = resolve(root, "dist");

await mkdir(dist, { recursive: true });

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
    entryPoints: [resolve(root, "popup-react/main.tsx")],
    outfile: resolve(dist, "popup.js")
  })
]);
