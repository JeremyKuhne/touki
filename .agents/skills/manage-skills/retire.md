# Retire a skill

Detail for the [manage-skills](SKILL.md) skill. Use for "retire this skill",
"remove this skill", or when a review finds that a skill is obsolete, duplicated,
or replaced.

Retirement is dependency-first. Deleting a directory before rerouting its consumers
leaves stale triggers, broken `requires`/`related` metadata, catalog entries,
packaging lists, validators, and generated instructions.

## 1. Establish the reason and replacement

Record why the skill is leaving and choose one disposition:

| Disposition | Use when | Outcome |
| --- | --- | --- |
| Replace | Another skill owns the workflow better | Reroute consumers, then remove the old skill. |
| Deprecate first | External consumers need migration time | Document the replacement and migration in the source catalog/release before later removal. |
| Remove now | The skill is local, unused, unsafe, or never applicable | Delete it after proving no live dependency remains. |

Do not invent unsupported `deprecated` frontmatter. Express deprecation through the
source catalog/release and narrowed routing until removal.

## 2. Inventory dependents, installations, and ownership

Search for the skill name, directory path, and workflow vocabulary in:

- catalog inventory and disambiguation;
- `requires`, `related`, descriptions, overlays, prompts, agents, and instructions;
- CI/validator expected-skill lists, generated catalogs, packaging include/remove
  lists, docs, tests, and eval tasks;
- repository scripts or examples that invoke the skill directly.

Use repository text/reference search rather than relying on a fixed path list. Skill
catalogs and expected inventories are commonly under `.agents/`, `.github/`, `tools/`,
package/project files, and contribution docs, but consuming repositories may bind
different locations in their overlays.

Also inventory every runtime target from [install.md](install.md): project,
inherited, user, plugin/managed, and custom directories for each host. Use host
skill listings to find registered directories and duplicate names. Classify each
target as a copy, registered source, symlink, plugin, or canonical source before
removing anything.

Classify ownership before acting:

- Removing a vendored skill from one consuming repo is a local reversible edit, but
  still requires the user's current request to authorize removal; it does not remove
  the upstream skill.
- Retiring a commons core is a shared breaking/lifecycle decision. Stop and obtain
  explicit user approval before creating an upstream branch or deleting shared
  source; pushing, opening a PR, releasing, or merging each remains separately gated
  by the repository's publishing policy.
- Remove any pending-upstream divergence record for the skill when the local copy
  leaves.
- Removing a user installation is not permission to delete its canonical private
  source. Unregistering a directory is not the same as deleting its files.

For Copilot CLI, a custom directory path unregisters the source and leaves its
files in place:

```pwsh
copilot plugins remove ./path/to/skill-directory --skill
```

Removing by skill name can delete a copied personal or project skill. Confirm the
install mode and pass the registered path when canonical files must survive.

## 3. Reroute before deletion

Update callers and neighboring skill boundaries first. The replacement description
and catalog disambiguation must cover valid requests previously owned by the retiring
skill without broadening into unrelated work. Remove or replace dependency metadata;
do not leave a `related` or `requires` edge to a missing skill.

If there is no replacement, state which requests become unsupported and why. For an
unsafe public import, remove execution permissions and references in the same change.

## 4. Remove lifecycle state

After rerouting is reviewable:

If the disposition is **Deprecate first**, publish the approved migration notice and
validate the replacement path, then stop. Keep the deprecated skill and its required
lifecycle state until the separately approved removal milestone.

For **Replace** or **Remove now**:

1. remove each approved runtime copy or registration at project/user/plugin scope;
2. delete the project core and overlay only when that project owns the source;
3. remove catalog rows and obsolete disambiguation;
4. update expected-skill lists, package manifests, generated collateral, docs, tests,
   and evals;
5. remove pin/provenance and pending-divergence records owned only by that skill;
6. regenerate mirrors/catalogs through their owning tools rather than hand-editing
   generated files.

Run only generators documented by the canonical source or local overlay. If no
owning tool exists for a collateral file, record it as not applicable or update
the hand-owned file directly; do not guess a command or silently skip known
generated output.

For a personal/private skill, also apply its retention policy to staging copies,
backups, sync locations, logs, and caches. Do not claim secure deletion unless the
storage and backup systems actually provide it.

Do not remove unrelated historical release notes merely because they mention the
skill; preserve history unless it still acts as live routing or instructions.

## 5. Prove closure

Run a semantic review of the replacement and neighboring trigger domains using
[review.md](review.md). Exercise one request formerly owned by the retired skill and
one near-neighbor that should not route to the replacement. Then invoke
`agent-files-review` for file-level validation and run repository validators, link
checks, generated-catalog checks, packaging checks, and upstream mirror checks.

The retirement is complete when no live dependency or routing reference points
to the removed skill, host listings no longer show the retired target, the
replacement path reaches an observable outcome, and expected project/user
inventories match disk. Report the disposition, removed and retained scopes,
canonical-source outcome, replacement or unsupported scope, validation evidence,
and any external migration still pending.
