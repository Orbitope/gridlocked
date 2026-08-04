#!/usr/bin/env node
// Capture promo assets straight from the published article sources in docs/.
// Nothing here redraws a figure: every image is the article's own rendered SVG,
// screenshotted headlessly. Run: node capture_promo.mjs [project ...]
//
// Requires: playwright + Chromium (PLAYWRIGHT_BROWSERS_PATH), ffmpeg.

import { chromium } from 'playwright';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const HOME = '/home/user';
// Playwright's bundled ffmpeg is VP8/WebM-only; imageio-ffmpeg's build has libx264 + gif,
// which is what X and Reddit actually accept.  pip install imageio-ffmpeg
const FFMPEG = process.env.PROMO_FFMPEG
  || '/usr/local/lib/python3.11/dist-packages/imageio_ffmpeg/binaries/ffmpeg-linux-x86_64-v7.0.2';
const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json',
  '.css': 'text/css', '.png': 'image/png', '.svg': 'image/svg+xml', '.ttf': 'font/ttf' };

// Kill scroll-reveal animations so figures are captured fully opaque.
const UNVEIL = `
  .reveal,.fig,figure,[class*=reveal]{opacity:1!important;transform:none!important;visibility:visible!important}
  *{transition:none!important;animation:none!important}
  ::-webkit-scrollbar{width:0!important;height:0!important}
`;

/* ------------------------------------------------------------------ config */
// still: { id, out, wrap?, dsf?, before?(page) }   wrap:true → capture the enclosing <figure>/.fig
// clip:  { out, el, wrap?, frames, fps?, dsf?, setup?(page), step?(page,i) }

