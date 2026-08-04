# Gridlocked — promo kit

**Article:** *A Bigger Maze Isn't a Harder Maze*
**Slot:** Week 1 — Reddit only. X already went out; nothing further planned there.

## Links

| Where | URL | Status |
|---|---|---|
| Article | `https://orbitope.github.io/gridlocked/` | ⚠️ **unverified** — inferred from the confirmed `orbitope.github.io/simulacrum/` pattern. Open it before posting. |
| Playable | `https://orbitope.itch.io/gridlocked` | confirmed (linked from the article outro) |
| Repo | `https://github.com/orbitope/gridlocked` | goes in a **comment**, never the post body |

## The hook

14,163 sliding-block puzzles solved exhaustively. Puzzle size predicts difficulty at **ρ = 0.03**;
dependency depth — what has to move before what — predicts it at **ρ = 0.88** on the same 550-puzzle
sample. A 422-position board takes 17 moves; an 11,487-position board takes 6. In hard puzzles **82%
of the optimal moves make the position look worse.**

---

## Reddit — r/WebGames · Thu 6 Aug

**Title**

```text
[Puzzle] Gridlocked — a sliding-block puzzle where the biggest boards are the easy ones
```

**Body**

```text
Rush Hour-style: slide the cars, get the red one out. I solved 14,163 boards exhaustively
to find out what actually makes one hard, and it isn't size — a 422-position board takes 17
moves while an 11,487-position board takes 6. The generator uses that to build the levels,
so the small cramped ones are the nasty ones. Free, browser, no install.
```

**Link:** the **itch build**, not the article. r/WebGames exists to be handed something playable;
a link to an essay reads as a bait-and-switch there. This is the one place in the whole plan where
the article isn't the post target.

**Image:** `img/wrong-way-82pct.mp4` (GIF fallback `img/wrong-way-82pct.gif`) — a real puzzle solving
itself with the distance-to-exit meter climbing while it goes the "wrong" way. Motion in the feed.

**Top-level comment**

```text
Write-up on what makes these hard, with every board on the page generated from the solver:
https://orbitope.github.io/gridlocked/

Engine's here if anyone wants it — bitboard solver, whole 6x6 state packed into a ulong,
full state-space enumeration in under 5ms: https://github.com/orbitope/gridlocked
```

---

## Reddit — r/puzzles · Sat 8 Aug

⚠️ **Check the sidebar first.** r/puzzles skews toward "help me solve this" and physical puzzles;
some weeks a game link is fine, some weeks it's removed on sight. If the rules read hostile, post to
**r/puzzlevideogames** instead with the same copy — it's a cleaner fit for a playable puzzle game and
was the better target all along.

**Title**

```text
The size of a sliding-block puzzle tells you almost nothing about how hard it is — 14,163 solved boards
```

**Body**

```text
I enumerated the full reachable state space for every puzzle in the set, so "difficulty" here is
exact: the fewest moves a perfect solver needs. Size and difficulty turn out to be statistically
unrelated (ρ = 0.03). What does predict it is dependency depth — how long the chain of "this car
has to move before that one" gets — at ρ = 0.88 on the same puzzles.
```

**Link:** the article.

**Image:** `img/scatter-depth.png` — the same 550 puzzles, one axis swapped, cloud snapping into a
line. Pair it with `img/scatter-size.png` if the sub allows a gallery; the before/after is the whole
argument.

**Top-level comment**

```text
Playable if you want to feel the difference: https://orbitope.itch.io/gridlocked
Solver and generator: https://github.com/orbitope/gridlocked
```

---

## X

Nothing scheduled — the Gridlocked post already went out.

If you ever want a second bite, the unused angle is the hex board: nobody appears to have run Rush
Hour on a hex grid before, and the third axis *drains* the difficulty (hex solves in 4.3–7.5 moves
on average against square's 12.2). Assets are ready: `img/three-axes.mp4`, `img/hex-board.png`,
`img/hex-density-correlation.png`.

---

## Asset index — `docs/promo/img/`

All captured from `docs/index.html` itself by `scripts/capture_promo.mjs`; nothing is redrawn.

| File | What it is |
|---|---|
| `scatter-size.png` / `scatter-depth.png` | the ρ = 0.03 → ρ = 0.88 axis flip. The strongest pair here. |
| `size-liar-big.png` / `size-liar-small.png` | 11,487 positions / 6 moves vs 422 positions / 17 moves |
| `size-distribution.png` | size spans 4.5 orders of magnitude, difficulty line stays flat |
| `hex-density-correlation.png` | depth predicts better as hex density rises — 0.66 → 0.76 → 0.83 |
| `wrong-way-82pct.mp4` / `.gif` | the 82%-of-moves-look-worse walkthrough |
| `dependency-depth-step.mp4` / `.gif` | two 6-move puzzles stepped together: three short hand-offs vs one 6-car chain |
| `three-axes.mp4` / `.gif` | a piece's 2 directions on square vs 3 on hex |
| `hex-board.png`, `depth-shallow.png`, `depth-deep.png` | supporting boards |

---

## Appendix — the five-week calendar

Warm-up **Wed 5 – Thu 6 Aug**: ordinary commenting, no links, in r/WebGames and r/puzzles. Keep
low-level commenting going in each week's target subs throughout — with a new account this matters
more than any single post.

| Week | Reddit #1 | Reddit #2 | X |
|---|---|---|---|
| 1 | Thu 6 Aug — **Gridlocked** → r/WebGames | Sat 8 Aug — r/puzzles | — (already posted) |
| 2 | Tue 11 Aug — **Hex Truchet** → r/proceduralgeneration | Thu 13 Aug — r/tabletopgamedesign | Wed 12 Aug |
| 3 | Tue 18 Aug — **Simulacrum** → r/reinforcementlearning | Thu 20 Aug — r/MachineLearning `[P]` (gated) | Wed 19 Aug |
| 4 | Tue 25 Aug — **Pushman** → r/Unity3D | Thu 27 Aug — r/gamedev | Wed 26 Aug |
| 5 | Tue 1 Sep — **RLevator** → r/reinforcementlearning | Thu 3 Sep — r/MachineLearning `[P]` (gated) | Wed 2 Sep |

Reddit posts land Tuesday mornings US-Eastern; the second sub is staggered two days so two threads
are never live at once. X threads go Wednesday, a day behind Reddit, so a good comment can be folded
in. **r/MachineLearning is gated on account standing** — skip it if the account is still thin; both
RL projects stand fine on r/reinforcementlearning alone. r/algorithms is deliberately unused: best
topical fit for Gridlocked, but hostile to self-promotion from a new account. Revisit after week 5.

Nothing here posts itself. Reddit and X both punish anything that reads as automated, and the
comment replies are most of the value.
