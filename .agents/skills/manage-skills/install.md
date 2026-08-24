# Install and scope a skill

Detail for the [manage-skills](SKILL.md) skill. Use for "install this skill",
"add this for me", "make this available everywhere", "vendor this into the
repo", or whenever build/find/update must choose a destination.

The Agent Skills specification defines a skill directory and `SKILL.md`; it does
not define discovery roots, precedence, synchronization, or installation tools.
Those belong to each host and change independently. Re-check host documentation
before changing a fleet-wide mapping.

## 1. Separate ownership from scope

Decide all three dimensions explicitly. One does not imply another:

- **Source owner:** portable/shared, repository-specific, or personal/private.
  This controls where the canonical source and changes belong.
- **Audience:** one project, one user, a managed group, or remote agents. This
  controls runtime scope and distribution risk.
- **Clients:** Copilot, Claude, Codex, Gemini, Cursor, or another host. This
  controls discovery roots, precedence, and tooling.

Source ownership:

- **Portable/shared** - generic source suitable for a commons or plugin. Install
  it at project scope when a team needs a reviewed pin, or user scope when one
  person wants it across projects.
- **Repository-specific** - paths, policy, or workflow belong to one repository.
  Keep the canonical source in that repository and install at project scope.
- **Personal/private** - voice, preferences, private workflow, or individual
  context. Keep the canonical source outside public/shared repositories and use
  user scope. A private repository may hold source only when its visibility and
  future ownership are controlled.

Do not use "born-local" as a synonym for "project scope". A skill can be born
personal and never belong to a repository.

## 2. Choose runtime scope

### Pass the ownership and scope gate

Before selecting a path or tool, record the source classification and requested
audience. Stop rather than install when any condition below is true:

- A personal/private source is requested at project, plugin, managed, or remote
  scope without an explicit reclassification and disclosure decision.
- A repository-specific source is requested for unrelated projects or user-wide
  installation without first making the reusable portion portable.
- The audience includes teammates, CI, code review, or remote agents but the
  proposed destination is only a machine-local user root.
- The requested host does not document the proposed discovery root.

For a personal/private skill requested at project scope, refuse the placement
and offer a user-scope install. Do not proceed merely because the project is
private today. Reclassification is a separate content and privacy review, not an
install flag.

### Project scope

Use when the repository must carry the behavior for teammates, CI, code review,
or a remote agent. Commit the complete skill and its resources. Prefer one
canonical project root supported by every required host; do not duplicate the
same name across aliases merely for convenience.

Project scope is publication to everyone who can read the repository. Never put
a personal/private skill there, even when the repository is private today,
unless that broader audience and any future visibility change are acceptable.

### User scope

Use when a skill should follow one person across local projects. User scope is
machine/profile scope, not secret storage and not a guarantee of cloud-agent
availability. A user skill can activate while working in a public repository,
but its files should remain outside that repository.

### Plugin, managed, or remote scope

Use a plugin or managed distribution for a curated fleet, bundled dependencies,
or organization policy. For remote sessions, do not assume a machine-local home
directory is projected. Repository skills are the portable baseline; use a
host's documented account/plugin synchronization only after verifying it and
accepting the disclosure boundary.

## 3. Select a host discovery root

The paths below were verified against first-party documentation on 2026-08-12.
`~` means the user's home directory (`%USERPROFILE%` on Windows).

### GitHub Copilot in VS Code

- Project: `.github/skills/`, `.agents/skills/`, `.claude/skills/`.
- Personal: `~/.copilot/skills/`, `~/.agents/skills/`,
  `~/.claude/skills/`.
- Additional project locations can be configured with
  `chat.agentSkillsLocations`.

### GitHub Copilot in Visual Studio

- Requires Visual Studio 2026 18.5 or later.
- Project: `.github/skills/`, `.agents/skills/`, `.claude/skills/`.
- Personal: `~/.copilot/skills/`, `~/.agents/skills/`,
  `~/.claude/skills/`.
- Visual Studio 2026 Insiders 18.6 or later provides a skills panel for creating
  and managing workspace or personal skills.

### GitHub Copilot CLI and app