const PROJECTS = {
  gridlocked: {
    dir: 'gridlocked/docs', page: 'index.html',
    stills: [
      { id: 'b_big',   out: 'size-liar-big' },
      { id: 'b_small', out: 'size-liar-small' },
      { id: 'scat',    out: 'scatter-size',  wrap: true },
      { id: 'scat',    out: 'scatter-depth', wrap: true,
        before: async p => { await p.click('.toggle-row button[data-x="d"]'); } },
      { id: 'dist',    out: 'size-distribution', wrap: true },
      { id: 'dens',    out: 'hex-density-correlation', wrap: true },
      { id: 'b_hex',   out: 'hex-board' },
      { id: 'b_shal',  out: 'depth-shallow' },
      { id: 'b_deep',  out: 'depth-deep' },
    ],
    clips: [
      { out: 'wrong-way-82pct', el: '#b_ci', wrap: true, frames: 12, fps: 1.2,
        setup: async p => { const r = await p.$('#ci_reset'); if (r) await r.click(); },
        step:  async p => { const s = await p.$('#ci_step'); if (s) await s.click(); } },
      { out: 'dependency-depth-step', el: '#b_dep1', wrap: true, frames: 8, fps: 1.2,
        setup: async p => { const r = await p.$('#dep_reset'); if (r) await r.click(); },
        step:  async p => { const s = await p.$('#dep_step'); if (s) await s.click(); } },
      { out: 'three-axes', el: '#b_axes', wrap: true, frames: 3, fps: 0.8,
        step: async p => { const s = await p.$('#ax_next'); if (s) await s.click(); } },
    ],
  },

  hextruchet: {
    dir: 'hextruchet/docs', page: 'index.html',
    stills: [
      { id: 'dboardU',  out: 'deck-uniform' },
      { id: 'dboardT',  out: 'deck-tuned' },
      { id: 'fig-deck', out: 'deck-comparison-full' },
      { id: 'tilesrow', out: 'five-tiles' },
      { id: 'fig-hist', out: 'loop-size-histogram' },
      { id: 'fig-sweep',out: 'bot-sweep' },
      { id: 'fig-slip', out: 'slip' },
      { id: 'gboard',   out: 'game-final', wrap: true,
        before: async p => { await p.$eval('#g-scrub', el => {
          el.value = el.max; el.dispatchEvent(new Event('input', { bubbles: true })); }); } },
    ],
    clips: [
      { out: 'game-37-turns', el: '#gboard', wrap: true, frames: 38, fps: 4,
        setup: async p => { await p.$eval('#g-scrub', el => {
          el.value = 0; el.dispatchEvent(new Event('input', { bubbles: true })); }); },
        step: async (p, i) => { await p.$eval('#g-scrub', (el, v) => {
          el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); }, i); } },
    ],
  },

  simulacrum: {
    dir: 'simulacrum/docs', page: 'index.html',
    stills: [
      { id: 'tp',     out: 'throughput-92x', wrap: true },
      { id: 'dep',    out: 'dependency', wrap: true },
      { id: 'b_heat', out: 'batch-independence-clean', wrap: true },
      { id: 'b_heat', out: 'batch-independence-bug', wrap: true,
        before: async p => { const b = await p.$('#b_bug'); if (b) await b.click(); } },
      { id: 'f_board', out: 'forager-board', wrap: true },
    ],
    clips: [
      { out: 'reference-vs-batched', el: '#d_ref', wrap: true, frames: 10, fps: 1.2,
        step: async p => { const b = await p.$('#d_fwd'); if (b) await b.click(); } },
      { out: 'jump-to-divergence', el: '#d_ref', wrap: true, frames: 6, fps: 1,
        setup: async p => { const b = await p.$('#d_jump'); if (b) await b.click(); },
        step:  async p => { const b = await p.$('#d_fwd'); if (b) await b.click(); } },
    ],
  },

  pushman: {
    dir: 'pushman/docs', page: 'index.html',
    stills: [
      { id: 'persOH',    out: 'personality-one-hot', wrap: true },
      { id: 'persBarsEl',out: 'personality-rewards', wrap: true },
      { id: 'aimSvg',    out: 'aiming-interactive', wrap: true },
    ],
    // the committed SVGs are already good art — just rasterize them
    svgs: ['combat-triangle', 'state-machine', 'reward-values-old-vs-new', 'aiming-dot-product',
           'obs-shape-mismatch', 'warm-start-local-optimum', 'observation-table', 'reward-table',
           'self-play-vs-round-robin', 'personality-reward-emphasis', 'reaction-lag-ghost',
           'personality-one-hot-matrix'],
    clips: [
      { out: 'reaction-lag', el: '#diffSvg', wrap: true, frames: 10, fps: 2,
        step: async (p, i) => { await p.$eval('#diffSlider', (el, v) => {
          const min = +el.min || 0, max = +el.max || 100;
          el.value = min + (max - min) * (v / 9);
          el.dispatchEvent(new Event('input', { bubbles: true })); }, i); } },
    ],
  },

  RLevator: {
    dir: 'RLevator/docs', page: 'index.html',
    viewport: { width: 1600, height: 900 }, dsf: 2,
    // scroll-driven: park on a scene section, shoot the sticky stage
    scenes: [
      { scene: 1, at: 0.15, out: 'observation-early' },
      { scene: 1, at: 0.75, out: 'observation-full' },
      { scene: 2, at: 0.5,  out: 'reward' },
      { scene: 3, at: 0.5,  out: 'architectures' },
      { scene: 4, at: 0.5,  out: 'reward-selector' },
      // the shaped→unshaped flip on the same building IS the finding — drive it, don't scroll to it
      { scene: 5, at: 0.5, out: 'results-M-shaped',   picks: ['M ·', 'Shaped'] },
      { scene: 5, at: 0.5, out: 'results-M-unshaped', picks: ['M ·', 'Unshaped'] },
      { scene: 5, at: 0.5, out: 'results-L-unshaped', picks: ['L ·', 'Unshaped'] },
      { scene: 5, at: 0.5, out: 'results-S-unshaped', picks: ['S ·', 'Unshaped'] },
      { scene: 5, at: 0.5, out: 'results-M-abandoned', picks: ['M ·', 'Unshaped', 'abandoned'] },
    ],
    sceneClips: [
      { scene: 1, out: 'what-the-agent-sees', frames: 11, fps: 1.5, from: 0.05, to: 0.95 },
      { scene: 5, out: 'results-flip',        frames: 10, fps: 1.5, from: 0.10, to: 0.95 },
    ],
  },
};

