# Skills improvement plan

Status: roadmap. Phase 0 (the in-repo refactor described below) is implemented
in the same change that adds this document; later phases are not started.
Authored 2026-06-04.

This plan answers three questions about the agent skills under
[.agents/skills/](../.agents/skills/):

1. Are the skills appropriately factored?
2. Should some skills be a different file type?
3. Which skills are portable to other repos, and how should they be shared?

It is scoped to the design intent captured from the maintainer: the only sharing
audience is **other personal repos (`JeremyKuhne/*`)**; the target hosts are
**Copilot in VS Code, the Copilot CLI, and the Copilot cloud agent** (not Claude
Code or other agents); and the preferred distribution is **a dedicated skills repo
consumed via `gh skill` that also doubles as a Copilot/Claude plugin marketplace**.

For the format reference and the existing catalog see
[.agents/skills/FORMAT.md](../.agents/skills/FORMAT.md) and
[.agents/skills/README.md](../.agents/skills/README.md). For the layer-selection
matrix see [docs/agent-customization.md](agent-customization.md).

## TL;DR answers

1. **Factoring is good but five skills break the repo's own size budget.** The
   structure (vendor-neutral location, cross-reference catalog, disambiguation
   table, CI freshness checks) is sound. The defect is that the largest
   reference-style skills load 12-18 KB of body on every trigger when only a
   fraction is usually relevant. Split them into a thin core plus sibling files,
   the way [framework-jit-optimization](../.agents/skills/framework-jit-optimization/SKILL.md)
   already does.
2. **The file-type assignments are correct; do not convert skills to prompts or
   agents.** Prompt files are not read by the Copilot CLI or cloud agent, so the
   PR/release workflows must stay skills. The fix is the opposite direction: trim
   the deep "when doing X" bullets in [AGENTS.md](../AGENTS.md) (now 66 % over
   budget) down to one-line pointers at the skills that already own that
   knowledge, and make [touki-reviewer.agent.md](../.github/agents/touki-reviewer.agent.md)
   a thin persona that references the checklist skills instead of restating them.
3. **Most skills have a portable core wrapped around a touki-specific overlay.**
   Share the cores from one `JeremyKuhne/agent-skills` repo that is both a
   `gh skill` source and a plugin marketplace; vendor pinned, provenance-stamped
   copies back into each consuming repo (required, because the cloud agent only
   sees committed repo-level files). Keep the touki overlays in touki.

## 1. Current-state assessment

### What is already right

- Skills live in the vendor-neutral [.agents/skills/](../.agents/skills/)
  location, which all three target Copilot surfaces read.
- [.agents/skills/README.md](../.agents/skills/README.md) is a real catalog with
  a disambiguation section for skills whose descriptions compete for
  auto-invocation - this is the single highest-value piece of the setup and most
  repos do not have it.
- Descriptions are written "pushy", with explicit trigger phrasing, which is what
  drives correct auto-invocation.
- CI freshness (90-day, git-history-derived) and the
  [Validate-AgentFiles.ps1](../tools/Validate-AgentFiles.ps1) frontmatter checks
  already exist.
- [framework-jit-optimization](../.agents/skills/framework-jit-optimization/SKILL.md)
  is the model for progressive disclosure: a 121-line core plus four sibling docs
  (`specialization.md`, `bcl-tradeoffs.md`, `antipatterns.md`, `unrolling.md`)
  that load only when referenced.

### The factoring defect: size-budget violations

Measured 2026-06-04 against the budgets in
[docs/agent-customization.md](agent-customization.md) (SKILL.md body <= 15 KB,
AGENTS.md <= 10 KB, each `*.instructions.md` <= 8 KB):

| File | Size | Budget | Over? |
| ---- | ---- | ------ | ----- |
| `performance-testing/SKILL.md` | 18.5 KB / 370 lines | 15 KB | yes |
| `security-review/SKILL.md` | 15.4 KB / 293 lines | 15 KB | yes |
| `publish-release/SKILL.md` | 14.4 KB / 339 lines | 15 KB | close |
| `polyfill-dotnet-api/SKILL.md` | 14.4 KB / 281 lines | 15 KB | close |
| `pre-pr-self-review/SKILL.md` | 12.7 KB / 252 lines | 15 KB | close |
| `AGENTS.md` | 16.6 KB / 328 lines | 10 KB | yes (66 %) |
| `msbuild.instructions.md` | 9.4 KB / 168 lines | 8 KB | yes (minor) |

A skill body loads in full the moment the skill triggers; only sibling files
load lazily. A 370-line `performance-testing` body therefore costs its whole
weight even when the user only wants the one-line "always pass `-f <tfm>`" rule.
Splitting reclaims that budget and - usefully - the split boundary is the same
boundary as the portable-core / touki-overlay boundary in section 3.

