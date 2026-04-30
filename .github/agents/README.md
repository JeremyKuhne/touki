# Custom agents (`.github/agents/`)

`*.agent.md` files define custom agent personas (planner, reviewer, etc.) with their own
tool restrictions, model preferences, and handoffs. Read by GitHub Copilot in VS Code
and Visual Studio.

## Format

```markdown
---
description: Brief description of what this agent does.
tools: ['search', 'edit']        # optional; restrict available tools
model: 'GPT-5 (copilot)'         # optional
handoffs:                         # optional
  - label: Start Implementation
    agent: implementation
    prompt: Now implement the plan above.
---

# Body

Persona instructions in Markdown.
```

See [docs/agent-customization.md](../../docs/agent-customization.md) for the full field
list and tooling support.