/* ------------------------------------------------------------------- utils */
function serve(root, port) {
  const srv = http.createServer((req, res) => {
    const rel = decodeURIComponent(req.url.split('?')[0]).replace(/^\/+/, '') || 'index.html';
    const file = path.join(root, rel);
    if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
      res.writeHead(404).end('nf'); return;
    }
    res.writeHead(200, { 'content-type': MIME[path.extname(file)] || 'application/octet-stream' });
    fs.createReadStream(file).pipe(res);
  });
  return new Promise(r => srv.listen(port, '127.0.0.1', () => r(srv)));
}

// PNG IHDR: width/height are big-endian u32 at bytes 16 and 20.
function pngSize(file) {
  const b = fs.readFileSync(file).subarray(0, 24);
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) };
}
// Resolve concrete even pixel dims so no ffmpeg filter expression needs escaping.
function fit(src, maxW) {
  const w = Math.min(src.w, maxW);
  const scale = w / src.w;
  const even = n => Math.max(2, Math.round(n / 2) * 2);
  return `${even(w)}:${even(src.h * scale)}`;
}

// A figure's caption can reflow between steps, so frames come out at slightly different
// sizes and ffmpeg's filter graph refuses them. Pad every frame up to the max, top-left
// anchored, in the page's own background colour.
function normalize(frameDir, bg) {
  const frames = fs.readdirSync(frameDir).filter(f => /^f\d+\.png$/.test(f)).sort();
  const dims = frames.map(f => pngSize(path.join(frameDir, f)));
  const W = Math.max(...dims.map(d => d.w)), H = Math.max(...dims.map(d => d.h));
  if (dims.every(d => d.w === W && d.h === H)) return frameDir;
  const normDir = path.join(frameDir, 'norm');
  fs.mkdirSync(normDir, { recursive: true });
  frames.forEach(f => execFileSync(FFMPEG, ['-y', '-loglevel', 'error',
    '-i', path.join(frameDir, f), '-vf', `pad=${W}:${H}:0:0:color=${bg}`,
    '-update', '1', path.join(normDir, f)]));
  return normDir;
}

function encode(frameDir, outBase, fps, bg = 'black') {
  frameDir = normalize(frameDir, bg);
  const pat = path.join(frameDir, 'f%04d.png');
  const src = pngSize(path.join(frameDir, 'f0000.png'));

  // MP4 — what X and Reddit prefer; capped at 1280 wide.
  execFileSync(FFMPEG, ['-y', '-loglevel', 'error', '-framerate', String(fps), '-i', pat,
    '-vf', `scale=${fit(src, 1280)}:flags=lanczos,format=yuv420p`,
    '-c:v', 'libx264', '-preset', 'slow', '-crf', '20', '-pix_fmt', 'yuv420p',
    '-movflags', '+faststart', `${outBase}.mp4`]);

  // GIF fallback — smaller, two-pass palette so the dark theme doesn't band.
  const gifScale = fit(src, 800);
  const pal = path.join(frameDir, 'pal.png');
  execFileSync(FFMPEG, ['-y', '-loglevel', 'error', '-framerate', String(fps), '-i', pat,
    '-vf', `scale=${gifScale}:flags=lanczos,palettegen=stats_mode=diff`, '-update', '1', pal]);
  execFileSync(FFMPEG, ['-y', '-loglevel', 'error', '-framerate', String(fps), '-i', pat,
    '-i', pal, '-lavfi',
    `[0:v]scale=${gifScale}:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3`,
    '-loop', '0', `${outBase}.gif`]);
}

async function shoot(page, sel, wrap, file) {
  const el = await page.$(sel);
  if (!el) return { ok: false, why: 'missing' };
  const target = wrap
    ? (await el.evaluateHandle(n => n.closest('figure,.fig,.wdg') || n)).asElement()
    : el;
  await target.scrollIntoViewIfNeeded().catch(() => {});
  await page.waitForTimeout(120);
  const box = await target.boundingBox();
  if (!box || box.width < 8 || box.height < 8) return { ok: false, why: 'zero-size' };
  await target.screenshot({ path: file });
  return { ok: true, w: Math.round(box.width), h: Math.round(box.height) };
}