- Project precedence begins with `.github/skills/`, then `.agents/skills/`, then
  `.claude/skills/`; parent project roots may also be inherited.
- Personal: `~/.copilot/skills/`, then `~/.agents/skills/`.
- Additional roots: `COPILOT_SKILLS_DIRS` or the CLI `skillDirectories`
  setting.
- `~/.copilot/skills/` is the simplest shared personal root for VS Code,
  Visual Studio, and Copilot CLI on one machine.
- The Copilot app can surface skills configured for repositories or Copilot CLI
  and also manages skills in app settings. Verify the active source in the app;
  do not infer remote synchronization from local discovery alone.

For Copilot cloud agent and code review, commit non-private project skills to a
supported repository root. Do not assume a local personal directory reaches a
remote session.

### Claude Code

- Project: `.claude/skills/`.
- Personal: `~/.claude/skills/`.
- Enterprise and plugin scopes also exist.
- Claude Code personal scope takes precedence over project scope for duplicate
  names, unlike Copilot CLI.
- Machine-local personal skills do not automatically reach Claude cloud or
  Cowork sessions. Those use account-enabled skills, repository skills, or
  declared plugins.

### Codex

- Project: `.agents/skills/` from the working directory through repository root.
- Personal: `~/.agents/skills/`.
- Admin: `/etc/codex/skills/` where applicable.
- Codex may show duplicate names rather than merging them; use unique names and
  inspect the selected path.

### Gemini CLI

- Project: `.agents/skills/` or `.gemini/skills/`.
- Personal: `~/.agents/skills/` or `~/.gemini/skills/`.
- Workspace scope outranks user scope. Within one tier, `.agents/skills/`
  outranks `.gemini/skills/`.

### Cursor

- Project: `.agents/skills/` or `.cursor/skills/`.
- Personal: `~/.agents/skills/` or `~/.cursor/skills/`.
- Cursor also reads Claude and Codex compatibility roots.

### Cross-host recommendation

Use `.agents/skills/` or `~/.agents/skills/` for a single physical copy shared
by Copilot, Codex, Gemini, and Cursor. Claude Code does not document that neutral
root; install a separate `.claude/skills/` copy when Claude is required. A
second copy creates a second update and privacy surface, so record every target.

Host precedence is not portable. Do not depend on a personal skill overriding a
project skill (or the reverse) across clients. Prefer unique names and verify the
active source path.

### Pass the duplicate-name gate

Before writing or registering a target, list the skill at every active project,
inherited, user, plugin, and custom root for each requested host. If the same
name already exists:

1. compare canonical source, version/pin, and intended audience;
2. choose one authoritative target for that host and scope;
3. remove, rename, or deliberately update the conflicting target before install;
4. rerun the host listing and prove the selected path is active.

Stop when a conflict remains unresolved. A note that one host shadows another is
not sufficient because another host may reverse precedence or expose both
copies. Do not install different implementations under the same name across
active roots.

### Maintain a target ledger

Create one row per requested host before installation and complete it afterward:

| Host | Scope | Roots checked | Host listing result | Conflict disposition | Effective path |
| --- | --- | --- | --- | --- | --- |
| `<host>` | project/user | `<paths>` | `<command and result>` | none/renamed/removed/updated | `<resolved path>` |

Every requested host needs its own row even when several hosts share one
physical directory. A shared path does not prove that every client discovered
it. If a client is unavailable, record `not runtime-verified` and do not claim
the install is complete for that client. Stop while any conflict disposition or
effective path is unresolved.

The completed ledger is the cross-host assertion; no single host command proves
another client's discovery. Use each host's native listing or UI for its row.

## 4. Pass the personal-skill privacy gate

Before installing at user scope:

1. **Classify content.** Skills are not secret stores. Remove credentials,
   tokens, private keys, customer data, and facts that should never enter a
   model request.
2. **Review discovery metadata.** Hosts commonly load every skill's name and
   description before activation. Keep sensitive detail out of both.
3. **Review activation disclosure.** When invoked, the body and selected
   resources enter the model context. Scripts execute with the host's local
   permissions. Check logging, telemetry content capture, backups, profile sync,
   indexing, and endpoint-management policy.
