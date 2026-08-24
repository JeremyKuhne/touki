# Review a skill

Detail for the [manage-skills](SKILL.md) skill. Use this workflow for "review this
skill", "is this skill effective", or as the semantic review pass after building or
updating a skill.

This page reviews whether a skill will invoke appropriately and lead an autonomous
agent through a complete, maintainable workflow. It deliberately does **not** repeat
frontmatter, schema, whitespace, link, or diagnostic checks. Those belong to
`agent-files-review`, which runs after this review.

## Choose the review scope

| Change | Review emphasis |
| --- | --- |
| New skill | Need, trigger boundaries, full workflow, born-local/shared placement |
| Existing skill effectiveness | Missed/false invocation, agent stalls, excessive context, missing stop/recovery paths |
| Vendored update | Behavioral drift, new dependencies, overlay compatibility, provenance/pin disposition |
| Local core edit | Common vs local classification and upstream/overlay/pending-divergence outcome |
| Installation/scope change | Canonical source, hosts, scope, copy/register mode, precedence, privacy |
| Portfolio review | Trigger overlap, catalog disambiguation, duplicate skills, missing prerequisites |

Review the smallest relevant surface: catalog row and neighboring trigger domains,
`SKILL.md`, overlay, then only the detail pages/scripts reached by the workflow under
review. Do not map every sibling file when one branch is changing.

- **New skill:** use every section.
- **Effectiveness issue:** emphasize evidence, routing, execution, and progressive
  disclosure; use portability/validation only where the fix touches them.
- **Vendored update or local core edit:** emphasize routing drift, portability,
  ownership disposition, overlay compatibility, and validation.
- **Portfolio review:** repeat routing/applicability checks across neighboring skills;
  do not deep-read unrelated recipes.

## 1. Establish evidence before judging prose