### The duplication defect: AGENTS.md restates skill content

The "General guidance" section of [AGENTS.md](../AGENTS.md) carries multi-line
bullets on span performance, scratch buffers, and polyfills that restate what
[framework-jit-optimization](../.agents/skills/framework-jit-optimization/SKILL.md),
[scratch-buffer-strategy](../.agents/skills/scratch-buffer-strategy/SKILL.md), and
[polyfill-dotnet-api](../.agents/skills/polyfill-dotnet-api/SKILL.md) already own
in depth (and which the `docs/` files back with measurements). That deep prose is
always-on overhead. The always-true one-liner should stay in AGENTS.md; the depth
belongs in the skill it points at.

## 2. Are skills the right file type?

The repo already documents the decision matrix
([docs/agent-customization.md](agent-customization.md)). Applying it to the
target hosts (VS Code + CLI + cloud agent), the type assignments hold:

| Skill group | Current type | Verdict |
| ----------- | ------------ | ------- |
| Workflows (`create-pr`, `address-pr-feedback`, `publish-release`) | skill | **Keep.** Prompt files are not read by the CLI or cloud agent, so a workflow that must run there cannot be a `*.prompt.md`. Skills are the only portable home. |
| Checklists (`pre-pr-self-review`, `security-review`, `agent-files-review`) | skill | **Keep as skills.** A skill is invocable by the user, auto-loaded by the model, and referenceable by any agent persona. Converting to agents would narrow host support (agents load unevenly across VS Code / CLI / cloud). |
| Reference (`framework-jit-optimization`, `polyfill-dotnet-api`, `scratch-buffer-strategy`) | skill | **Keep.** "Knowledge that loads when relevant" is the definition of a skill. |

Two refinements (not type changes):

- **Trim AGENTS.md to pointers** (section 1) so the always-on layer stops
  duplicating the on-demand layer.
- **Make `touki-reviewer` a thin persona.** Today
  [touki-reviewer.agent.md](../.github/agents/touki-reviewer.agent.md) restates
  the JIT-naming rule, polyfill conventions, and test conventions that
  [security-review](../.agents/skills/security-review/SKILL.md),
  [polyfill-dotnet-api](../.agents/skills/polyfill-dotnet-api/SKILL.md), and
  [tests.instructions.md](../.github/instructions/tests.instructions.md) already
  own. Replace the restatements with references so there is a single source of
  truth and no drift. The persona keeps only what is unique to it: the read-only
  stance, the findings-table format, and the "review for what the build will not
  catch" framing.

Recommendation on the delegated "review skills vs agent" question: **keep the
review knowledge in skills, keep one thin reviewer persona that points at them.**
Optionally mark the review/checklist skills with `context: fork` (VS Code only,
ignored elsewhere) so a heavy review runs in a subagent and only its conclusion
returns to the main session.

## 3. Portability and the core-plus-overlay model

Portability is rarely all-or-nothing. The right shape (author once against the
open spec, keep host-specific or repo-specific material additive) is a **generic
core plus a touki overlay**. The split boundary from section 1 is exactly this
boundary, so the refactor and the extraction are one piece of work.

| Skill | Tier | Portable core | Touki overlay |
| ----- | ---- | ------------- | ------------- |
| `security-review` | core extractable | audit checklist: length/size bounds, integer overflow, allocation DoS, complexity / ReDoS, malformed input, the unsafe-API table | touki examples, cross-TFM timing notes, `<Name>.Security.cs` convention |
| `pre-pr-self-review` | core extractable | generic pre-PR checklist: tests per branch, empty/null spans, `checked()` sums, standard throw helpers, "PR description matches the diff" | polyfill conventions, net472/net481 test splits, JIT-naming, the net481 `Unsafe.As` pitfall |
| `performance-testing` | core extractable | BenchmarkDotNet authoring/running, `[MemoryDiagnoser]`, dead-code-elimination discipline | multi-TFM `-f net481/net10`, the `touki.mcp` analyzer, before/after on both TFMs |
| `framework-jit-optimization` | already split | older-RyuJIT optimization knowledge | net481/net10 crossovers, `touki.perf` data |
| `scratch-buffer-strategy` | core extractable | `stackalloc` vs `[SkipLocalsInit]` vs pool decision tree | `BufferScope<T>`, net481/net10 crossovers |
| `fuzz-testing` | core extractable | SharpFuzz target authoring + crash-to-regression loop | `touki.fuzz` layout, cross-TFM build tricks |
| `create-pr` | core extractable | git/PR open workflow + approval gate | branch naming, `dotnet test -c Release`, fork/upstream |
| `agent-files-review` | core extractable | reviewing agent-customization files generally | `Validate-AgentFiles.ps1`, the mirror generator, `agent-files.yml` |
| `polyfill-dotnet-api` | repo-specific (small core) | the "MS package -> PolySharp -> hand-roll" decision tree | `touki/Framework/` layout, namespace rules, extension-block coexistence |
| `address-pr-feedback` | repo-specific | thin approval-gate restatement | touki approval phrasing bank |
| `publish-release` | repo-specific | none meaningful | MinVer dual tag streams, package names |
| `run-tests-on-wsl` | repo-specific | the WSL DrvFs / NuGet trap workaround | oracle suite names, `touki.tests` paths |

