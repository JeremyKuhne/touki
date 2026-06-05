# Find a skill

Detail for the [manage-skills](SKILL.md) skill. Answers "find a skill for X" /
"is there a skill that does X" with a tiered search and a per-repo applicability
check, then a short recommendation.

## Tiered search (nearest trust first)

Search in order and stop reporting once you have the picture; do not skip a tier,
because a local hit changes the recommendation entirely.

1. **Local - this repo's `.agents/skills/`.** List the skill directories and read
   the catalog [README.md](../README.md) inventory and disambiguation. A match
   here means the skill is already present; the answer is "you have it", and the
   only question is whether it needs an overlay tweak. No tooling required.

2. **The commons (pending).** Once the shared skills repo exists, search it:

   ```pwsh
   gh skill search <terms> --repo JeremyKuhne/agent-skills
   ```

   Anything here is curated and pre-vetted. Until the commons is stood up, skip
   this tier (see "Current status" in [SKILL.md](SKILL.md)).

3. **Public catalogs.** Search the wider ecosystem - the awesome-copilot
   collection, `anthropics/skills`, and the registry:

   ```pwsh
   gh skill search <terms>
   ```

   Public results are **untrusted by default**. Do not recommend installing one
   without the security gate in [build.md](build.md). When `gh` is unavailable,
   fall back to browsing the catalogs in a browser and note that the install path
   will be manual.

## Applicability check

A skill existing is not the same as a skill belonging here. Before recommending a
vendor, judge whether the skill's domain applies to **this** repo:

- A domain skill (for example a CsWin32 COM skill) is irrelevant in a repo that
  does no COM, even though it is a perfectly good skill. Say so; do not recommend
  vendoring something that will never fire.
- A project-gated skill (one that drives a sibling project such as a fuzz or perf
  project) applies if the repo has that project *or should have it*. If the
  project is missing, note that vendoring the skill can stand it up (see the
  bootstrap path in [build.md](build.md)).
- A repo-local skill from another repo (tied to that repo's unique structure)
  does not transfer; flag it as out of scope.

## Recommendation report

Output a short summary, not a raw search dump:

- **Where it exists:** local / commons / public / nowhere.
- **Applicable here:** yes / no / only if a gated project is added, with the
  one-line reason.
- **Recommended action:** use it (already local) / tweak the overlay / vendor from
  the commons / evaluate-and-vendor from public / build new / skip as
  inapplicable.

That recommendation is the hand-off into [build.md](build.md), which turns "vendor"
or "build new" into concrete steps.
