# Build a skill

Detail for the [manage-skills](SKILL.md) skill. "Build a skill for X" / "create a
skill" does **not** start by writing a new skill. It starts by finding one.

## The find-first decision tree

Reinventing a skill that already exists - in this repo, the commons, or a public
catalog - is the failure mode this path exists to prevent. Always run
[find.md](find.md) first, then act on the result:

1. **Run find.**
2. **Already in this repo** -> do not build. If it does not quite fit, add or edit
   the repo's **overlay** (see [update.md](update.md)), never the vendored core.
3. **In the commons** -> do not build. Vendor it and add a thin overlay for any
   repo-specific paths or cross-references:

   ```pwsh
   gh skill install JeremyKuhne/agent-skills <skill> --pin vX.Y.Z
   ```

   `--pin` records the exact version so later `update --all` runs skip it until
   you deliberately re-pin. The install writes provenance frontmatter (source
   repo, ref, and tree SHA); commit it.
4. **In a public catalog** -> do not build from scratch. Apply the security gate
   below. If it is good, vendor it; if it is close but imperfect, fork it into the
   commons and vendor that. A mediocre public skill is usually worth adapting over
   a blank start.
5. **Nowhere** -> build new (next section).

## Security gate for public sources

Public skills are an instruction-injection supply chain - audits have found a
meaningful fraction carry a critical issue (prompt injection, malicious scripts,
exposed secrets). Before installing anything from a public source:

- **Preview, do not blind-install:** `gh skill preview <owner/repo> <skill>` and
  read the `SKILL.md`, every script, and every `references/` file - not just the
  summary.
- **Pin** to a tag or commit SHA; never track a moving ref.
- **Never accept `allowed-tools` from a third party**, especially `shell` / `bash`
  - it removes the per-command confirmation. Strip it on import and let the host
  prompt.
- **Prefer provenance-bearing sources** (the curated registry, verified
  publishers) over a random repo from a blog post.
- Treat a cloned repo's `.agents/` as untrusted code: opening it can load skills
  into a trusted session.

## Building a new skill (it exists nowhere)

Author it to [FORMAT.md](../FORMAT.md):

- A thin `SKILL.md` core under the size budget; push deep detail into sibling
  `*.md` files in the same directory (the pattern this very skill uses).
- `name` matches the directory; a "pushy" `description` with explicit trigger
  phrasing that will auto-invoke on the right asks without over-firing.
- Set `metadata.portability`.
- Add a row to the catalog [README.md](../README.md) inventory in the same change,
  and a disambiguation entry if the trigger phrasing competes with an existing
  skill.
- Validate: `pwsh tools/Validate-AgentFiles.ps1` and
  `pwsh tools/Test-AgentFileLinks.ps1`.

### Born-local vs born-shared

Decide where the skill's home is before writing much:

- **Born-local** - the skill is specific to this repo (its paths, projects, or
  one-off workflow). Author it here under `.agents/skills/` and leave it; it never
  goes to the commons.
- **Born-shared** - the skill is generic and other repos will want it. Author the
  portable core directly in the commons, then vendor it back here with an overlay.
  Keep repo-specific paths, cross-references, and example links out of the core
  from the start - they belong in the overlay. (Until the commons exists, author
  it here, keep the core clean, and migrate it when the commons is stood up.)

A skill that is mostly generic but needs a few local specifics is still
born-shared: the generic part is the core, the specifics are the overlay. The test
is whether another repo would want the core unchanged.