### Sharing architecture

Stand up one repo - working name `JeremyKuhne/agent-skills` - that is
simultaneously:

- a **`gh skill` source**: portable skill cores under the standard skill
  directory, each with a clean `name` + `description` and no host-specific
  frontmatter in the core;
- a **plugin marketplace**: a `plugin.json` bundling the skills plus portable
  agent personas plus an `.mcp.json` (the `microsoft-learn` server, and the
  NuGet MCP server for package intelligence), and a `marketplace.json` so
  `copilot plugin marketplace add JeremyKuhne/agent-skills` works. The plugin
  format is the Copilot CLI's and is Claude-compatible, so the same repo serves
  the CLI bundle path and any future Claude use at no extra cost.

Consumption model (the load-bearing constraint):

> The cloud agent has **no user level** - it sees only committed, repo-level
> files. Shared skills therefore cannot be referenced; they must be **vendored as
> committed copies** into each consuming repo.

So the shared repo is the source of truth, and each repo (touki included) holds
**pinned, provenance-stamped copies**:

- `gh skill install JeremyKuhne/agent-skills <skill> --pin vX.Y.Z` vendors a copy
  into `.agents/skills/` and writes the source repo + ref + tree SHA into the
  copy's frontmatter. Commit it.
- `gh skill update` later compares tree SHAs and surfaces upstream drift as a
  normal diff; `--pin` excludes a skill from bulk updates until you re-pin.
- The touki overlay skills stay in touki and reference the vendored cores.

Constraint to resolve first: the GitHub CLI (`gh` >= 2.90, which provides
`gh skill`) is **not installed on the maintainer's machine**. Either install it
(CI runners already have it) or use the manual-vendor fallback: copy the core
folder in, record the source ref in frontmatter by hand, and run a small
tree-SHA drift script in CI instead of `gh skill update`.

### Expressing the skill -> MCP dependency

[polyfill-dotnet-api](../.agents/skills/polyfill-dotnet-api/SKILL.md) uses the
`microsoft-learn` MCP server to confirm modern BCL shapes. The spec has no
machine-enforced dependency field, so use the layered convention:

- add a `compatibility:` frontmatter line: "Uses the microsoft-learn MCP server
  to verify modern BCL API shapes when available; falls back to fetched web docs
  otherwise";
- write the body defensively: "if the microsoft-learn tools are available, use
  them; otherwise fetch the docs page" (graceful degradation works on every
  host);
- in the shared plugin, list `microsoft-learn` in the bundle's `.mcp.json` so
  installing the plugin wires the server - the only path that actually installs
  the dependency with the skill;
- if polyfill work ever runs on the **cloud agent**, add `microsoft-learn` (with
  a `tools` allowlist) to the repo's cloud-agent MCP configuration, since that
  surface does not read `.vscode/mcp.json`.

### Portability metadata

The spec has no portability field, so record the tier in the sanctioned
`metadata` map plus the catalog:

```yaml
metadata:
  portability: repo-specific          # portable | semi-portable | repo-specific
  shared-source: JeremyKuhne/agent-skills@v1.2.0   # only on vendored copies
```

Add a matching column to [.agents/skills/README.md](../.agents/skills/README.md).
Verify [Validate-AgentFiles.ps1](../tools/Validate-AgentFiles.ps1) tolerates the
extra frontmatter key before rolling it out (it currently checks only `name` and
`description`).

## 4. Phased execution plan

Each phase is independently reviewable and reversible. Phase 0 is entirely
in-repo and unblocks the rest.

### Phase 0 - in-repo refactor (no external repo, low risk)

- [ ] Split `performance-testing`, `security-review`, `polyfill-dotnet-api`,
      `pre-pr-self-review`, and `publish-release` into a thin core SKILL.md plus
      sibling reference files, mirroring `framework-jit-optimization`. Target
      every core under ~150 lines; push tables, examples, and measured data into
      siblings. The core/sibling cut follows the core/overlay column in section 3.
