import { mkdir } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

async function loadPlaywright() {
  try {
    return await import("playwright");
  } catch (error) {
    if (error?.code !== "ERR_MODULE_NOT_FOUND") {
      throw error;
    }

    return await import(pathToFileURL("C:/tmp/lyrics-debug-tools/node_modules/playwright/index.mjs").href);
  }
}

const { chromium } = await loadPlaywright();

const root = process.cwd();
const outDir = path.join(root, "artifacts", "lyrics-animation-capture");
const pagePath = path.join(root, "TaskbarLyrics.App", "Web", "Lyrics", "debug.html");
const scenario = process.argv[2] ?? "same-timestamp";

const scenarios = {
  normal: "runNormalSwitch",
  "same-timestamp": "runSameTimestampSwitch",
  rapid: "runRapidSwitch"
};

const fnName = scenarios[scenario] ?? scenarios["same-timestamp"];

await mkdir(outDir, { recursive: true });

const browser = await chromium.launch({
  headless: true
});

try {
  const page = await browser.newPage({
    viewport: { width: 520, height: 190 },
    deviceScaleFactor: 2
  });

  await page.goto(pathToFileURL(pagePath).href);
  await page.waitForFunction(() => window.debugLyrics && window.taskbarLyrics);

  await page.evaluate((name) => {
    void window.debugLyrics[name]();
  }, fnName);

  const stage = page.locator("#debugStage");
  const timeline = [0, 120, 280, 560, 900, 1180, 1500, 1850, 2200];
  const start = Date.now();
  const frames = [];

  for (const at of timeline) {
    const wait = Math.max(0, at - (Date.now() - start));
    if (wait > 0) {
      await page.waitForTimeout(wait);
    }

    const snapshot = await page.evaluate(() => window.debugLyrics.snapshot());
    const fileName = `${String(at).padStart(4, "0")}ms-${scenario}.png`;
    const filePath = path.join(outDir, fileName);
    await stage.screenshot({ path: filePath });
    frames.push({ at, file: filePath, snapshot });
  }

  console.log(JSON.stringify({ scenario, outDir, frames }, null, 2));
} finally {
  await browser.close();
}