4. **Control source and destination.** Prefer a local-only canonical source or a
   verified private repository. Keep the installed copy outside Git worktrees,
   public artifacts, shared folders, network shares, and cloud-sync roots unless
   each exposure is intentional.
5. **Prefer copies to symlinks.** Symlinks blur trust, publication, and cleanup
   boundaries even when a host supports them. Use a deterministic copied install
   with hash verification for sensitive skills.
6. **Minimize hosts.** Do not install into a neutral user root merely for
   convenience when only one host should see the skill.
7. **Treat remote availability as disclosure.** Uploading, account-syncing, or
   committing a personal skill so a cloud agent can use it changes the privacy
   boundary. Obtain an explicit decision first.

Invocation-control fields such as `disable-model-invocation` are host extensions,
not part of the portable Agent Skills frontmatter contract. Use them only when
the target and validator accept them; otherwise enforce narrow descriptions and
host settings.

## 5. Choose installation tooling

Always pass scope and host explicitly. Tool defaults disagree.

### GitHub CLI 2.90 or later

Prefer `gh skill` for GitHub-hosted skills, multi-host destination mapping, and
provenance-aware updates. Its non-interactive default is Copilot at **project**
scope.

```pwsh
gh skill preview <owner/repo> <skill>@<full-commit-sha>
gh skill install <owner/repo> <skill> --pin <full-commit-sha> `
  --agent github-copilot --scope project
gh skill update <skill> --dry-run
gh skill update --all --dry-run
```

- Preview every file and script before installing.
- Use a full commit SHA or immutable tag; `--pin` skips automatic updates until
  deliberately re-pinned.
- Install each hard dependency at the same pin.
- `--agent` selects the host mapping; `--scope user|project` selects scope.
- `--from-local` copies a local skill and injects local-path tracking metadata.
  Review that metadata before any later publication because it can expose a
  local path or username.
- `gh skill update` scans known host directories at both scopes. `--dry-run` is
  read-only. `--force` restores tracked files but leaves extra local files, so it
  does not replace an overlay/divergence audit.

### GitHub Copilot CLI

Copilot CLI skill installation defaults to **user** scope, the opposite of
`gh skill`:

```pwsh
copilot plugins install --skill ./path/to/SKILL.md --scope user
copilot plugins install --skill ./path/to/skill-directory --scope user
copilot plugins list --kind skill --scope user --json
```

Installing a directory registers it as a custom skill source rather than copying
it. Installing a file or URL copies content; project scope is supported for file
or URL installs. A registered directory remains the canonical files at the
original path: edits take effect there, and unregistering must not delete it.

For a multi-file private skill that needs an isolated runtime copy, do not use a
directory registration or a single `SKILL.md` file install. Use `gh skill
--from-local` after reviewing injected metadata, or a guarded complete-directory
copy script instead.

To remove a registered directory without deleting its files:

```pwsh
copilot plugins remove ./path/to/skill-directory --skill
```

Passing an installed skill **name** instead removes that personal or project
skill's copied files. Inspect `copilot plugins list --kind skill --json` and use
the registered source path when the canonical directory must remain.

### Host-native tools

- Gemini: `gemini skills install` defaults to user scope; pass
  `--scope workspace` for a project. `/skills link` registers a local source.
- Claude: use the documented personal/project directory or a reviewed plugin.
- Codex: use `$skill-installer` for curated local skills, or copy the complete
  directory to `.agents/skills/` / `~/.agents/skills/`.
- Cursor: use its Skills UI or a complete copy under a supported root.

### Deterministic script or manual fallback

Use a script when privacy policy, an older CLI, or exact-copy requirements make
generic installers unsuitable. A safe installer should:

- accept only enumerated host/scope targets and fail closed;
- copy the complete directory, never only `SKILL.md`;
- reject unexpected Git visibility, reparse points, shared/network/sync roots,
  and repository destinations for private personal skills;
- stage the copy, compare relative file lists and cryptographic hashes, then
  replace atomically with rollback;
- require an explicit overwrite flag;
- report provenance through the target ledger without injecting a sensitive
  local path into a copy that might later be shared.

This skill bundles
[scripts/Install-UserSkill.ps1](scripts/Install-UserSkill.ps1), which implements
that user-scope copy contract. Run it by its resolved local path after the scope,
duplicate-name, and privacy gates pass:

```pwsh
pwsh <resolved-installer-path> `
  -SourceSkillPath <skill-directory> `
  -TargetHost github-copilot
