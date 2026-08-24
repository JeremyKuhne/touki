# Update a skill

Detail for the [manage-skills](SKILL.md) skill. "Update the skill" has two
directions. The second - pushing a local improvement - is where the
[golden rule](SKILL.md) is enforced.

## Pull: take upstream changes

First identify the canonical source and every installed project/user target. Do
not edit a runtime copy merely because it is the first path found.

When a skill has moved upstream, check before changing files:

```pwsh
# One skill
gh skill update <skill> --dry-run

# All installed skills
gh skill update --all --dry-run
```

`gh skill update` compares the local copy's provenance tree SHA against upstream
and scans known host directories at project and user scope. Review each target
like a dependency bump: read what changed, run the applicable agent-file checks,
then re-pin when satisfied. Update an overlay's `core-pin` and re-review its
bindings in the same change. A skill pinned with `--pin` is skipped; reinstall
it with a new pin deliberately.

### Pass the pin and divergence gate

Before changing any pin or provenance ref:

1. enumerate the overlay and every pending-divergence record for the skill;
2. compare each divergent file against both the current pin and candidate pin;
3. search the candidate upstream tree and release history for the equivalent
   change;
4. remove a divergence record only when the candidate artifact contains it;
5. rebase a still-needed divergence onto the candidate and update its base pin,
   affected files, reason, and upstream status;
6. update every overlay `core-pin` only after its bindings are reviewed against
   the candidate;
7. run the upstream mirror comparison and semantic cases before installing.

For an installer-produced artifact, normalize only the installer boundary. The
mirror comparison passes when:

- the installed manifest equals the source manifest plus declared overlays or
  pending-divergence files;
- every source resource other than `SKILL.md` is byte-identical;
- every source-authored frontmatter field has the same parsed value;
- the normalized `SKILL.md` body is identical after line-ending and
  frontmatter-boundary normalization; and
- generated `github-repo`, `github-ref`, `github-pinned`, `github-path`,
  `github-tree-sha`, or `local-path` metadata matches the reviewed source and
  target.

Do not compare a provenance-stamped `SKILL.md` by raw file hash: `gh skill`
reserializes frontmatter and may remove the blank line after its closing
delimiter. Treat any other field, body, resource, or manifest difference as
drift. Overlays and local catalog collateral are additive and must be identified
separately.

Stop the update if any overlay or divergence has no explicit disposition. A new
pin with a stale base-pin record is unexplained drift, even when validation and
the skill itself still load.

`--force` overwrites locally modified tracked files but does not remove extra
files. It therefore does not prove overlays or pending divergences are still
valid.

Manual fallback (no `gh`): compare the canonical source or installed core against
the recorded immutable revision, apply the reviewed diff, update provenance, and
reinstall every recorded host/scope target with file-list and hash verification.

## Push: send a local improvement to the right layer

When you improve a vendored skill locally, first classify the change, then decide
where it lives. Classification does not trigger any action on its own.

- **Local deviation** - specific to this repo or user (a repo-only tool,
  personal policy, local path, or target-specific example). It belongs in the
  installation's **overlay**, never in the vendored core. Move the change into
  `overlay.md` (starting from
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
  pending-upstream divergence*: record it in the commit message and the repository's
  divergence ledger or a short note. Identify the skill, affected files, base pin,
  reason, and upstream status so the drift check's later alarm is expected, not a
  surprise. Re-attempt upstreaming when it becomes plausible; remove the record when
  the pinned upstream artifact contains the change.
- **Reclassify** - if discussion shows the change is actually repo-specific, move
  it to the overlay instead and restore the core.

Default to asking even when the change looks obviously common and obviously worth
sharing. Nothing about upstreaming happens without an explicit decision.

Before presenting an upstream summary or publishing an approved commons change,
run `technical-writing` against the current diff and lifecycle disposition.
Review pending-divergence text locally when upstreaming is deferred. For an
approved PR, run pre-publication mode on the exact title and body immediately
before creation; rerun it if the candidate, diff, validation, or upstream state
changes. A successful prose review does not answer the upstreaming query or
authorize the PR.

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
- **The drift check** (`gh skill update`, or the normalized mirror comparison in
  CI) compares the local core against that recorded upstream. Unexplained drift
  - a local core that no longer matches its pin and has no corresponding upstream
  PR - is the alarm that an improvement was written into the wrong layer.

So the discipline is mechanical: if the drift check lights up and there is no
upstream PR in flight and no recorded pending-upstream note, the change was a local
deviation written into the core by mistake; move it to the overlay and restore the
core.

## After any update

Run [review.md](review.md) against the changed routing, workflow, and ownership
surface. Re-run the validators and link check, and if the change touched the catalog
or trigger phrasing, reconcile the catalog `README.md` (inventory row and
disambiguation) in the same change. Then hand off to `agent-files-review` to validate
the resulting files; semantic lifecycle review does not replace file-level review.
If the skill is obsolete rather than changed, follow [retire.md](retire.md) instead
of forcing removal into the update path.

Then follow [install.md](install.md) to verify each effective project/user copy,
registered source, plugin, and host path. Report any target intentionally left
at an older pin.

Run the update cases in [evaluations.md](evaluations.md), including a candidate
pin that already contains one local divergence and another that does not.