function rgbToHex(css) {
  const m = /(\d+)\D+(\d+)\D+(\d+)/.exec(css || '');
  if (!m) return 'black';
  return '#' + m.slice(1, 4).map(n => (+n).toString(16).padStart(2, '0')).join('');
}

async function unveil(page) {
  await page.addStyleTag({ content: UNVEIL });
  await page.evaluate(async () => {
    const h = document.body.scrollHeight;
    for (let y = 0; y < h; y += 500) { window.scrollTo(0, y); await new Promise(r => setTimeout(r, 30)); }
    window.scrollTo(0, 0);
  });
  await page.waitForTimeout(800);
}

/* -------------------------------------------------------------------- main */
const only = process.argv.slice(2);
const names = only.length ? only : Object.keys(PROJECTS);
const report = [];

for (const [i, name] of names.entries()) {
  const cfg = PROJECTS[name];
  if (!cfg) { console.log(`?? unknown project ${name}`); continue; }
  const root = path.join(HOME, cfg.dir);
  const outDir = path.join(HOME, cfg.dir, 'promo', 'img');
  const tmpDir = path.join('/tmp/promo-frames', name);
  fs.mkdirSync(outDir, { recursive: true });
  fs.rmSync(tmpDir, { recursive: true, force: true });
  fs.mkdirSync(tmpDir, { recursive: true });

  const port = 8300 + i;
  const srv = await serve(root, port);
  const browser = await chromium.launch();
  const base = `http://127.0.0.1:${port}/${cfg.page}`;
  const vp = cfg.viewport || { width: 1600, height: 1000 };

  console.log(`\n=== ${name} ===`);

  // ---- stills
  for (const s of cfg.stills || []) {
    const page = await browser.newPage({ viewport: vp, deviceScaleFactor: s.dsf || cfg.dsf || 3 });
    await page.goto(base, { waitUntil: 'domcontentloaded' });
    await unveil(page);
    if (s.before) await s.before(page), await page.waitForTimeout(400);
    const file = path.join(outDir, `${s.out}.png`);
    const r = await shoot(page, '#' + s.id, s.wrap, file);
    console.log(`  still ${s.out.padEnd(30)} ${r.ok ? `${r.w}x${r.h}` : 'FAIL:' + r.why}`);
    report.push({ project: name, asset: `${s.out}.png`, ...r });
    await page.close();
  }

  // ---- committed SVGs → PNG
  for (const svg of cfg.svgs || []) {
    const page = await browser.newPage({ viewport: { width: 1400, height: 1000 }, deviceScaleFactor: 3 });
    await page.goto(`http://127.0.0.1:${port}/diagrams/${svg}.svg`, { waitUntil: 'networkidle' });
    const el = await page.$('svg');
    const file = path.join(outDir, `${svg}.png`);
    if (el) {
      const box = await el.boundingBox();
      await el.screenshot({ path: file });
      console.log(`  svg   ${svg.padEnd(30)} ${Math.round(box.width)}x${Math.round(box.height)}`);
      report.push({ project: name, asset: `${svg}.png`, ok: true, w: Math.round(box.width), h: Math.round(box.height) });
    } else {
      console.log(`  svg   ${svg.padEnd(30)} FAIL`);
      report.push({ project: name, asset: `${svg}.png`, ok: false, why: 'no svg root' });
    }
    await page.close();
  }

  // ---- clips
  for (const c of cfg.clips || []) {
    const page = await browser.newPage({ viewport: vp, deviceScaleFactor: c.dsf || 2 });
    await page.goto(base, { waitUntil: 'domcontentloaded' });
    await unveil(page);
    const fdir = path.join(tmpDir, c.out);
    fs.mkdirSync(fdir, { recursive: true });
    const bg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
    if (c.setup) await c.setup(page), await page.waitForTimeout(400);
    let ok = true;
    for (let f = 0; f < c.frames; f++) {
      if (f > 0 && c.step) { await c.step(page, f); await page.waitForTimeout(260); }
      const r = await shoot(page, c.el, c.wrap, path.join(fdir, `f${String(f).padStart(4, '0')}.png`));
      if (!r.ok) { ok = false; break; }
    }
    if (ok) {
      encode(fdir, path.join(outDir, c.out), c.fps || 2, rgbToHex(bg));
      const mp4 = fs.statSync(path.join(outDir, `${c.out}.mp4`)).size;
      const gif = fs.statSync(path.join(outDir, `${c.out}.gif`)).size;
      console.log(`  clip  ${c.out.padEnd(30)} ${c.frames}f  mp4 ${(mp4/1024|0)}KB  gif ${(gif/1024|0)}KB`);
      report.push({ project: name, asset: `${c.out}.mp4`, ok: true, frames: c.frames, mp4, gif });
    } else {
      console.log(`  clip  ${c.out.padEnd(30)} FAIL`);
      report.push({ project: name, asset: `${c.out}.mp4`, ok: false, why: 'frame capture failed' });
    }
    await page.close();
  }

  // ---- scroll-driven scenes (RLevator)
  if (cfg.scenes || cfg.sceneClips) {
    const page = await browser.newPage({ viewport: vp, deviceScaleFactor: cfg.dsf || 2 });
    await page.goto(base, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2500);
    const sceneBg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
    const park = async (scene, at) => {
      await page.evaluate(([s, f]) => {
        const el = document.querySelector(`[data-scene="${s}"]`);
        if (!el) return;
        const r = el.getBoundingClientRect();
        window.scrollTo(0, window.scrollY + r.top - window.innerHeight * 0.1 + r.height * f);
      }, [scene, at]);
      await page.waitForTimeout(900);
    };
    for (const s of cfg.scenes || []) {
      await park(s.scene, s.at);
      // The stage's own controls beat guessing a scroll offset — click them by label.
      for (const label of s.picks || []) {
        const hit = await page.evaluate(txt => {
          const stage = document.querySelector('.rlv-stage');
          const n = [...stage.querySelectorAll('*')].filter(e =>
            e.children.length === 0 && e.textContent.trim().startsWith(txt)).pop();
          if (!n) return false;
          (n.closest('button,[role=button],div') || n).click();
          return true;
        }, label);
        if (!hit) console.log(`    ! pick "${label}" not found for ${s.out}`);
        await page.waitForTimeout(700);
      }
      const file = path.join(outDir, `${s.out}.png`);
      const r = await shoot(page, '.rlv-stage > div', false, file);
      console.log(`  scene ${s.out.padEnd(30)} ${r.ok ? `${r.w}x${r.h}` : 'FAIL:' + r.why}`);
      report.push({ project: name, asset: `${s.out}.png`, ...r });
    }
    for (const c of cfg.sceneClips || []) {
      const fdir = path.join(tmpDir, c.out);
      fs.mkdirSync(fdir, { recursive: true });
      let ok = true;
      for (let f = 0; f < c.frames; f++) {
        await park(c.scene, c.from + (c.to - c.from) * (f / (c.frames - 1)));
        const r = await shoot(page, '.rlv-stage > div', false,
          path.join(fdir, `f${String(f).padStart(4, '0')}.png`));
        if (!r.ok) { ok = false; break; }
      }
      if (ok) {
        encode(fdir, path.join(outDir, c.out), c.fps || 1.5, rgbToHex(sceneBg));
        const mp4 = fs.statSync(path.join(outDir, `${c.out}.mp4`)).size;
        console.log(`  clip  ${c.out.padEnd(30)} ${c.frames}f  mp4 ${(mp4/1024|0)}KB`);
        report.push({ project: name, asset: `${c.out}.mp4`, ok: true, frames: c.frames, mp4 });
      } else {
        console.log(`  clip  ${c.out.padEnd(30)} FAIL`);
        report.push({ project: name, asset: `${c.out}.mp4`, ok: false, why: 'frame capture failed' });
      }
    }
    await page.close();
  }

  await browser.close();
  srv.close();
}

const bad = report.filter(r => !r.ok);
console.log(`\n${report.length - bad.length}/${report.length} assets captured` +
  (bad.length ? `\nfailed: ${bad.map(b => b.project + '/' + b.asset + ' (' + b.why + ')').join(', ')}` : ''));
fs.writeFileSync('/tmp/promo-frames/report.json', JSON.stringify(report, null, 2));