Start from the concrete reason for review: user feedback, a missed invocation, an
agent transcript, a changed file, validator failure, or a workflow that stalled.
Write one falsifiable hypothesis (for example, "the description never names the
user's phrase" or "the update path does not dispose of local core drift") and the
cheapest check that can disprove it.

When no failure prompted the review, exercise representative requests mentally or
with the repository's eval harness:

- one request that should invoke the skill;
- one near-neighbor that should route elsewhere;
- one ordinary happy path;
- one missing prerequisite or failed tool path;
- one completion path that proves the user's outcome, not merely file creation.

## 2. Review invocation and portfolio routing

- **Description:** names concrete user phrases and outcomes, not only the domain.
- **Boundaries:** says what routes elsewhere when adjacent skills overlap.
- **Catalog:** inventory and disambiguation agree with the description.
- **Applicability:** project-gated prerequisites are present or explicitly separate
  setup work; irrelevant skills are not vendored "just in case".
- **Dependencies:** `requires` and `related` match actual workflow routing; optional
  skills have a local fallback instead of a dead end.
- **Installation:** target hosts and scope match the audience, duplicate names
  resolve to the intended source, and a project/user hit is not mistaken for the
  other scope.

Treat false positives and false negatives separately. Broader trigger wording is not
automatically better: it may steal work from a more precise skill.

## 3. Review agent execution

An effective skill lets an agent decide, act, validate, and stop without inventing
missing policy.

- The entry point exposes a fast decision path; deep detail is linked, not front-loaded.
- Preconditions, permissions, irreversible actions, and secret boundaries are explicit.
- Decision branches name the next action and the evidence that selects it.
- Happy path, absent dependency, malformed/failed output, and cleanup/recovery paths
  are distinguishable where policy differs.
- Expensive workflows (for example performance runs or repository-wide audits) have
  budgets, escalation gates, and hard stop conditions.
- Validation follows the first edit and scales with risk.
- Completion is an observable user outcome with a concise report of evidence and
  remaining gaps.
- Installation workflows distinguish copying, registering a source, symlinking,
  and plugin/managed distribution. Verification proves the effective source
  path, not merely that files were created.

Flag instructions that only say "consider", "review", or "ensure" without naming a
check, owner, or next action. Also flag unconditional guarantees whose invariant is
not stated or tested.

## 4. Review progressive disclosure and cognitive load

- Keep `SKILL.md` focused on triggers, non-negotiable rules, workflow selection, and
  links to detail.
- Put long recipes, references, platform variants, and uncommon recovery procedures
  in purpose-named sibling pages.
- Prefer one canonical rule with short pointers over duplicated checklists that can
  drift.
- Use tables for routing/selection, numbered steps for sequence, and checklists for
  independent gates.
- Keep vocabulary stable across the core, overlay, catalog, scripts, and output.
- Remove historical narrative unless it changes a current decision; retain durable
  lessons as rules with the boundary that makes them true.

Detail is useful when it changes agent behavior. It is harmful when an agent must
retain unrelated branches in working memory before choosing the first action.

## 5. Review portability and lifecycle placement

- Portable behavior belongs in the core; repository paths, commands, defaults, and
  local policy belong in the overlay.
- Source ownership and runtime scope are orthogonal. A portable core may be a
  user install; a born-personal skill may have no repository home. Reclassifying
  scope does not reclassify ownership.
- A new skill is born-shared when another repository would want the core unchanged;
  otherwise it is born-local.
- A vendored core edit ends upstreamed, moved to the overlay, or recorded as a
  pending-upstream divergence with its files, base pin, reason, and upstream status.
  Never accept unexplained drift.
- Re-review overlays when a core pin changes; a syntactically valid overlay can bind
  obsolete assumptions.
- Public-source imports pass the security gate in [build.md](build.md), retain an
  immutable pin, and do not inherit third-party tool permissions blindly.
- Personal skills pass the privacy gate in [install.md](install.md). Review name
  and description exposure, invocation-time model disclosure, script
  permissions, source/destination visibility, synchronization, logs, and remote
  availability.

## 6. Review validation and maintenance

Require the narrowest semantic check that can fail the changed behavior: routing
examples, eval tasks, fake adapters, script contract tests, or a representative dry
run. Then invoke `agent-files-review` for file-level validation and run the consuming
repository's validator, link checker, markdown checks, generated-catalog checks, and
upstream mirror check as applicable.

After semantic findings are resolved, run `technical-writing` in review mode on
the changed skill prose. Treat changed triggers, requirement strength, commands,
permissions, or stopping behavior as semantic changes that must return to the
earlier review sections; do not accept them as style edits. Prose quality cannot
make an incomplete or unsafe workflow correct.

For an install/scope change, exercise one project request, one user request, one
duplicate-name case, and one private or remote-session boundary. Use the host's
skill listing to confirm the active source path and scope.

Treat an unresolved ownership/scope mismatch or duplicate name as a blocking
finding, not residual risk. For a pin change, require a disposition for every
overlay and pending divergence at the candidate pin.

For a changed vendored skill, verify both sides:

- local normalized mirror comparison against the provenance pin makes
  additional drift visible without treating generated provenance or YAML
  serialization as a core edit;
- upstream comparison shows whether each recorded divergence remains at the candidate
  pin, and records are removed once that artifact contains the change.

Run the applicable cases in [evaluations.md](evaluations.md) before declaring the
semantic review complete.

## Review output

Lead with findings ordered by severity and grounded in file/section references.
Separate:

1. invocation/routing defects;
2. workflow, safety, or completion defects;
3. portability/lifecycle defects;
4. progressive-disclosure and maintainability issues;
5. missing semantic tests or evidence.

Then state the ownership disposition (core, overlay, upstream, or pending
divergence), installation targets and scope when applicable, the
`agent-files-review` result, commands run, and residual risk. If no issues remain,
say so explicitly rather than manufacturing style churn: "Semantic review
complete; no invocation, workflow, portability, or lifecycle defects found.
Proceeding to agent-files-review."
