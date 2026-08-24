# Find a skill

Detail for the [manage-skills](SKILL.md) skill. Answers "find a skill for X" /
"is there a skill that does X" with a tiered search and a per-repo applicability
check, then a short recommendation.

## Tiered search (nearest trust first)

Search in order and stop reporting once you have the picture; do not skip a tier,
because a local hit changes the recommendation entirely.

### 1. Installed - active project and user scopes

Establish the intended hosts and scope, then inspect every discovery root that
can be active there. Include inherited/parent roots, plugins, and configured
custom directories. Use the host's skill listing when available because it
exposes precedence and registered directories that a fixed file scan can miss.
Record every matching name, resolved path, scope, and winning source.

A project hit does not satisfy "install this for me everywhere"; a user hit does
not prove the repository carries the skill for teammates or remote agents.
Follow [install.md](install.md) for the host matrix.

### 2. Configured upstream sources

If the installed overlay declares an ordered upstream source policy, search each
organization, private, or shared source in that order and record its trust
boundary. A private source must not appear in public output. Unless the overlay
explicitly changes or removes it, include the shared commons:

```pwsh
gh skill search <terms> --repo JeremyKuhne/agent-skills
```

Anything here is curated and pre-vetted. When `gh` is unavailable, browse the
commons repository's `skills/` directory, read candidate descriptions and
metadata, and record the release tag or commit for a later pinned manual install.
Do not treat the moving default branch as the pin.

Do not infer equal trust from ordering. Organization and private sources still
need their declared review policy; an unknown source remains untrusted even when
it appears before the default commons.

### 3. Public catalogs

Search the wider ecosystem - the awesome-copilot collection,
`anthropics/skills`, and the registry:

```pwsh
gh skill search <terms>
```

Public results are **untrusted by default**. Do not recommend installing one
without the security gate in [build.md](build.md). When `gh` is unavailable,
browse the same catalogs manually, record the exact source revision, and keep
installation blocked until every source file and script can be reviewed.

## Applicability and scope check

A skill existing is not the same as belonging at the requested scope. Before
recommending an install, judge whether the domain applies and who needs it:

- A domain skill (for example a CsWin32 COM skill) is irrelevant in a repo that
  does no COM, even though it is a perfectly good skill. Say so; do not recommend
  vendoring something that will never fire.
- A project-gated skill (one that drives a sibling project such as a fuzz or perf
  project) applies if the repo has that project *or should have it*. If the
  project is missing, report that project creation as a separate prerequisite;
  vendoring the current core does not scaffold it.
- A repo-local skill from another repo (tied to that repo's unique structure)
  does not transfer; flag it as out of scope.
- A personal/private skill does not belong in a project root merely so it is easier
  to discover. Keep it at user scope and state that remote agents will not
  necessarily receive a machine-local copy.
- A general portable skill need not be committed to every repo when one user wants
  it. Conversely, a personal install does not satisfy a team requirement for a
  reviewed, pinned project copy.

## Recommendation report

Output a short summary, not a raw search dump:

- **Where it exists:** installed path(s) and scope(s) / commons / public / nowhere.
- **Applicable to whom:** project / user / managed or remote, with the one-line
  reason.
- **Active source:** winning path and any duplicate-name ambiguity.
- **Recommended action:** use the existing target / install at the missing
  scope / tweak the overlay / install from the commons / evaluate a public
  source / build new / skip as inapplicable.

That recommendation hands "build new" to [build.md](build.md) and any runtime copy
or registration to [install.md](install.md).
