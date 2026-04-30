# Reusable prompts (`.github/prompts/`)

`*.prompt.md` files are reusable prompts available as slash commands in GitHub Copilot
(VS Code, Visual Studio). Drop a file here and it shows up in the chat `/` menu.

## Format

```markdown
---
description: Short description shown in the / menu.
---

# Body

Prompt body in Markdown. Use `#tool:<tool-name>` to reference agent tools.
```

See [docs/agent-customization.md](../../docs/agent-customization.md) for details.
