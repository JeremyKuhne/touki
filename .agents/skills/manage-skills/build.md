# Build a skill

Detail for the [manage-skills](SKILL.md) skill. "Build a skill for X" / "create a
skill" does **not** start by writing a new skill. It starts by finding one.

## The find-first decision tree

Reinventing a skill that already exists - in this repo, the commons, or a public
catalog - is the failure mode this path exists to prevent. Always run
[find.md](find.md) first, then act on the result:

### 1. Run find

Run [find.md](find.md) before writing anything.

### 2. Already installed at the requested host and scope

Do not build. If it does not quite fit, follow [update.md](update.md) and classify
the change before editing any installed copy. A hit at another scope is not
equivalent; route the existing source through [install.md](install.md).

### 3. In the commons

Do not build. Install it and add a thin overlay for project- or user-specific
paths and cross-references. Choose scope and required hosts with
[install.md](install.md). For a Copilot project copy:

```pwsh
gh skill install JeremyKuhne/agent-skills <skill> --pin vX.Y.Z `
  --agent github-copilot --scope project
```

Read the selected revision's `metadata.requires` and install its complete
transitive requirement closure at the same pin; `gh skill install` installs only
the named skill. Review each requirement's applicability and source before
installing it.

`--pin` records the exact version so later updates skip it until you deliberately
re-pin. The install reserializes `SKILL.md` frontmatter and adds provenance
metadata (source repo, ref, pin, path, and tree SHA). Commit a project copy; keep
a user copy outside project source control. Compare installed artifacts with the
normalized mirror contract in [update.md](update.md), not a raw `SKILL.md` file
hash.

Without `gh`, check out or download the exact tag/commit, copy the complete
directory for the skill and each transitive requirement (not only each
`SKILL.md`), preserve or add provenance metadata for that immutable revision,
add the local overlay, and compare every copied file list and hash against the
source before running the validators. If an exact revision or complete file set
cannot be obtained, keep installation blocked.

### 4. In a public catalog

Do not build from scratch. Apply the security gate below. If it is good, install
it at the selected scope; if it is close but imperfect, fork it into the commons
and install that. A mediocre public skill is usually worth adapting over a blank
start.

### 5. Nowhere

Build new using the next section.

## Security gate for public sources

Public skills are an instruction-injection supply chain - audits have found a
meaningful fraction carry a critical issue (prompt injection, malicious scripts,
exposed secrets). Before installing anything from a public source:

- **Preview, do not blind-install:**
  `gh skill preview <owner/repo> <skill>@<full-commit-sha>` and read the
  `SKILL.md`, every script, and every `references/` file - not just the summary.
- **Pin** to a tag or commit SHA; never track a moving ref.
- **Never accept `allowed-tools` from a third party**, especially `shell` / `bash`
  - it removes the per-command confirmation. Strip it on import and let the host
  prompt.
- **Prefer provenance-bearing sources** (the curated registry, verified
  publishers) over a random repo from a blog post.
- Treat a cloned repo's `.agents/` as untrusted code: opening it can load skills
  into a trusted session.

## Building a new skill (it exists nowhere)

Author it to the repo's `FORMAT.md`:

- A thin `SKILL.md` core under the size budget; push deep detail into sibling
  `*.md` files in the same directory (the pattern this very skill uses).
- `name` matches the directory; a "pushy" `description` with explicit trigger
  phrasing that will auto-invoke on the right asks without over-firing.
- Set every portfolio metadata field (`portability`, `applicability`, `binding`,
  `risk`, `maturity`, `requires`, and `related`) using the repo's `FORMAT.md`.
- For an overlay-aware core, include the standard loader sentence. Create a
  downstream overlay from `assets/overlay.md.tmpl`; replace `{{SKILL_NAME}}` and
  `{{CORE_PIN}}`, then add only repository-specific bindings.
- Add a row to the catalog `README.md` inventory in the same change, and a
  disambiguation entry if the trigger phrasing competes with an existing skill.
- Once behavior and routing are complete, run `technical-writing` in revise
  mode over the human- and agent-facing text. Preserve literal trigger phrases,
  normative force, tool and file names, permission boundaries, and stop
  conditions. Do not let a clarity edit change the workflow silently.
- Validate the `SKILL.md` frontmatter with the bundled
  [scripts/Validate-Skills.ps1](scripts/Validate-Skills.ps1) in strict portfolio
  mode, then run the repo's remaining agent-file checks (the installed-artifact
  link check, markdown lint, and generated catalog check).

Before calling the skill complete, select and verify its runtime targets with
[install.md](install.md), run the semantic workflow review in
[review.md](review.md), then hand the resulting files to `agent-files-review` for
frontmatter, links, whitespace, and repository diagnostics. A validator pass does
not establish that the skill will invoke at the right time or lead an agent to a
finished outcome.

### Canonical source ownership

Decide where the canonical source lives before writing much. This is independent
of where runtime copies will be installed:

- **Born-repository** - the skill is specific to this repo (its paths, projects,
  or one-off workflow). Author it in a supported project root and leave it; it
  never goes to the commons.
- **Born-personal** - the skill is specific to one person (voice, private context,
  preferences, or an individual workflow). Author it in a local-only directory or
  controlled private source, then install at user scope. Do not put it in a public,
  shared, organization, plugin, or project distribution path.
- **Born-shared** - the skill is generic and other repos will want it. First ask
  the user whether to pursue commons authoring, as required by
  [update.md](update.md).
  Prepare and validate the portable core in a commons checkout or local staging
  branch, but do not create a remote branch, push, or open a PR without the
  repository's explicit approvals. Once the shared change is merged and released,
  vendor that immutable revision back here with an overlay. Keep repo-specific
  paths, cross-references, and example links out of the core from the start - they
  belong in the overlay. Leave a short prose cue in the core telling the agent to
  read `overlay.md` when present; that stable loader contract is what gets the
  overlay read.

A skill that is mostly generic but needs a few project or user specifics is still
born-shared: the generic part is the core, the specifics are an installation-local
overlay. The test is whether another consumer would want the core unchanged.