- [ ] Trim the AGENTS.md "General guidance" deep bullets (span performance,
      scratch buffers, polyfills) to one-line pointers; regenerate the mirror with
      `pwsh tools/Validate-AgentFiles.ps1 -Fix`. Re-measure to confirm AGENTS.md
      is back under 10 KB.
- [ ] Thin [touki-reviewer.agent.md](../.github/agents/touki-reviewer.agent.md):
      replace restated rules with references to the owning skills/instructions.
- [ ] Add the `compatibility:` line and graceful-degradation prose to
      `polyfill-dotnet-api`.
- [ ] Add `metadata.portability` to every skill and the catalog column; confirm
      the validator passes.
- [ ] Optional: add `context: fork` to the review/checklist skills.
- Validation: `pwsh tools/Validate-AgentFiles.ps1`; re-run the size measurement;
      confirm every catalog cross-reference still resolves.

### Phase 1 - stand up the shared repo

- [ ] Create `JeremyKuhne/agent-skills` (confirm the name first).
- [ ] Move the portable cores in; keep their descriptions host-neutral.
- [ ] Add portable agent personas (the generic reviewer), `plugin.json`,
      `marketplace.json`, and `.mcp.json` (`microsoft-learn`, NuGet MCP).
- [ ] Tag `v0.1.0`.
- Validation: install the plugin into a scratch checkout via the CLI; confirm the
      skills appear and the MCP servers start.

### Phase 2 - re-consume into touki

- [ ] Vendor the pinned cores back into touki's `.agents/skills/` (via
      `gh skill install --pin`, or manual-vendor if `gh` is not installed).
- [ ] Keep the touki overlays in touki, referencing the vendored cores.
- [ ] Update the catalog with the `shared-source` provenance.
- Validation: full `Validate-AgentFiles.ps1`; spot-check that a vendored core plus
      its overlay still reads as one coherent skill.

### Phase 3 - roll out to the next personal repo

- [ ] Install the plugin (or vendor the skills) into one other `JeremyKuhne/*`
      repo end to end as the real cross-repo test.
- [ ] Capture anything that needed per-repo editing; fold genuinely generic bits
      back into the cores.

### Phase 4 - freshness and CI automation

- [ ] Add a report-only `gh skill update --all` drift job (or the tree-SHA script
      fallback) that opens a PR when a vendored core diverges from upstream.
- [ ] Optionally add `skills-ref validate` over `.agents/skills/**` to the
      existing [agent-files.yml](../.github/workflows/agent-files.yml).
- [ ] Keep the existing 90-day freshness warning.

## 5. Open decisions

- **Shared repo name** - `JeremyKuhne/agent-skills` vs `dotnet-agent-skills` vs
  other.
- **`gh` adoption** - install the GitHub CLI locally for `gh skill`, or use the
  manual-vendor + drift-script fallback.
- **Portable agent personas** - worth publishing given uneven agent-host support,
  or keep agents touki-local for now and share only skills + MCP.
- **Cloud-agent MCP** - only needed if polyfill work runs on the cloud agent;
  defer until that happens.
- **msbuild.instructions.md (9.4 KB)** - out of scope here, but it is over its
  8 KB budget and is a candidate for a later split.

## 6. Per-skill disposition (appendix)

| Skill | Phase 0 action | Tier | Where the core lives after sharing |
| ----- | -------------- | ---- | ---------------------------------- |
| `performance-testing` | split | core extractable | shared (mechanics) + touki overlay (TFM/mcp) |
| `security-review` | split | core extractable | shared (checklist) + touki overlay |
| `polyfill-dotnet-api` | split + add `compatibility:` | repo-specific (small core) | shared (decision tree) + touki overlay (layout) |
| `pre-pr-self-review` | split | core extractable | shared (checklist) + touki overlay |
| `publish-release` | split | repo-specific | touki only |
| `framework-jit-optimization` | none (already split) | core extractable | shared (RyuJIT knowledge) + touki overlay (data) |
| `scratch-buffer-strategy` | metadata only | core extractable | shared (decision tree) + touki overlay |
| `fuzz-testing` | metadata only | core extractable | shared (harness) + touki overlay (project) |
| `create-pr` | metadata only | core extractable | shared (workflow) + touki overlay |
| `agent-files-review` | metadata only | core extractable | shared (checklist) + touki overlay (tooling) |
| `address-pr-feedback` | metadata only | repo-specific | touki only |
| `run-tests-on-wsl` | metadata only | repo-specific | touki only (WSL trap note may generalize) |
