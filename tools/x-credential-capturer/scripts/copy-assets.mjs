import { cp, mkdir } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const dist = resolve(root, "dist");

const files = ["manifest.json", "popup.html", "popup.css", "README.md"];
for (const file of files) {
  await cp(resolve(root, file), resolve(dist, file));
}

await mkdir(resolve(dist, "styles"), { recursive: true });
await cp(resolve(root, "styles"), resolve(dist, "styles"), { recursive: true });
