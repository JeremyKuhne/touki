# Update a skill

Detail for the [manage-skills](SKILL.md) skill. "Update the skill" has two
directions. The second - pushing a local improvement - is where the
[golden rule](SKILL.md) is enforced.

## Pull: take upstream changes

When a vendored skill has moved upstream in the commons, pull the change:

```pwsh
# one skill
gh skill update <skill>
# every unpinned vendored skill
gh skill update --all
```

`gh skill update` compares the local copy's provenance tree SHA against upstream
and surfaces the difference as a normal diff. Review it like a dependency bump:
read what changed, run the repo's agent-file checks (the frontmatter validator and
the installed-artifact link checker), then re-pin when satisfied. Update the
overlay's `core-pin` and re-review its bindings in the same change. A skill pinned
with `--pin` is skipped by `--all`; bump its pin deliberately when you want its
updates.

Manual fallback (no `gh`): compare the local core against the commons copy at the
recorded ref, apply the diff by hand, and update the provenance SHA.

## Push: send a local improvement to the right layer

When you improve a vendored skill locally, first classify the change, then decide
where it lives. Classification does not trigger any action on its own.

- **Local deviation** - specific to this repo (a repo-only tool, a local path, a
  repo-specific example). It belongs in the **overlay**, never in the vendored
  core. Move the change into `overlay.md` (starting from
  `assets/overlay.md.tmpl` when needed), restore the core to match upstream, and
  record the current pin in `core-pin`. No upstreaming question arises.
- **Common** - generic, helps every consumer (a clearer phrasing of a portable
  rule, a new universally-applicable check, a fixed error). It *should* go
  upstream, but upstreaming is **never automatic** and is not always plausible:
  the commons may be unreachable, the change may be sensitive or need discussion
  first, or you may lack the time or rights. So **ask** before attempting it.

### The upstreaming query (common changes only)

Stop and ask the user whether to attempt upstreaming. **Never open a commons PR on
your own** - it is a publish action, gated by the same rule as any push (the
repo's contribution and publish rules). Present what the change is, why it is
common, and the options:

- **Upstream it now** - prepare the PR to the commons; *creating* it still needs an
  explicit publish verb from the user. Once merged, re-vendor here at the new pin.
- **Not now / not plausible** - keep the change in the local core as a *tracked
  pending-upstream divergence*: record it (in the commit message and a short note)
  so the drift check's later alarm is expected, not a surprise. Re-attempt
  upstreaming when it becomes plausible; re-pinning clears the divergence.
- **Reclassify** - if discussion shows the change is actually repo-specific, move
  it to the overlay instead and restore the core.

Default to asking even when the change looks obviously common and obviously worth
sharing. Nothing about upstreaming happens without an explicit decision.

## The golden rule and its mechanics

*Never let a vendored core diverge silently.* A vendored core is a mirror of
upstream; any edit to it is a deliberate fork that must end in one of three
**recorded** states - never an unexplained one:

- promoted upstream and the core re-pinned,
- moved to the overlay and the core restored, or
- kept as a **tracked pending-upstream divergence** - a common change that could
  not be upstreamed yet, recorded so the divergence is intentional and visible.

The point is *visibility*, not "resolved within the hour". A recorded divergence is
fine; an unexplained one is the alarm. What makes this enforceable:

- **Provenance frontmatter** on every vendored copy records the source repo, ref,
  and tree SHA it was installed from.
- **The drift check** (`gh skill update`, or the tree-SHA comparison in CI)
  compares the local core against that recorded upstream. Unexplained drift - a
  local core that no longer matches its pin and has no corresponding upstream PR -
  is the alarm that an improvement was written into the wrong layer.

So the discipline is mechanical: if the drift check lights up and there is no
upstream PR in flight and no recorded pending-upstream note, the change was a local
deviation written into the core by mistake; move it to the overlay and restore the
core.

## After any update

Re-run the validators and the link check, and if the change touched the catalog
or a skill's trigger phrasing, reconcile the catalog `README.md` (inventory row,
disambiguation) in the same change. Then hand off to the repository's
`agent-files-review` skill to validate the resulting files.
