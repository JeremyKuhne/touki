# Skills catalog (`.agents/skills/`)

Inventory of skills available in this repo, what triggers each, and how to
disambiguate overlapping ones. For the file format and discovery rules, see
[FORMAT.md](./FORMAT.md).

Skill auto-invocation matches against the `description` field. When two skills
have overlapping verbs in their descriptions, the wrong one can fire silently.
The "Disambiguation" section below records every known overlap.

## Inventory

| Skill | Trigger phrasing | Cross-references |
| ----- | ---------------- | ---------------- |
| [polyfill-dotnet-api](./polyfill-dotnet-api/SKILL.md) | "polyfill", "backport", "add a span overload for net472/net481", "make API X available downlevel", missing-on-net472-present-on-net10 | `pre-pr-self-review`, `performance-testing`, `framework-jit-optimization`, `create-pr` |
| [framework-jit-optimization](./framework-jit-optimization/SKILL.md) | hot-path tuning for `net481` in `touki/Framework/`, generic specialization, scalar/unrolled vs BCL delegation, net481 RyuJIT regressions | `performance-testing` |
| [performance-testing](./performance-testing/SKILL.md) | authoring/running BenchmarkDotNet benchmarks in `touki.perf`, comparing implementations, evaluating allocations | `framework-jit-optimization` |
| [pre-pr-self-review](./pre-pr-self-review/SKILL.md) | self-review checklist before opening or pushing a PR; missing tests, unchecked length sums, empty-span foot-guns, TFM phrasing | `create-pr`, `polyfill-dotnet-api` |
| [create-pr](./create-pr/SKILL.md) | "make a PR", "open a pull request", "push and PR", "publish for review" &mdash; **initial** publish | `pre-pr-self-review` |
| [address-pr-feedback](./address-pr-feedback/SKILL.md) | "address the review", "fix the comments", "address Copilot's feedback", "fix the CI failure" &mdash; **post-PR** review-cycle work | `pre-pr-self-review` |
| [agent-files-review](./agent-files-review/SKILL.md) | reviewing/validating agent-customization files (`AGENTS.md`, `*.instructions.md`, `*.prompt.md`, `*.agent.md`, `SKILL.md`, validator, CI workflow); fixing `agent-files.yml` failures | &mdash; |
| [publish-release](./publish-release/SKILL.md) | "publish a new version", "release alpha.N", "ship a beta", "cut a release", "promote alpha to beta", "tag and publish" &mdash; choosing the right `Major.Minor.Patch`, alpha/beta/rc/stable channel, tag stream (`v*` vs `ts-v*`), and GitHub release notes | `pre-pr-self-review` |

## Disambiguation

These pairs are known to compete for auto-invocation. When both descriptions
match the user's request, follow the rule below.

### `create-pr` vs `address-pr-feedback`

Both touch PR mechanics. They are mutually exclusive by **lifecycle stage**:

- **No PR exists yet** &rarr; `create-pr`. The skill ensures changes are on a
  non-`main` branch, commits and pushes, and targets `upstream/main` when
  available.
- **PR exists, reviewer left comments / CI failed** &rarr; `address-pr-feedback`.
  Edit-only by default. Does **not** push without explicit approval.

If the user says "open a PR" while a PR already exists for this branch, ask
which they mean before invoking either.

### `polyfill-dotnet-api` vs `framework-jit-optimization`

Both can land code under `touki/Framework/`. They are mutually exclusive by
**intent**:

- **Adding/back-porting an API surface** missing on net472 &rarr;
  `polyfill-dotnet-api`. Picks the right source (Microsoft package vs PolySharp
  vs hand-rolled) and enforces namespace/`#if` rules.
- **Tuning an existing hot path** for net481 RyuJIT specifically &rarr;
  `framework-jit-optimization`. No new public surface; the file already exists.

If a polyfill needs hot-path tuning after the fact, run `polyfill-dotnet-api`
first to land the surface, then `framework-jit-optimization` for the tuning
pass.

### `performance-testing` vs `framework-jit-optimization`

Adjacent, not overlapping, but easy to confuse:

- **Harness mechanics** (writing a benchmark class, parameters, diagnosers,
  reading the output) &rarr; `performance-testing`.
- **What the JIT will do with the code under benchmark** (inlining, intrinsics,
  devirtualization, the `Unsafe.As` foot-gun) &rarr;
  `framework-jit-optimization`.

You will frequently use both in sequence.

## Maintenance

Freshness is tracked from git history, not from a manual column. CI warns
(does not fail) when a skill directory has no commits in the last 90 days
&mdash; see [.github/workflows/agent-files.yml](../../.github/workflows/agent-files.yml).

A stale warning means "re-read this skill end-to-end against the current
codebase." Confirm:

1. Every cross-reference resolves.
2. Every file path / type / API mentioned still exists.
3. Every claim about the codebase is still true.

The only way to clear the warning is to commit a change to the skill
directory &mdash; ideally the result of a real review pass, but at minimum a
whitespace touch with a commit message stating "verified still current."

When adding a new skill, append a row to the inventory above in the same
change set; the skill is not "shipped" until the catalog reflects it.