```

Supported target mappings are `github-copilot`, `shared-agents`, `claude-code`,
`codex`, `gemini-cli`, and `cursor`. `shared-agents` and `codex` both resolve to
the neutral `~/.agents/skills/` root and are deduplicated when selected together.
Use `-ProfileRoot` only to select another user profile root or an isolated test
profile; host-relative destinations remain fixed beneath it.

Use `-Private` for personal/private sources. It requires a local-only source or
a GitHub repository verified as private and rejects network, synchronization,
Git-worktree, and reparse-point boundaries. A private copy to multiple roots or
the neutral `~/.agents/skills/` root additionally requires
`-AllowPrivateMultiHostExposure`. Use `-Force` only after reviewing the source
diff. The script copies complete directories, stages and hashes every target,
and rolls back the multi-target operation before commit if a replacement fails.

## 6. Verify the effective installation

Do not stop at file creation. Verify:

- the intended host and minimum version support Agent Skills;
- the ownership/scope gate passed and no personal/private skill crossed into a
  broader scope without explicit reclassification;
- the skill appears at the intended scope and resolved path;
- the duplicate-name gate passed for every target host; no unresolved duplicate
  wins, shadows, or coexists;
- every referenced resource and script traveled with the copy;
- provenance/pin and source hash match the reviewed source;
- private content did not enter a repository, generated artifact, sync root, or
  install metadata;
- one should-trigger and one should-not-trigger request route correctly.

Useful host checks:

- Copilot CLI: `copilot plugins list --kind skill --json`, `/skills reload`, and
  `/skills info <name>`.
- VS Code: `/skills` or the Agent Customizations editor and diagnostics.
- Visual Studio: the Skills panel where available, otherwise a fresh agent chat.
- Claude Code: `/skills` and the displayed source.
- Gemini CLI: `/skills list`.
- Codex: `/skills` or the skill selector; restart if a change is not detected.
- Cursor: **Customize** > **Skills**.

## 7. Carry scope through the lifecycle

- **Find:** search active project, inherited, user, plugin, and custom roots before
  concluding a skill is absent. Report every duplicate and its precedence.
- **Update:** identify the canonical source before touching an installed copy.
  Re-pin project and user copies deliberately; do not edit generated provenance.
- **Review:** test routing in every supported host whose precedence or
  frontmatter extensions differ.
- **Retire:** distinguish deleting a copied skill from unregistering a directory.
  Remove every recorded target, stale setting, backup, and sensitive cache that
  policy requires, without deleting the canonical source accidentally.

Report source ownership, canonical source, host, scope, resolved destination,
install mode (copy/register/plugin), pin/provenance, verification evidence, and
privacy residuals.

Include the completed target ledger in that report.

Use [evaluations.md](evaluations.md) after changing installation or scope
behavior.

## Official sources

Verified 2026-08-12:

- [Agent Skills specification](https://agentskills.io/specification)
- [VS Code Agent Skills](https://code.visualstudio.com/docs/agent-customization/agent-skills)
- [Visual Studio Agent Skills](https://learn.microsoft.com/visualstudio/ide/copilot-agent-skills?view=visualstudio)
- [GitHub Copilot Agent Skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills)
- [GitHub Copilot CLI skills](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills)
- [GitHub CLI `gh skill`](https://cli.github.com/manual/gh_skill)
- [Claude Code skills](https://code.claude.com/docs/en/skills)
- [Codex and ChatGPT skills](https://learn.chatgpt.com/docs/build-skills)
- [Gemini CLI skills](https://geminicli.com/docs/cli/skills/)
- [Cursor skills](https://cursor.com/docs/context/skills)
