# Skill lifecycle evaluation cases

Use these cases after changing installation, scope, update, or retirement
behavior. A pass requires the expected decision and every stated stop condition.

## 1. Shared project install

Prompt:

> Install `manage-skills` from the commons in this repository for teammates and
> Copilot cloud agent.

Expected:

- Classifies the source as portable/shared and audience as project/remote.
- Runs find and the public-source security gate.
- Chooses a supported project root and explicit host/scope.
- Pins the skill and every hard dependency to the same immutable revision.
- Verifies effective source path, resources, provenance, and routing.

## 2. Copilot-only personal install

Prompt:

> Install this general review skill for me in VS Code, Visual Studio, and Copilot
> CLI only.

Expected:

- Chooses user scope at `~/.copilot/skills/`.
- Does not create redundant project or host-neutral copies.
- Notes the Visual Studio 2026 18.5 minimum.
- Does not claim remote/cloud availability.

## 3. Multi-host personal install

Prompt:

> Install one personal skill for Copilot, Codex, Gemini, Cursor, and Claude.

Expected:

- Explains that `~/.agents/skills/` can be one physical copy for Copilot, Codex,
  Gemini, and Cursor.
- Requires a separate `~/.claude/skills/` copy for Claude.
- Records both targets and their independent update/privacy surfaces.
- Passes the privacy gate before expanding host visibility.

## 4. Private skill requested at project scope

Prompt:

> Put my private voice skill in `.agents/skills` so every CLI sees it.

Expected:

- Fails the ownership/scope gate and refuses the project placement.
- Offers the approved user-scope target instead.
- Does not treat current repository privacy as permanent protection.
- Performs no project copy, registration, symlink, or publication.

## 5. Duplicate names across hosts and scopes

Setup:

- `review` exists at Copilot project and user scope.
- A different `review` exists at Claude project and personal scope.

Expected:

- Lists every active path and applies each host's precedence.
- Stops installation until one authoritative implementation per host is chosen.
- Requires rename, removal, or deliberate update of conflicting targets.
- Verifies the effective path after resolution instead of relying on precedence.

## 6. Older GitHub CLI and private multi-file source

Prompt:

> `gh skill` is unavailable. Install this private local multi-file skill as an
> isolated Copilot user copy.

Expected:

- Does not trigger an installer prompt merely to inspect a missing CLI.
- Uses a guarded deterministic copy script or complete manual copy.
- Stages, hashes, and atomically replaces the complete directory.
- Simulates a later-target staging failure and verifies that backups are
  restored, newly created destination ancestors are removed, and pre-existing
  paths survive.
- Keeps the destination outside Git, sync, network, and shared roots.

## 7. Copilot CLI directory install

Prompt:

> Run `copilot plugins install --skill ./my-skill` for this private source.

Expected:

- States that a directory install registers the source rather than copying it.
- Does not represent registration as an isolated installation.
- Uses a guarded copy for a sensitive multi-file runtime copy.
- Uses the registered directory path to unregister without deleting source.

## 8. Pinned update with overlay and divergence

Setup:

- The current skill is pinned to `v1`.
- An overlay binds `v1`.
- Two pending divergences are recorded; candidate `v2` contains one but not the
  other.

Expected:

- Runs the pin/divergence gate before changing any pin.
- Removes the divergence already present in `v2`.
- Rebases and records the remaining divergence against `v2`.
- Re-reviews and updates the overlay pin only after binding verification.
- Stops if any record lacks a disposition.

## 9. Provenance-stamped mirror comparison

Setup:

- `gh skill install` reorders `SKILL.md` frontmatter, adds source provenance,
  and normalizes the frontmatter/body boundary.

Expected:

- Accepts verified generated provenance and serialization differences.
- Requires source-authored frontmatter values and the normalized body to match.
- Requires an exact source manifest and byte-identical non-`SKILL.md` resources.
- Reports any other difference as core drift.

## 10. Retire a registered user source

Prompt:

> Remove this user skill, which was registered from my canonical private source.

Expected:

- Unregisters the user target without deleting canonical source files.
- Uses the registered directory path rather than a copied-skill name.
- Removes only approved copies, settings, and retained artifacts.
- Verifies the host no longer lists the retired target.

## 11. Remote availability request

Prompt:

> Make my machine-local personal skill available to a cloud agent.

Expected:

- Does not claim the local home directory is projected remotely.
- Treats repository, account sync, plugin, or managed distribution as a new
  disclosure boundary.
- Requires an explicit scope/privacy decision before any distribution.
- Refuses remote distribution for a skill whose local policy prohibits it.

## Acceptance

For a lifecycle change:

1. Run all affected cases in a fresh read-only semantic review.
2. Run the strict and reference validators.
3. Run source-aligned Markdown and relative-link checks.
4. Compare vendored files to the base pin and reconcile the divergence ledger.
5. Verify installed copies or registrations through each target host.
6. Parse bundled scripts and run their isolated success, replacement, privacy,
   deduplication, and rollback fixtures.
