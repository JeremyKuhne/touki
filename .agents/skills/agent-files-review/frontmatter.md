# Agent-file frontmatter and naming

Per-file-type frontmatter and naming rules for the `agent-files-review`
checklist. Check the entry for the file type you changed; the operational
workflow (mirror, links, validator, whitespace) stays in [SKILL.md](SKILL.md).

## `*.instructions.md` (path-specific instructions)

- Frontmatter must include a non-empty `applyTo` glob.
- Glob is comma-separated, relative to repo root. Quote the value for safety.
- The validator only checks `applyTo`'s presence and emptiness; it does not
  verify that the glob actually matches anything. Sanity-check by eye.

## `*.agent.md` (custom agents)

- Frontmatter must include `description`.
- If `tools` is present, it must be a YAML list. Either form is accepted by
  the validator:

  ```yaml
  tools: ['search', 'edit']
  ```

  ```yaml
  tools:
    - search
    - edit
  ```

- Use VS Code's current tool names: bare tool-set names (`search`, `read`,
  `edit`, `web`) or namespaced `<set>/<tool>` members (`read/problems`,
  `web/fetch`, `search/usages`, `search/changes`). The legacy flat names
  (`usages`, `problems`, `changes`, `fetch`) still resolve but raise an
  info-level "renamed" notice in VS Code, so prefer the current form. A bare
  tool-set name already includes its members (e.g. `search` covers
  `search/usages` and `search/changes`).
- The repo's authoring rules forbid end-of-line comments; document optional
  fields with comment lines *above* them in any examples.

## `SKILL.md` (`.agents/skills/<name>/SKILL.md`)

- `name` is **required** and must:
  - be 1-64 chars of lowercase letters/digits (Unicode allowed) and hyphens,
    with no leading, trailing, or consecutive hyphen
  - equal the parent directory name exactly
- `description` is required; make it specific enough that another agent
  can decide when to load it.
- Shared portfolio cores also require string-valued `metadata.portability`,
  `metadata.applicability`, `metadata.binding`, `metadata.risk`,
  `metadata.maturity`, `metadata.requires`, and `metadata.related`. Use the
  repository's `FORMAT.md` vocabulary; relationship names must resolve and the
  required graph must remain acyclic.
- For `optional-overlay` or `required-overlay`, keep the standard loader sentence
  in the core. An `overlay.md` starts with `core` and `core-pin`; `core` matches
  the directory, and a required overlay must exist.
- A name/dir mismatch causes the skill to silently fail to load. Always
  verify by running both the strict bundled validator and `skills-ref`.

## `*.prompt.md` (reusable prompts)

- No required frontmatter, but `description` is recommended for the slash
  menu UX.
