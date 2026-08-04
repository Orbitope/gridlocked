---
name: article-promo-kit
description: Generate platform-specific promotional posts (Twitter/X thread, Reddit posts, Hacker News post) for a new Orbitope article, plus a chart image, using only tools already on the system (curl, the default browser, macOS screencapture). No pip installs required. Use when asked to "promote", "post about", "announce", or "make a promo kit" for an Orbitope article or project update.
---

# Article Promo Kit (zero-dependency)

Everything here runs on tools already on the machine. No libraries to install.

## 1. Get the article

- **Inside the Orbitope repo:** start the local dev server if it's not running —
  check for `Gemfile` (Jekyll: `bundle exec jekyll serve --port 4000 &`) or
  `package.json` (Node: `npm run dev &`) — then `curl http://localhost:4000/<article>/`.
  Prefer this over the deployed site when available: it's the only way to see
  JS-rendered charts, not just static HTML.
- **Otherwise:** `curl https://orbitope.github.io/<article>/`, or fetch whatever
  URL was given directly.
- **Given only a project name:** `curl https://orbitope.github.io` first to find
  the article link.

## 2. Extract the hook

Pull out: the core number/claim, the mechanism (the "how"), domain tags (RL,
procgen, search/algorithms, Unity, Godot, PyTorch — these drive subreddit
choice), and whether a playable/runnable link exists vs. code-only.

## 3. Draft platform-specific copy

**Twitter/X** — casual, first-person, build-in-public tone. Lead with the hook.
Thread (2-4 tweets) if there's a real "how" to walk through. Link → article.

**Reddit** — title states the finding plainly, no "I made X" framing. Body:
2-3 sentences max. Link → article in the post body; repo link goes in a
top-level comment, not the post, so it doesn't read as an ad. Pick at most 2
subreddits from this table:

| Domain tag | Primary subreddit | Secondary (only if it fits) |
|---|---|---|
| RL / ML training | r/reinforcementlearning | r/MachineLearning (only for a genuinely novel finding) |
| Procedural generation | r/proceduralgeneration | — |
| Puzzle / search algorithms | r/algorithms | r/puzzles (if playable) |
| Unity-specific dev | r/Unity3D | — |
| Godot-specific dev | r/godot | — |
| Playable in browser | r/WebGames | — |
| Tabletop game design | r/tabletopgamedesign | r/boardgames (playtester recruiting only) |

**Hacker News** — `Show HN: <what it does> – <the number/claim>`. No image.
Link → repo directly if public, otherwise the article. Little to no body text.

Keep the voice direct and honest about limitations — post-mortem framing
("how not to...", "three ways to fail...") is a strength, not something to
polish away.

## 4. Get an image — browser + screenshot, nothing else

**Preferred: capture the real chart from the page.**
1. `open <url>` — the local dev server URL if you have one, so JS-rendered
   charts actually show up.
2. Scroll to the chart nearest your hook.
3. In the terminal: `screencapture -i chart.png`, then click-drag a box around
   just the chart in the browser window. Done — clean PNG, zero setup.

**Fallback: hand-write a plain SVG, then screenshot that the same way.**
Use this only if there's no usable chart on the page for this particular hook.
SVG is just text, no library needed — write a file like:

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="600" height="400">
  <rect width="600" height="400" fill="#faf6ef"/>
  <rect x="80" y="300" width="120" height="20" fill="#e9d3ab"/>
  <rect x="320" y="80" width="120" height="240" fill="#e2725b"/>
  <text x="80" y="340" font-family="sans-serif" font-size="16" fill="#2b2b2b">Baseline</text>
  <text x="320" y="340" font-family="sans-serif" font-size="16" fill="#2b2b2b">Result</text>
  <text x="320" y="60" font-family="sans-serif" font-size="22" font-weight="bold" fill="#2b2b2b">92x</text>
</svg>
```

Adjust the bar heights/labels/number to the actual hook, save it, then
`open chart.svg` → `screencapture -i chart.png` to capture it as a PNG.

## 5. Present the output

For each platform: the copy (ready to paste), which image to attach, and
which link goes where (post body vs. comment). No extra commentary — just
the finished kit.

## Notes

- Never post automatically. Reddit and HN both penalize anything that reads
  as automated — draft only, a human submits.
- If a project doesn't cleanly map to any subreddit, say so and recommend
  Twitter/HN only rather than forcing a bad fit.
