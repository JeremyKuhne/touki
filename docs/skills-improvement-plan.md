# Skills improvement plan

Status: roadmap. **Phase 0 (the in-repo refactor) is complete** - merged in
[PR #174](https://github.com/JeremyKuhne/touki/pull/174). **Phase 1 (the
`manage-skills` meta-skill) is implemented** in
[PR #175](https://github.com/JeremyKuhne/touki/pull/175). **Phase 2 (the commons
skeleton)** and **Phase 3 (the `security-review` vendoring pilot)** are done - the
commons is [JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills).
**Phase 4 is in progress**: `scratch-buffer-strategy` (commons `v0.2.1`,
[PR #177](https://github.com/JeremyKuhne/touki/pull/177)) and
`framework-jit-optimization` (commons `v0.3.0`,
[PR #178](https://github.com/JeremyKuhne/touki/pull/178)) are extracted and
vendored; the four `traceq`-independent cores are next, then the plan **pauses at
the `traceq` seam** (see Phase 4) before `performance-testing` and Phase 5.
Section 7 vets the whole sharing design end to end against the two real
consumers, [`JeremyKuhne/madowaku`](https://github.com/JeremyKuhne/madowaku)
(a merge) and [`JeremyKuhne/thirtytwo`](https://github.com/JeremyKuhne/thirtytwo)
(greenfield). Authored 2026-06-04; last updated 2026-06-06.

**Update 2026-06-15 - the `traceq` seam is cleared.** The trace tooling was
promoted out of touki into the standalone
[`filtrace`](https://github.com/JeremyKuhne/filtrace) repo, published to NuGet
(`KlutzyNinja.Filtrace` CLI + `KlutzyNinja.Filtrace.Mcp` server, `0.1.0`), and
touki migrated onto the packages (touki #220). filtrace **ships its own agent
skill**, which adds a third skill source beyond born-local and the commons: a
**tool-shipped skill** whose canonical home is the tool's own repo
(single-sourced from filtrace `docs/`), vendored into consumers exactly like a
commons core but re-vendored from the tool repo rather than the commons. touki
now vendors it at
[.agents/skills/filtrace/](../.agents/skills/filtrace/SKILL.md) (pinned, with a
touki [overlay](../.agents/skills/filtrace/overlay.md)), and `performance-testing`
delegates trace-driving to it instead of re-documenting the verbs. What remains of
Phase 4 is the `performance-testing` BenchmarkDotNet core extraction into the
commons - its profiling half now points at the vendored `filtrace` skill, not the
retired `touki.mcp`.

This plan answers three questions about the agent skills under
[.agents/skills/](../.agents/skills/):

1. Are the skills appropriately factored?
2. Should some skills be a different file type?
3. Which skills are portable to other repos, and how should they be shared?
4. Which repos should actually carry each skill (and how is a skill kept from
   firing where it does not belong)?

It is scoped to the design intent captured from the maintainer: the sharing
audience is the maintainer's own .NET repos and is **not limited to the
`JeremyKuhne` owner** - `thirtytwo` and other repos are in scope, and some skills
may eventually be consumed **publicly**; the target hosts are **Copilot in VS Code,
the Copilot CLI, and the Copilot cloud agent** (not Claude Code or other agents);
and the preferred distribution is **a dedicated skills repo consumed via
`gh skill` that also doubles as a Copilot/Claude plugin marketplace**.

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
   the deep "when doing X" bullets in [AGENTS.md](../AGENTS.md) (was 66 % over
   budget before Phase 0) down to one-line pointers at the skills that already own
   that knowledge, and make [touki-reviewer.agent.md](../.github/agents/touki-reviewer.agent.md)
   a thin persona that references the checklist skills instead of restating them.
3. **Most skills have a portable core wrapped around a touki-specific overlay.**
   Share the cores from one `JeremyKuhne/agent-skills` repo - a **bidirectional
   commons**, not a touki export - that is both a `gh skill` source and a plugin
   marketplace; vendor pinned, provenance-stamped copies back into each consuming
   repo (required, because the cloud agent only sees committed repo-level files).
   Keep each repo's overlay local. The first consumer, `madowaku`, already proves
   the two-way flow: it contributes `cswin32-*` skills touki lacks and carries a
   hand-forked `performance-testing` to reconcile (section 7). **Portability** (how
   much a skill's content must change) and **applicability** (which repos should
   carry it at all) are separate axes: a repo only ever vendors the skills it
   actually needs, so a CsWin32 COM skill is simply never installed into touki
   (section 3).

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

Measured 2026-06-04 (the pre-Phase-0 baseline; Phase 0 below records the result)
against the budgets in
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
| `address-pr-feedback` | core extractable | the post-PR approval-gate + feedback workflow (shared with `create-pr`) | touki approval phrasing bank, `AGENTS.md` anchor |
| `publish-release` | repo-specific | none meaningful | MinVer dual tag streams, package names |
| `run-tests-on-wsl` | repo-specific | the WSL DrvFs / NuGet trap workaround | oracle suite names, `touki.tests` paths |

### Sharing architecture: a bidirectional commons

Stand up one repo - working name `JeremyKuhne/agent-skills` - that is
simultaneously a **`gh skill` source** (portable skill cores under the standard
directory, each with a clean `name` + `description` and no host-specific
frontmatter) and a **plugin marketplace** (`plugin.json` bundling the skills,
portable agent personas, and an `.mcp.json` wiring `microsoft-learn` + the NuGet
MCP server; `marketplace.json` so
`copilot plugin marketplace add JeremyKuhne/agent-skills` works). The plugin
format is the Copilot CLI's and is Claude-compatible, so one repo serves the CLI
bundle path and any future Claude use at no extra cost.

It is **not** "touki broadcasts its skills outward." Section 7 shows the first
consumer, `madowaku`, already carries skills touki lacks (`cswin32-interop`,
`cswin32-com`) and a hand-forked copy of `performance-testing`. The commons takes
portable cores **from every repo** and feeds them back to all of them.

Consumption model (the load-bearing constraint):

> The cloud agent has **no user level** - it sees only committed, repo-level
> files. Shared cores therefore cannot be referenced; they must be **vendored as
> committed copies** into each consuming repo.

Each repo (touki and madowaku included) holds **pinned, provenance-stamped
copies**:

- `gh skill install JeremyKuhne/agent-skills <skill> --pin vX.Y.Z` vendors a copy
  into `.agents/skills/` and writes the source repo + ref + tree SHA into the
  copy's frontmatter. Commit it. (`gh` 2.93 is now installed locally; it just
  needs `gh auth login`. The manual-vendor fallback - copy the folder, record the
  ref by hand, run a tree-SHA drift script in CI - remains the offline option.)
- `gh skill update` later compares tree SHAs and surfaces upstream drift as a
  normal diff; `--pin` excludes a skill from bulk updates until you re-pin.

Three rules the `madowaku` vet (section 7) forces on what a "core" may contain.
They are not stylistic: the consumer's CI runs the same `Test-AgentFileLinks.ps1`
link gate and the same `Validate-AgentFiles.ps1`, so a core that breaks them
fails the consumer's build.

1. **No outward links in the core - not to sibling skills, instructions files,
   `docs/`, or the source tree.** touki's `performance-testing` links to
   `framework-jit-optimization` and `scratch-buffer-strategy`; madowaku's links to
   `cswin32-*`. A shared core carrying either set dangles in the other repo and
   fails its link check. The same applies to `../../../docs/...` links (see
   "Delivery of referenced docs" below) and - the largest category by far - to the
   34 `../../../touki/...` source example links the skills currently carry (see
   "The bigger dangling surface" below). `gh skill install` copies none of these.
   Every outward link lives in the per-repo overlay; deep content the core
   genuinely needs travels as the skill's own bundled `references/` or a portable
   sibling.
2. **No repo-specific paths, project names, or TFM monikers in the core.** The
   core says "the perf project" and `-f <tfm>`; the overlay supplies `touki.perf`
   / `net10.0` or `madowaku.perf` / `net10.0-windows10.0.22000.0`. (Both repos
   already share `net481` as the framework bench moniker - that much is genuinely
   common.)
3. **Vendoring is a merge, not a greenfield drop.** madowaku already has its own
   `.agents/skills/README.md` catalog, `FORMAT.md`, and a divergent
   `performance-testing`, plus a *different* `.github/instructions/` set
   (`interop`, no `perf`/`polyfills`). A core that links to `perf.instructions.md`
   or `polyfills.instructions.md` breaks there; reconcile against what exists.

### Delivery of referenced docs (the `references/` bundle)

A skill body that links `../../../docs/foo.md` has a hidden coupling: `gh skill
install` copies the **skill directory only**. The `docs/` file does not travel,
so the link dangles in every consumer and fails its `Test-AgentFileLinks.ps1`
gate - the same failure mode as a sibling-skill cross-reference, which is why
rule 1 above now names `docs/` explicitly.

Six docs are reached this way from skill bodies today. The `Class` column is the
shareability classification from a content audit: **A** = portable whole, **B** =
touki-specific (stays behind), **C** = split (portable core + touki appendix).

| Doc | Size | Class | Linked from skill | Portable / touki |
| --- | ---- | ----- | ----------------- | ---------------- |
| `arraypool-performance.md` | 28 KB | A | `scratch-buffer-strategy` | ~28 KB / ~0 - generic buffer-strategy field manual |
| `polyfill-layout.md` | 5 KB | C | `polyfill-dotnet-api` | ~2.5 KB extern-alias recipe / ~2.5 KB touki layout |
| `performance-investigation.md` | 39 KB | C | `performance-testing`, `scratch-buffer-strategy` | ~16 KB profiling field manual / ~23 KB `touki.mcp` |
| `performance-investigation-without-mcp.md` | 3 KB | B | `performance-testing` | touki `tools/` scripts |
| `fuzz-testing-plan.md` | 7 KB | B | `fuzz-testing` | touki fuzz roadmap |
| `globbing-feature-plan.md` | 76 KB | B | `run-tests-on-wsl` | touki feature log |

The doc that prompted this review, `framework-span-performance.md` (21 KB, **C**:
~17 KB portable strategy manual / ~4 KB touki worked example), is *not* in that
set: it is reached from **AGENTS.md** and five sibling docs, not from a skill
body. It is, however, a prime candidate to *become* `framework-jit-optimization`'s
bundled reference when that skill is shared - the same net472 RyuJIT applies to
madowaku and thirtytwo. Its 28 KB companion `bcl-ignorecase-valley-rca.md` is a
pure touki case study (**B**) that stays behind; it is referenced only from a
source comment, not from any skill, so it never needs to travel.

The Agent Skills spec already supplies the mechanism. A skill directory may carry
`scripts/`, `assets/`, and **`references/`** ("docs loaded on demand"). Anything
under the skill directory travels with `gh skill install` and, like an in-dir
sibling, loads only when the SKILL.md links it (progressive-disclosure tier 3).
No skill uses a `references/` directory yet; this is where a skill's genuinely
needed deep doc belongs, not `docs/`.

Decision rule, one per referenced doc:

1. **Generic deep content the skill needs wherever it runs** -> move the whole
   file into `<skill>/references/`. It travels, loads on demand, and is
   spec-canonical. `arraypool-performance.md` is the clean case: the content audit
   put it at ~95 % portable (class A), so `scratch-buffer-strategy` carries it
   whole and madowaku / thirtytwo get identical net472-JIT buffer guidance.
2. **touki-specific deep content** (a touki tool, feature plan, or measurement)
   -> the link lives in the **touki overlay**, never the shared core.
   `performance-investigation-without-mcp.md` (touki `tools/` scripts),
   `fuzz-testing-plan.md`, and `globbing-feature-plan.md` are these (class B);
   consumers neither inherit nor miss them. `fuzz-testing` and `run-tests-on-wsl`
   are repo-local / project-gated skills anyway, so their doc links never leave
   touki.
3. **Half-and-half docs** (a portable concept plus a touki appendix) -> split:
   the portable field-manual part becomes a `references/` doc that travels; the
   touki case study / measurement table / tool integration stays in `docs/`. The
   audit found three class-C docs: `framework-span-performance.md` (~17 KB
   strategy manual travels, ~4 KB worked example stays), `polyfill-layout.md`
   (~2.5 KB extern-alias recipe travels, ~2.5 KB touki layout stays), and
   `performance-investigation.md` (~16 KB profiling field manual could travel,
   ~23 KB `touki.mcp` integration stays). Moving the 76 KB whole of
   `globbing-feature-plan.md` into a skill would be the wrong call; splitting a
   class-C doc is the right one.

Mechanics and ordering:

- **The canonical copy stays single.** When a doc moves into a shared skill's
  `references/`, every prior inbound link (AGENTS.md, sibling docs) repoints to
  the new path - so AGENTS.md will link *into* a skill directory. That is unusual
  but valid; the link checker accepts it and nothing is duplicated. A `docs/`
  doc that is *also* a skill reference is the trigger to split (rule 3), never to
  keep two hand-synced copies.
- **This is real content movement, not a Phase-0-style edit**, so it lands in the
  core-extraction phases - the pilot (Phase 3) for the first skill and the bulk
  (Phase 4) for the rest, where touki re-vendors each core and repoints its
  AGENTS.md / `docs/` links. Until a consumer exists the current touki links are
  correct as-is - the dangle only manifests on a *consumer*, and there is none
  yet.

### The bigger dangling surface: source example links and overlay siblings

A full link audit of every skill file changes the priority order. The `docs/`
coupling above is real but small - **7 references**. The dominant dangling
category is something the earlier drafts under-weighted: **34 links from skill
bodies into the touki source tree** (`../../../touki/Framework/Polyfills/...`,
`../../../touki.perf/...`, `../../../touki.tests/...`, `../../../touki.fuzz/...`),
almost all of them "see this example file" citations. For comparison the audit
counted 30 skill->skill links, 7 docs, 5 instructions, 4 AGENTS, and just 1
external. Source example links are the single largest thing that breaks a vendored
skill's link check, so rule 2 ("no repo-specific paths in the core") is not a
minor style nit - it is the load-bearing rule, and the example-file links are
where it bites hardest. Two skills concentrate the problem: `polyfill-dotnet-api`
(~15 example links across its core and siblings) and `pre-pr-self-review`
(~10, all inside its `polyfill-correctness.md` sibling).

That second case forces a refinement to the model: **sibling files split into two
kinds, exactly like the skill itself.**

- A **portable sibling** is generic deep content that should travel with the
  shared core (e.g. `security-review`'s `unsafe-apis.md`, `framework-jit-optimization`'s
  `specialization.md`). It goes in the commons copy.
- An **overlay sibling** is touki-specific content that must *not* travel even
  though it lives in the same directory today. `pre-pr-self-review`'s
  `polyfill-correctness.md` is the clear case: it is a `SELF` sibling (so a naive
  `gh skill install` would copy it), but its body is touki polyfill guidance laced
  with ~10 `touki/Framework/...` example links. Vendor it as-is and every consumer
  inherits ten dangling links. It belongs in the touki overlay, not the shared
  payload.

So the core/overlay boundary runs *through* a skill's sibling set, not just
between the core SKILL.md and its siblings. The Phase-0 split optimized siblings
for context size (load-on-demand); the sharing split must additionally tag each
sibling **portable** or **overlay**, and only portable siblings (plus `references/`)
travel. Concretely, when a shared core needs to *show* an example, the core says
"see the polyfill examples under your repo's `Framework/Polyfills/` tree" (a
generic instruction the agent resolves locally) while the *concrete* file links
stay in the overlay sibling. This is the same move as rule 2, applied one level
down.

Validation is mechanical and already wired: a vendored skill that still carries a
source example link, an overlay sibling, or a `docs/` link fails the consumer's
`Test-AgentFileLinks.ps1`. That gate is the proof the core was extracted cleanly
(section 7's acid test).

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

### Two axes: portability and applicability

A single "portability" tier is not enough. Two independent questions decide a
skill's fate in any given repo:

- **Portability** - how much the skill's *content* must change to be reused.
  `security-review` is generic; `run-tests-on-wsl` is touki-pathed. (Recorded as
  `metadata.portability`, added in Phase 0.)
- **Applicability** - whether a repo *should carry the skill at all*, by domain.
  Independent of portability: `cswin32-com` is highly portable (its content is
  generic across any CsWin32-COM codebase) yet narrowly applicable (only repos
  that do COM interop). touki does P/Invoke but no COM, so it should carry
  `cswin32-interop` marginally at most and **never** `cswin32-com`.

Record both, documentation-only, in the `metadata` map plus the catalog:

```yaml
metadata:
  portability: repo-specific          # portable | semi-portable | repo-specific
  applicability: cswin32-com          # universal | dotnet-framework | cswin32 | cswin32-com | project-gated | repo-local
  requires-project: <root>.fuzz       # optional; the project the skill drives (scaffolded if absent - see below)
  shared-source: JeremyKuhne/agent-skills@v1.2.0   # only on vendored copies
```

Honest limit: neither field is machine-enforced. `Validate-AgentFiles.ps1`
checks only `name` and `description`, and its hand-rolled parser does not read
nested `metadata` at all. No Copilot host gates skill loading on a custom field
either. These tags are decision aids for the human or agent choosing *what to
vendor*, not runtime guards. Add matching columns to
[.agents/skills/README.md](../.agents/skills/README.md).

### Controlling where a skill fires

There is exactly one reliable control and one soft control.

1. **Selective vendoring (reliable, primary).** Skills auto-load from
   `.agents/skills/`. A skill that is not vendored into a repo cannot fire there,
   period. "CsWin32 COM should not be used in touki" is enforced simply by never
   installing `cswin32-com` into touki. Applicability is therefore implemented as
   *which cores each repo chooses to vendor* - a selection, not a flag.
2. **Description discipline (soft, secondary).** Auto-invocation matches the
   `description`. Scope a domain skill's trigger phrasing tightly ("...for the
   CsWin32 COM layer") so that even if it is present it will not match a foreign
   task. This reduces misfires; it does not guarantee silence.

Two limits worth stating plainly, because they shape the design:

- **Skills have no `applyTo` path glob.** That field belongs to
  `*.instructions.md`, not skills. You cannot path-scope a skill *within* a repo
  (e.g. "only for files under `src/com/`"). The granularity is per-repo (vendor
  or not), not per-path. A rule that must be path-scoped belongs in an
  instructions file, not a skill.
- **No host reads a custom applicability field.** So the matrix in section 6 is
  enforced by the vendoring step (and by CI that fails if an unexpected skill
  directory appears), not by the field itself.

### Commons structure: a tiered catalog, not one bundle

Because applicability is per-repo, the commons must be a catalog you *pick from*,
not an all-or-nothing bundle. Three tiers:

- **Universal** - vendor into essentially every .NET repo: `security-review`,
  `pre-pr-self-review`, `create-pr`, `address-pr-feedback`, `agent-files-review`,
  `performance-testing`, `scratch-buffer-strategy`, `framework-jit-optimization`,
  and `manage-skills` (the lifecycle meta-skill - section 3).
- **Domain** - vendor only into repos in that domain: `cswin32-interop` /
  `cswin32-com` (CsWin32 repos: madowaku, thirtytwo), `polyfill-dotnet-api`
  (repos with a `Framework/` polyfill tree), `fuzz-testing` (any library with a
  parser / codec / buffer surface worth fuzzing - project-gated, see below).
- **Repo-local** - never shared: `run-tests-on-wsl` (touki globbing oracles),
  the dual-stream half of `publish-release`.

Two ways to deliver "pick from": per-skill `gh skill install <skill>` (the CLI
installs one skill at a time, which is exactly the selection mechanism), or a
marketplace that publishes **multiple plugins** (a `dotnet-universal` bundle and a
`cswin32` bundle) so a consumer installs only the relevant set. Prefer per-skill
installs for fine control; offer the bundles as a convenience for the common
groupings.

### Project-name conventions and standing up missing projects

Several skills drive a *sibling project*, and those projects follow a naming
pattern. The KlutzyNinja-family repos use a flat convention at the repo root:
`<root>` for the library, `<root>.tests`, `<root>.perf`, `<root>.fuzz`,
`<root>.testsupport`. So "the fuzz project" maps to a concrete name by convention,
and that convention **is** the generification: a shared core never hardcodes
`touki.fuzz` - it says "the fuzz project (`<root>.fuzz` by convention)" and the
agent resolves `<root>` from whatever repo it is working in. There is no
templating engine in skills (the `metadata` map is inert), so this is prose
convention plus agent inference, not string substitution.

**The convention is not universal, so the core must detect rather than assume.**
`thirtytwo` predates it: its projects live under `src/` as `src/thirtytwo` and
`src/thirtytwo_tests` (an underscore, not `.tests`, nested under `src/`). A core
that assumes the flat dotted form would generate wrong paths there. Two responses,
used together: the core states the convention and instructs the agent to match the
*host repo's actual* layout (look at the existing sibling projects, don't assume
touki's); and where a repo deviates, its overlay records the mapping (logical
project -> concrete path) so the resolution is pinned, not guessed.

**Project-gated applicability.** This reframes a row the earlier draft got wrong.
`fuzz-testing` is not repo-local - it applies to any repo that has, *or should
have*, a fuzz project. madowaku has no `madowaku.fuzz` *yet*; that is "not
provisioned," not "never applicable." Contrast `run-tests-on-wsl`, which is tied
to touki's globbing oracle suites and has no analog anywhere - genuinely
repo-local. The matrix in section 6 therefore distinguishes **bootstrap** (vendor
it; the project will be stood up) from **no** (never belongs here).

**Standing up a missing project.** A project-gated skill carries its own scaffold,
so vendoring it into a repo that lacks the project is self-sufficient. The skill
directory ships an `assets/` template - the `.csproj` (including the cross-TFM
`DependencyTargetFramework` trick), the `Program.cs` harness switch, `GlobalUsings.cs`,
and the prereq install script - and a bootstrap step in the body:

> This skill drives the fuzz project. If `<root>.fuzz` (resolved against this
> repo's project-naming convention) does not exist, scaffold it from `assets/`,
> name it to match the repo's existing sibling projects, wire it into the
> solution (`*.slnx`), then proceed.

On the origin repo (touki) the project already exists, so the bootstrap is a
no-op. The template itself is core-plus-overlay: the skeleton (SharpFuzz
reference, harness switch, cross-TFM csproj trick) is generic; the lines that name
the library project are `<root>` placeholders the agent fills in. Because vendoring
a project-gated skill into a repo without the project triggers that bootstrap,
**vendoring `fuzz-testing` into madowaku is itself the act of declaring "I want
fuzzing here"** - the skill turns the intent into `madowaku.fuzz`.

### Greenfield vs merge consumers

The two real consumers sit at opposite ends, and the rollout must handle both:

- **madowaku is a merge.** It already has `.agents/skills/`, a catalog,
  `FORMAT.md`, a divergent `performance-testing`, and a *different* instruction
  set. Vendoring reconciles against what exists (section 7).
- **thirtytwo is greenfield.** It has `.github/` but **no `AGENTS.md` and no
  `.agents/`**. Bootstrapping it means standing up the scaffolding first
  (`.agents/skills/` + catalog + `FORMAT.md` + `Validate-AgentFiles.ps1` +
  `agent-files.yml`), then vendoring - the plugin install can carry the
  scaffolding, or it can be copied from touki/madowaku. It also **deviates from
  the project-naming convention** (`src/thirtytwo`, `src/thirtytwo_tests` with an
  underscore, nested under `src/`), so any project-gated skill vendored there must
  resolve against that layout, not the flat dotted form (section 3).

### Governance once consumers are external or public

The moment a consumer is public or outside your control, the lightweight model
needs the governance layer the 2026 practitioner guidance calls for:

- The commons repo is **public** with a `LICENSE` (MIT, matching touki).
- Installs are **`--pin`ned** to a tag or SHA, and the commons publishes
  **immutable releases** so a pinned tag cannot be rewritten after the fact.
- Provenance frontmatter (source repo + ref + tree SHA) lets every consumer -
  yours or not - detect upstream drift with `gh skill update`.
- These were "nice to have" while every consumer was yours; they become
  load-bearing the moment one is not.

### The meta-skill: find, build, and update skills

Everything above describes the *architecture*; this is the *operating manual*
that puts it in an agent's hands. The lifecycle - discover a skill, add one,
update one, flow improvements back - should itself be a skill, so that "find a
skill for X", "build a skill for X", and "update the skill" produce output aligned
with this plan instead of ad-hoc results. Built under the name `manage-skills` in
Phase 1 (the name remains an open decision, section 5). It is **universal tier**
(every consuming repo needs it) and it dogfoods the very pattern the plan
prescribes: a thin core that dispatches to three sub-pages, `find.md`, `build.md`,
`update.md`.

It is also the bootstrap tool. There is a mild chicken-and-egg - you need a way to
vendor skills before you have vendored any - resolved by building `manage-skills`
first **in touki** (Phase 1), where it works in degraded mode before any commons
exists, then migrating it to the commons and vendoring it back as the first
install; thereafter it drives every other `find` / `build` / `update`.

#### Find

"Find a skill for X" / "is there a skill that does X" runs a **tiered search**,
nearest-trust first:

1. **Local** - the repo's own `.agents/skills/`. Already vendored? Then the answer
   is "you have it"; maybe it only needs an overlay tweak.
2. **The commons** - `gh skill search JeremyKuhne/agent-skills` (or browse the
   marketplace). The curated, trusted source; anything here is pre-vetted.
3. **Public catalogs** - `gh skill search` against awesome-copilot,
   `anthropics/skills`, and the wider registry. Untrusted by default; gated below.

Find also applies the **applicability** axis (section 3): a `cswin32-com` hit is
irrelevant in a repo that does no COM, and the skill should say so rather than
recommend a vendor that will never fire. Output is a short report: where the skill
exists, its tier, whether it is applicable here, and the recommended action
(vendor / tweak / build / skip).

#### Build (which first runs Find)

The load-bearing rule: **"build a skill" must start by finding one.** Reinventing
a skill that already exists - in the commons or publicly - is the failure mode this
skill exists to prevent. The decision tree:

1. **Run Find.**
2. **Already in this repo** -> do not build. If it does not quite fit, add or edit
   the per-repo *overlay*, never the vendored core (see Update).
3. **In the commons** -> do not build. Vendor it (`gh skill install --pin`) and add
   a thin overlay for repo-specific paths / cross-references.
4. **In a public catalog** -> do not build from scratch. Evaluate trust and quality
   (security gate below); if good, vendor it; if close-but-imperfect, fork it into
   the commons and vendor that. A mediocre public skill is usually worth adapting
   over starting blank.
5. **Nowhere** -> build new, following [FORMAT.md](../.agents/skills/FORMAT.md): a
   thin core under the size budget, sibling files for depth, `metadata.portability`
   + `applicability` set at birth, a catalog row, and a disambiguation entry if its
   trigger phrasing competes with an existing skill. Decide its tier and whether it
   is **born-local** (touki-specific, stays in touki) or **born-shared** (generic,
   authored directly in the commons and vendored back).

**Security gate for public sources** (the 2026 guidance is emphatic - ToxicSkills
found ~13 % of public skills carry a critical issue): `gh skill preview` before any
install; read the `SKILL.md`, every script, and every `references/` file, not just
the summary; pin to a tag or SHA; **never accept `allowed-tools: shell` (or any
pre-approved tool) from a third party** - strip it and let the host prompt; prefer
provenance-bearing sources; treat a cloned repo's `.agents/` as untrusted code.

#### Update (pull drift, push improvements)

Update has two directions, and the second is the one the plan most needs to pin
down.

- **Pull** - `gh skill update` compares the vendored copy's provenance tree SHA
  against upstream and surfaces drift as a normal diff. Review it like a dependency
  bump; re-pin when satisfied.
- **Push** - when you *improve* a vendored skill locally, classify the change
  before saving it. Classification does not trigger any action by itself:
  - **Common** (generic, benefits every consumer - a clearer phrasing of a
    portable rule, a new universally-applicable check): it *should* go
    **upstream**, but upstreaming is **never automatic**. It is not always
    plausible - the commons may be unreachable, the change may be sensitive or
    need discussion, or you may lack the time or rights. So **ask** before
    attempting it; never open a commons PR unprompted (it is a publish action,
    gated like any push). If upstreamed, re-vendor at the new pin; if not yet,
    keep it in the local core as a *tracked pending-upstream divergence* -
    recorded, not hidden - and re-attempt later.
  - **Local deviation** (touki-specific - a `touki.mcp` note, a touki path, a
    touki-only example): it belongs in the **overlay**, never in the vendored core.

The **golden rule** that makes the bidirectional commons stable: *never let a
vendored core diverge silently.* A vendored core is a mirror of upstream; an edit
to it is a deliberate fork that must end in one of three **recorded** states -
never an unexplained one: promoted upstream and re-pinned; moved to the overlay
with the core restored; or kept as a tracked pending-upstream divergence (a common
change that could not be upstreamed yet). The point is *visibility*, not speed: a
recorded divergence is fine, an unexplained one is the alarm. The provenance SHA
plus the `gh skill update` drift check is what makes that enforceable - unexplained
drift with no upstream PR in flight is the signal that an improvement was written
into the wrong layer. This is the mechanism behind the bidirectional commons (the
"Sharing architecture" subsection's "portable cores from every repo, fed back to
all of them"): common improvements flow upstream *when plausible*, local deviations
live in the overlay.

#### Disambiguation

`manage-skills` must not collide with two neighbours. It is **not**
[agent-files-review](../.agents/skills/agent-files-review/SKILL.md) - that validates
the *syntax and conventions* of an individual agent file (frontmatter, mirror sync,
whitespace); `manage-skills` operates on the *catalog lifecycle* (discover, add,
vendor, sync). Run `manage-skills` to bring a skill in or push one out, then
`agent-files-review` to validate the file you ended up with. It is also distinct
from any host's built-in "create a skill" helper, which scaffolds a blank file
without the find-first / commons-aware logic that is this skill's whole point.

## 4. Phased execution plan

Each phase is independently reviewable and reversible. Phase 0 is entirely
in-repo and unblocks the rest.

The phasing below was re-cut once the design stabilized. An earlier draft had five
phases that batched the work by *activity* - "stand up the repo", "extract all the
cores", "re-consume", "widen". That shape had three problems: the standup phase
became a dumping ground (repo + meta-skill + core extraction + doc moves +
source-link genericization + plugin/marketplace all at once); it deferred every
real risk to the end (extract all 8-plus cores, *then* discover at the first
consumer that the extraction was wrong); and it described the core extraction
twice. The re-cut below front-loads infrastructure, then proves the entire pipeline
on **one** low-risk skill before touching the rest, then widens. It separates the
governance gate from any specific consumer.

### Phase 0 - in-repo refactor (DONE, merged in PR #174)

Outcome: the five oversized skills were split into thin cores + 13 sibling files
(`performance-testing` 18.5 -> 5.4 KB, `security-review` 15.4 -> 4.3 KB,
`publish-release` 14.4 -> 5.7 KB, `polyfill-dotnet-api` 14.4 -> 6.2 KB,
`pre-pr-self-review` 12.7 -> 8.5 KB); every core is now well under the 15 KB
budget. AGENTS.md was trimmed 16.6 -> 15.8 KB and the mirror regenerated;
`touki-reviewer` was thinned to references; `metadata.portability` was added to
all 12 skills with a README column; FORMAT.md documents the new fields and the
thin-core pattern; `polyfill-dotnet-api` gained the `microsoft-learn`
`compatibility:` line and graceful-degradation prose.

- [x] Split the five oversized skills into thin core + sibling files.
- [x] Trim AGENTS.md "General guidance" to pointers; regenerate the mirror.
- [x] Thin `touki-reviewer.agent.md` to references.
- [x] Add the `compatibility:` line + graceful-degradation prose to `polyfill-dotnet-api`.
- [x] Add `metadata.portability` to every skill and the catalog column.
- [ ] Deferred (optional): `context: fork` on the review/checklist skills.

Known limitation carried forward: AGENTS.md is 15.8 KB vs the 10 KB target. The
residual is the load-bearing, append-only "Working with the user on changes"
approval section; trimming further means cutting safety content, so it is left as
a separate decision (section 5).

### Phase 1 - build the `manage-skills` meta-skill in touki (DONE, PR #175)

The lifecycle skill (section 3) is the bootstrap tool, so it comes first - and it
can be built and used **in touki alone**, before any commons exists. Its `find` /
`build` / `update` logic degrades gracefully: with no commons yet, `find` searches
local + public sources, and the commons tier lights up once Phase 2 lands. This
delivers value immediately and is the lowest-risk possible start.

Outcome: authored [.agents/skills/manage-skills/](../.agents/skills/manage-skills/)
as a thin 5.3 KB core plus `find.md` / `build.md` / `update.md` siblings, each well
under budget. `build` enforces find-first; `find` is the tiered local -> commons
-> public search with the applicability check; `update` carries the pull (drift)
and push (common vs overlay) flows with the **ask-before-upstreaming** query and
the three-state golden rule (upstream / overlay / tracked pending-upstream
divergence). Added the catalog row and a `manage-skills` vs `agent-files-review`
disambiguation entry. The core is deliberately free of `docs/` and source links so
it migrates cleanly to the commons later (born-shared, built locally first).

- [x] Author `manage-skills` under touki's `.agents/skills/` following FORMAT.md
      (thin core + `find.md` / `build.md` / `update.md` siblings).
- [x] Implement find-first `build`, the tiered `find`, and the `update`
      pull/push + golden-rule classification (section 3).
- [x] Add its catalog row + disambiguation entry (vs `agent-files-review`).
- Validation: `Validate-AgentFiles.ps1` and `Test-AgentFileLinks.ps1` pass (47
      files). The live local-`find` tier was smoke-tested (a "benchmarking" query
      resolves to `performance-testing` via the catalog). The commons and public
      tiers await `gh auth login` and the Phase 2 commons repo, per the skill's
      "Current status" note.

### Phase 2 - stand up the commons skeleton (DONE)

Infrastructure only - no skill content yet, so nothing here can break a consumer.

Outcome: created the private repo
[JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) with the
MIT `LICENSE`, an adoption `README`, `FORMAT.md`, `plugin.json` +
`.github/plugin/marketplace.json` + `.mcp.json` (`microsoft-learn` http + NuGet
`dnx NuGet.Mcp.Server@1.4.3`), a placeholder `agents/` personas dir, and a CI
workflow (markdownlint + lychee + a guarded `skills-ref validate` that stays inert
until the first skill lands).

- [x] Create `JeremyKuhne/agent-skills` (private) with a `LICENSE` (MIT) and a
      short adoption README.
- [x] Add the distribution scaffolding: `plugin.json`, `marketplace.json`, and
      `.mcp.json` (`microsoft-learn`, NuGet MCP), plus a placeholder personas dir.
- [x] Stand up the commons' own CI (markdownlint, link check, guarded
      `skills-ref validate`).
- Validation: `gh` reaches the repo; `gh skill preview` returns a graceful
      "no skills found" against the empty repo; `dnx` (the NuGet MCP runtime) is
      present.

### Phase 3 - pilot one skill end-to-end (`security-review`) (DONE)

The de-risking vertical slice: take a **single** skill all the way through the
pipeline before touching the others. `security-review` is the ideal pilot - it is
universal, has no project gating, no source-example links, no backing `docs/`, and
no overlay siblings, so it isolates the *mechanism* (extract, vendor, pin, drift,
link-check) from the content complications the other skills add.

Outcome: extracted the portable `security-review` core into the commons (tag
`v0.1.0`), vendored it back into touki with `gh skill install --pin v0.1.0`, and
added a touki `overlay.md` for the dropped cross-references and example links. The
full loop works. Two findings the pilot surfaced, both folded back in:

1. **The commons publisher layout must be top-level `skills/`, not `.agents/skills/`.**
   `gh skill publish` only discovers `skills/*/SKILL.md` (and a few variants). The
   skeleton's `.agents/skills/` was wrong for a *publisher*; the commons now uses
   `skills/`. Consumers still vendor into their own `.agents/skills/` (where
   `gh skill install` places the copy). This is the clean split: publisher =
   `skills/`, consumer = `.agents/skills/`.
2. **Skill `description` frontmatter must be strict-YAML-safe for vendoring.**
   `gh skill`'s parser is stricter than touki's lenient flat parser: a bare
   colon-space inside an unquoted `description` (e.g. "...this repo's polyfill
   work: missing tests...") fails its YAML load. `security-review` happened to
   avoid it; `pre-pr-self-review` does not. Before Phase 4 bulk-extracts the rest,
   quote (or rephrase) any `description` containing a bare colon-space.

- [x] Extract `security-review`'s core into the commons; tag the commons `v0.1.0`.
- [x] Vendor it back into touki via `gh skill install --pin`, writing provenance
      frontmatter; touki's `Validate-AgentFiles.ps1` and `Test-AgentFileLinks.ps1`
      pass.
- [x] Exercise the drift check: `gh skill update --all` recognizes the provenance
      and skips the pinned skill; the `push` golden-rule flow is documented in the
      overlay and `manage-skills`.
- Validation: the full loop works on one skill with zero dangling links.

### Phase 4 - extract the remaining universal cores (the bulk)

With the pipeline proven, do the rest of the universal tier - this is where the
heavy content work lives.

**Progress.** `scratch-buffer-strategy` is the first completed Phase 4 increment
(commons `v0.2.1`, merged in PR #177): its core + bundled
`references/arraypool-performance.md` are vendored into touki with an overlay.
PR #177 deferred deleting the touki `docs/arraypool-performance.md` copy; this PR
collapses it - the `docs/` original is removed and README, AGENTS.md, and the
sibling perf docs now point at the single vendored reference.
`framework-jit-optimization` is the second (commons `v0.3.0`): its core + four
siblings + the bundled `references/framework-span-performance.md` (the portable
field manual split out of the class-C doc) are vendored with an overlay; the touki
`docs/framework-span-performance.md` is thinned to the `OrdinalIgnoreCase` worked
example plus a pointer up to the vendored field manual, and the live-guidance
links (README, AGENTS.md, coding_guidelines) are repointed. The remaining
universal cores are still to do.

**Remaining sequence and the `traceq` pause (revised 2026-06-06).** A separate
effort will productize the EventPipe trace tooling that currently lives in
`touki.mcp` as a standalone, installable analyzer (`traceq`; see its
implementation plan). That changes the order of the rest of Phase 4, because
exactly one remaining skill - `performance-testing` - is entangled with that
tooling, while the others are not. The revised order:

1. **`pre-pr-self-review`** - next, and a prerequisite for everything downstream.
   It clears the known YAML blocker (its `description` has a bare colon-space that
   breaks `gh skill`'s parser - see Phase 3 finding 2) and codifies the
   "every perf claim carries a benchmark or an explicit 'not measured'" convention
   that the `traceq` effort builds on. Split `polyfill-correctness.md` out as an
   **overlay** sibling (it drags ~10 touki source links; it must not ship in the
   shared payload).
2. **`create-pr`, `address-pr-feedback`, `agent-files-review`** - the workflow
   trio. All `traceq`-independent, all small (core + overlay, no large backing-doc
   splits). `agent-files-review` and the PR skills harden the exact files and flow
   the `traceq` effort's agent-built milestones run on.
3. **PAUSE the skills plan here** (see "The `traceq` seam" below) and stand up the
   `traceq` repository.
4. **Resume after `traceq`:** extract `performance-testing` **whole, in final
   form**, then proceed to Phase 5.

- [ ] Extract the `traceq`-independent universal cores in order:
      `pre-pr-self-review` (+ the `polyfill-correctness.md` overlay split and the
      `description` YAML fix), then `create-pr`, `address-pr-feedback`,
      `agent-files-review` - each host-neutral.
- [ ] **Defer `performance-testing`** until after `traceq` lands (see "The
      `traceq` seam"). Its BenchmarkDotNet core is portable, but its `profiling.md`
      sibling and its backing `docs/performance-investigation.md` are written
      around the `touki.mcp` analyzer that `traceq` replaces. Extracting it now
      means handling the skill twice (BDN core now, profiling rewrite at `traceq`
      M6); deferring means one clean extraction in final form.
- [ ] Move each shared core's generic backing doc into `<skill>/references/`:
      `arraypool-performance.md` -> `scratch-buffer-strategy/references/` (whole,
      class A) **[vendored in PR #177; touki `docs/` copy collapsed and references
      repointed in this PR]**;
      the portable span field-manual -> `framework-jit-optimization/references/`
      (split out of the class-C `framework-span-performance.md`) **[done, commons
      v0.3.0 - touki `docs/` copy thinned to the worked-example appendix]**. Leave
      touki-specific docs behind for the overlay.
- [ ] **Do not split `docs/performance-investigation.md`** (the old plan said to).
      It is ~40 KB and ~22 `touki.mcp` references - the `traceq` effort rewrites
      and single-sources it at M4/M6, so a split now is thrown-away work, and its
      genuinely-portable part (BDN run commands, result columns, regression
      thresholds) already duplicates the `performance-testing` siblings. It stays a
      touki doc, linked from the `performance-testing` overlay, until the `traceq`
      rewrite.
- [ ] Tag each sibling **portable** or **overlay**; move overlay siblings (most
      importantly `pre-pr-self-review/polyfill-correctness.md` and the
      example-heavy parts of `polyfill-dotnet-api`) out of the shared payload, and
      replace the 34 source example links in shared cores with generic "see the
      examples under your repo's `Framework/Polyfills/` tree" instructions
      (section 3, "The bigger dangling surface").
- [ ] Re-vendor each pinned core into touki; repoint touki's AGENTS.md and
      sibling-doc links to the vendored `<skill>/references/` copy; delete the old
      `docs/` originals (or leave the touki appendix for a split doc); update the
      catalog with `shared-source` provenance.
- Validation: full `Validate-AgentFiles.ps1` + `Test-AgentFileLinks.ps1` on touki;
      spot-check that each vendored core plus its overlay reads as one coherent
      skill.

### The `traceq` seam - pause the skills plan, stand up `traceq`

After the four `traceq`-independent cores above (`pre-pr-self-review`, `create-pr`,
`address-pr-feedback`, `agent-files-review`) are extracted and vendored, **pause
the skills rollout and begin the `traceq` effort.** This is the cleanest seam in
the roadmap:

- It leaves **every `traceq`-independent universal core extracted** - Phase 4 is
  complete except the one skill that is deliberately parked.
- `performance-testing` is **deferred intact, not left half-finished.** Its final
  shape depends on `traceq` (the profiling sibling and `performance-investigation.md`
  are `traceq`'s M6 rewrite targets). Touching it before then means handling the
  skill twice; deferring means one clean extraction in final form - stable BDN
  mechanics and `traceq`-based profiling together.
- It sits just before Phase 5 (the first *external* consumer), which is already the
  roadmap's biggest commitment boundary.

Why begin `traceq` *at* this seam and not earlier:

- `traceq`'s M0 scaffold seeds its `AGENTS.md` / `copilot-instructions` **from
  touki's**; the better-factored that surface is, the lighter the seed. The four
  workflow cores harden exactly that surface, and `agent-files-review` is the skill
  that governs it.
- `traceq`'s pre-PR convention ("every surface-text change carries an eval delta or
  an explicit 'not measured'") is the `pre-pr-self-review` convention; have it solid
  first.
- The `traceq` plan flags **solo-maintainer stall mid-extraction** as a top risk.
  Two large tracks competing for focus is the real cost - reach this documented
  clean seam, then commit to `traceq` rather than interleaving.

What resumes **after** `traceq` reaches parity (its M6):

- Extract `performance-testing` whole: the portable BDN core
  (`authoring` / `running` / `interpreting-results`) plus a profiling page now
  written against `traceq` (`dnx TraceQ.Mcp` / the `traceq` CLI) instead of
  `touki.mcp`, promoted into the commons core. `docs/performance-investigation.md`
  is rewritten through `traceq`'s single-source knowledge pipeline at the same time.
- Hand-off note for `traceq` M4: the commons `performance-testing` core and
  `traceq`'s own knowledge layer (its SKILL / AGENTS snippet) must single-source
  against each other, or the profiling prose becomes a third copy that drifts.
- Then Phase 5 (madowaku), which now *also* validates `traceq` consumption from a
  second repo - folding two acid tests into one.

### Phase 5 - first external consumer: madowaku (the full vet)

`madowaku` is the designated first consumer and the end-to-end vet; section 7
walks it in detail. Crucially, the `performance-testing` reconciliation here
*validates* the Phase 4 extraction rather than duplicating it - if the core was
extracted cleanly, madowaku's fork drops onto it with only an overlay.

- [ ] Reconcile the two `performance-testing` skills: confirm touki's extracted
      core (extracted **after** `traceq`, per the `traceq` seam) also subsumes
      madowaku's hand-forked copy; if not, the core still carried touki specifics -
      fix the core, not the consumer.
- [ ] Contribute madowaku's `cswin32-interop` / `cswin32-com` portable cores into
      the commons (skills flow both ways).
- [ ] Vendor the applicable shared cores into madowaku (section 6 table), each
      `--pin`ned, adding a per-repo overlay where needed.
- [ ] If fuzzing madowaku is wanted, vendor `fuzz-testing` and let its bootstrap
      stand up `madowaku.fuzz` from the skill's `assets/` template, named to match
      madowaku's flat convention and wired into `madowaku.slnx`.
- [ ] Run madowaku's `Validate-AgentFiles.ps1` and `Test-AgentFileLinks.ps1`; they
      must pass with zero dangling cross-references.
- [ ] Capture anything that needed per-repo editing; fold genuinely generic bits
      back into the cores and push repo-specific bits to overlays.

### Phase 6 - freshness and CI automation

Most valuable now that more than one repo vendors the cores.

- [ ] Add a report-only `gh skill update --all` drift job (or the tree-SHA script
      fallback) that opens a PR when a vendored core diverges from upstream.
- [ ] Add `skills-ref validate` over `.agents/skills/**` to each consumer's
      existing [agent-files.yml](../.github/workflows/agent-files.yml).
- [ ] Keep the existing 90-day freshness warning.

### Phase 7 - governance gate (before any external or public consumer)

Independent of any specific consumer, and a prerequisite for Phase 8. This is the
governance layer the 2026 guidance requires once a consumer is public or outside
your control (section 3, "Governance").

- [ ] Decide commons visibility (public vs private) and confirm the `LICENSE`.
- [ ] Turn on **immutable releases** and require **`--pin`ned** installs so a
      pinned tag cannot be silently rewritten.
- [ ] Confirm provenance + drift checks are enforced across consumers.

### Phase 8 - greenfield consumer: thirtytwo, and generalize the recipe

- [ ] Bootstrap `thirtytwo` as a greenfield consumer: stand up `.agents/skills/`
      + catalog + `FORMAT.md` + validator + `agent-files.yml`, then vendor the
      universal tier plus the `cswin32-*` domain skills (which apply there but not
      to touki).
- [ ] Teach the project-gated skills' bootstrap to resolve thirtytwo's deviant
      layout (`src/<root>`, `<root>_tests` underscore) rather than the flat dotted
      form, and record that mapping in thirtytwo's overlay. If `performance-testing`
      or `fuzz-testing` is vendored, stand up the matching project under `src/`.
- [ ] Generalize the per-repo overlay recipe so a new consumer (any owner) can
      adopt the commons from a short README, not tribal knowledge.

## 5. Open decisions

- **Shared repo name** - `JeremyKuhne/agent-skills` vs `dotnet-agent-skills` vs
  other. (It is a bidirectional commons, not a touki export - section 3.)
- **Lifecycle meta-skill name** - `manage-skills` vs `skill-lifecycle` vs
  `curate-skills` vs `skills-commons` (section 3). The trigger phrasing must catch
  "find a skill", "build a skill", and "update the skill" without over-firing on
  ordinary work.
- **`gh` adoption** - RESOLVED: `gh` 2.93 is installed locally (via winget) and
  `gh skill` is available. It still needs `gh auth login` (interactive) before
  `gh skill install/update` will work. The manual-vendor + drift-script fallback
  remains the offline option.
- **Audience breadth** - the commons now targets repos beyond `JeremyKuhne/*`
  (e.g. `thirtytwo`) and possibly public consumers. That makes the commons repo
  most naturally **public**, which raises the governance items below.
- **Commons visibility and license** - public vs private. If any consumer is
  public or outside your control, the commons needs a `LICENSE` (MIT, matching
  touki), `--pin`ned installs, and immutable releases so a pinned tag cannot be
  silently rewritten. Private is fine while every consumer is yours.
- **Portable agent personas** - worth publishing given uneven agent-host support,
  or keep agents touki-local for now and share only skills + MCP.
- **Referenced-doc move vs split** - the content audit settled most of this:
  `arraypool-performance.md` (28 KB, class A) moved whole into
  `scratch-buffer-strategy/references/` (vendored in PR #177; the touki `docs/`
  copy was deleted and all references repointed to the vendored reference in this
  PR); the
  class-C docs
  (`framework-span-performance.md` ~17/4, `polyfill-layout.md` ~2.5/2.5,
  `performance-investigation.md` ~16/23) split, with the portable field-manual in
  `references/` and the touki appendix left in `docs/`. The remaining judgement
  call is `performance-investigation.md`: its 16 KB portable profiling core is
  worth extracting only if `performance-testing`'s profiling sub-page is actually
  shared - otherwise leave it whole in `docs/` as a touki overlay link. Splitting
  keeps the vendored payload small but adds a maintenance seam.
- **Deviant project layouts: normalize vs detect** - `thirtytwo` uses
  `src/<root>` + `<root>_tests` (underscore) instead of the flat dotted
  convention. Either normalize it toward the convention (invasive, and its
  `_tests` heritage may be load-bearing) or teach every project-gated skill's
  bootstrap to detect the host layout and match it. Detection is the lower-risk
  default; normalization is a one-time cleanup if the divergence proves costly.
- **Cloud-agent MCP** - only needed if polyfill work runs on the cloud agent;
  defer until that happens.
- **msbuild.instructions.md (9.4 KB)** - out of scope here, but it is over its
  8 KB budget and is a candidate for a later split.

## 6. Applicability matrix (appendix)

Two axes per skill (portability, applicability tier) and the concrete per-repo
answer for the three known repos. `yes` = vendor it; `no` = never; `marginal` =
only if that domain grows in the repo; `overlay` = shared core plus a per-repo
overlay; `bootstrap` = applicable, but the gated project does not exist yet, so
vendoring the skill stands it up on first use (section 3); `partial` = some of it
applies. thirtytwo answers assume its scaffolding is stood up first (Phase 8).

| Skill | Portability | Applicability | touki | madowaku | thirtytwo |
| ----- | ----------- | ------------- | ----- | -------- | --------- |
| `security-review` | semi-portable | universal | yes | yes | yes |
| `pre-pr-self-review` | semi-portable | universal | yes | yes | yes |
| `create-pr` | semi-portable | universal | yes | yes | yes |
| `address-pr-feedback` | repo-specific | universal | yes | yes | yes |
| `agent-files-review` | semi-portable | universal | yes | yes | yes |
| `manage-skills` (section 3) | semi-portable | universal | yes (built) | yes | yes |
| `performance-testing` | semi-portable | universal, project-gated (`<root>.perf`) | overlay | overlay | bootstrap |
| `filtrace` (from filtrace) | vendored (tool repo) | dotnet-profiling | yes | yes | yes |
| `scratch-buffer-strategy` | semi-portable | dotnet-perf | yes | yes | yes |
| `framework-jit-optimization` | semi-portable | dotnet-framework | yes | yes | marginal |
| `polyfill-dotnet-api` | repo-specific (small core) | dotnet-framework-polyfill | yes | yes | marginal |
| `publish-release` | repo-specific | repo-local | yes | partial | marginal |
| `fuzz-testing` | semi-portable | domain, project-gated (`<root>.fuzz`) | yes | bootstrap | bootstrap |
| `run-tests-on-wsl` | repo-specific | repo-local | yes | no | no |
| `cswin32-interop` (from madowaku) | semi-portable | cswin32 | marginal | yes | yes |
| `cswin32-com` (from madowaku) | semi-portable | cswin32-com | **no** | yes | yes |

The `cswin32-com` row is the headline: one skill, three repos, three answers
(never touki; yes for both CsWin32 repos). That is the applicability axis, and it
is enforced by not vendoring the skill into touki - not by any field a host
reads. The `fuzz-testing` row is the second lesson: its `bootstrap` answers are
not "no" - the skill applies to madowaku and thirtytwo, it just stands up
`madowaku.fuzz` / the thirtytwo equivalent on first use (section 3). Only
`run-tests-on-wsl` is a true `no` for the other repos.

## 7. Full vet: consuming the commons in real repos

[`JeremyKuhne/madowaku`](https://github.com/JeremyKuhne/madowaku) is the
designated first consumer and the end-to-end test of this plan. It is a near-twin
of touki - a strong-named (`klutzyninja.snk`), multi-targeted library on the
**same TFMs** (`net10.0` modern, `net472` framework, benches on `net481`), with
the same `Directory.Build.props` shape, the same `tools/Validate-AgentFiles.ps1`
+ `.github/workflows/agent-files.yml` gate, the same `.agents/skills/` + catalog
+ `FORMAT.md` scaffolding, and a `madowaku/Framework/` polyfill tree. It differs
in exactly the ways that make it a good vet:

- It is a **Win32 interop library** (CsWin32: `NativeMethods.txt`,
  `madowaku/Windows/`) and it **consumes the `KlutzyNinja.Touki` package** - so
  touki is both a dependency and a skill-sharing sibling.
- It has **no `testsupport`, `fuzz`, `mcp`, or `msbuildshim` projects**, a
  **single** publish stream, and a Windows-flavored modern TFM moniker
  (`net10.0-windows10.0.22000.0`).
- Its `.github/instructions/` set is **different**: `interop`, `msbuild`, `tests`
  - no `perf` or `polyfills`.

### What madowaku already has

Its catalog already lists three skills: two domain skills touki has no use for
(`cswin32-interop`, `cswin32-com`) and a **hand-forked `performance-testing`**
whose header literally reads "adapted from Touki's performance-testing skill and
narrowed to madowaku conventions." That single fact is the most valuable input to
this vet: the shared-core idea is not theoretical here, it is a divergence that
**already happened** and must be reconciled.

### The acid test: reconcile the two `performance-testing` skills

Diffing touki's split core against madowaku's 4.6 KB monolithic copy yields a
clean separation, which is the proof the "core" is real:

- **Genuinely common (-> shared core):** BenchmarkDotNet hosts the benches; one
  `public` class per file; `[MemoryDiagnoser]` always; `[Benchmark(Baseline=true)]`
  for comparisons; the return-a-value / no-`void` dead-code-elimination rule;
  setup in `[GlobalSetup]`; avoid helper indirection; `-f <tfm>` mandatory on a
  multi-targeted project; `-c Release` mandatory; run both TFMs; read `Allocated`
  / `Gen0-2`; weigh `Ratio` with `Allocated`. Both even share `net481` as the
  framework bench moniker.
- **Per-repo (-> overlay):** project name (`touki.perf` vs `madowaku.perf`) and
  namespace; the modern TFM moniker (`net10.0` vs `net10.0-windows10.0.22000.0`);
  domain examples (StoreInteger / string formatting vs VARIANT conversion);
  cross-references (`framework-jit-optimization` + `scratch-buffer-strategy` vs
  `cswin32-*`); profiling tooling (touki's `touki.mcp` analyzer is touki-only;
  madowaku adds `--disasm`); `argument-hint` frontmatter (madowaku sets one,
  touki does not).

If both repos can drop their forks, vendor one pinned shared core, add a ~20-line
overlay, and still pass their own `Test-AgentFileLinks.ps1`, the core is proven
generic. If the link check fails, the "core" still carried repo-specific content
- the failure is the test working.

### What the vet changes about the plan

Walking madowaku surfaced four corrections, now folded into section 3:

1. **The flow is bidirectional.** madowaku contributes `cswin32-*`; the design is
   a commons, not "touki exports skills" (section 3, TL;DR #3).
2. **Cross-references must live in the overlay, not the core** - touki's xrefs
   dangle in madowaku and vice-versa, and both repos' link-check CI would fail.
3. **Project names, paths, and TFM monikers must live in the overlay** - the core
   uses placeholders (`-f <tfm>`, "the perf project").
4. **Vendoring is a merge, not a greenfield drop** - madowaku already has a
   catalog, `FORMAT.md`, a divergent skill, and a *different* instruction set, so
   a core linking to `perf.instructions.md` / `polyfills.instructions.md` breaks
   there.

### Second consumer: thirtytwo (greenfield, and the applicability axis)

[`JeremyKuhne/thirtytwo`](https://github.com/JeremyKuhne/thirtytwo) is "an
experiment wrapping Win32 into an object model, leveraging CsWin32 as the base
interop layer." Two things make it the complement to the madowaku vet:

- **It is greenfield.** It has `.github/` but no `AGENTS.md` and no `.agents/`,
  so it exercises the bootstrap path (stand up scaffolding, then vendor) that
  madowaku's merge path does not.
- **It makes the applicability axis concrete.** Because thirtytwo is a CsWin32 UI
  object model, `cswin32-interop` *and* `cswin32-com` clearly belong there - the
  same `cswin32-com` skill that must **never** be vendored into touki. One skill,
  three repos, three answers: yes (thirtytwo, madowaku), no (touki). That is
  applicability, enforced by choosing not to install the skill in touki, not by
  any flag the host reads.

thirtytwo is under the same owner today, but the design no longer assumes that:
the audience explicitly includes repos beyond `JeremyKuhne/*` and possibly public
consumers, which is what promotes the governance items (license, pinning,
immutable releases) from optional to required (section 3).

### Vet verdict

The plan survives both consumers, with one framing fix (a bidirectional commons,
not a touki export), one conceptual addition (portability and **applicability**
are separate axes, the latter enforced by selective vendoring), and the three
core-content rules in section 3 promoted from "nice to have" to **enforced by the
consumer's CI**. Phase 5 is a concrete madowaku reconciliation and Phase 8 is the
thirtytwo bootstrap plus the audience-broadening (section 4). The skills that do
**not** port - `fuzz-testing`, `run-tests-on-wsl`, the dual-stream half of
`publish-release` - are exactly the ones tied to projects a given consumer lacks,
and the CsWin32 skills that do not port *to touki* are the applicability axis
working as intended, not a defect.
