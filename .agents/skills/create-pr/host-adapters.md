# Create-PR host adapters

The [create-pr core](SKILL.md) is capability-based. Use this page only to map
those capabilities to exact client tool names; the workflow and approval gates
remain in the core.

## GitHub Copilot in VS Code

- Structured question: `vscode_askQuestions`. Supply two options - the suggested
  branch name and `Use a different name` - and leave free-form input enabled.
- Pull-request creation: `github-pull-request_create_pull_request`. Supply
  `title`, `body`, `head`, and `base`. For an upstream-targeted PR, also supply
  the upstream `repo` and the fork `headOwner`.

## GitHub Copilot CLI

The CLI has no built-in pull-request creation capability in the portable tool
set. Ask the branch-name question in chat, then use authenticated `gh pr create`.
The core contains the upstream and origin command forms.

## Other Agent Skills hosts

Use a host-native structured question or GitHub integration when one exists.
Otherwise ask in chat and use `gh`. If `gh` is unavailable, emit the compare URL
from the core and stop; do not invent a host tool name or silently install one.
