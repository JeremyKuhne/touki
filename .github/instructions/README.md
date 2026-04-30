# Path-specific instructions (`.github/instructions/`)

`*.instructions.md` files apply rules to specific file patterns. Read by GitHub Copilot
(cloud agent, code review, VS Code, Visual Studio).

## Format

```markdown
---
applyTo: '**/*.cs, **/*.csx'
---

# Title

Instructions in Markdown...
```

`applyTo` is **required** and accepts comma-separated globs relative to the repo root.

See [docs/agent-customization.md](../../docs/agent-customization.md) for details.
