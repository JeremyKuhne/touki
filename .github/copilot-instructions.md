<!-- DO NOT EDIT. Generated mirror of /AGENTS.md. Edit AGENTS.md and run: pwsh tools/Validate-AgentFiles.ps1 -Fix -->
# AGENTS.md

Instructions for AI coding agents working in this repository. Applies to GitHub Copilot
(VS Code, Visual Studio, CLI, github.com cloud agent), Claude Code, OpenAI Codex, Cursor,
Aider, Gemini CLI, and any other tool that supports the [AGENTS.md](https://agents.md/)
standard.

This file is the single source of truth. `.github/copilot-instructions.md` mirrors this
file for Copilot features that read it directly; do not edit the mirror by hand.

For broader contributor guidance, see [CONTRIBUTING.md](../CONTRIBUTING.md) and
[docs/coding_guidelines.md](../docs/coding_guidelines.md). For how to add or update agent
customizations (skills, prompts, custom agents, path-specific instructions), see
[docs/agent-customization.md](../docs/agent-customization.md).

## Project overview

`touki` is a C# library that targets both **.NET 10** and **.NET Framework 4.7.2**.
All code must compile and behave correctly on both targets.

Top-level layout:

- `touki/` - main library
- `touki.tests/` - xUnit tests (uses FluentAssertions; access to internals via `InternalsVisibleTo`)
- `touki.testsupport/` - shared test helpers (`TestAccessor`, etc.)
- `touki.perf/` - BenchmarkDotNet performance projects
- `sample/` - usage samples
- `docs/` - contributor and design documentation

## Environment setup

- On Unix, run `./setup.sh` once after cloning to install the .NET 10 SDK and update PATH.
- On Windows, install the .NET SDK from <https://dotnet.microsoft.com/download> and use
  `dotnet` directly. PowerShell is the preferred terminal.
- Use the `dotnet` CLI for building and testing. CI runs `dotnet build` and `dotnet test`.

## Coding style

- Use the latest C# features (C# 14) where applicable.
- Always use C# keywords for types (`int`, `string`, `bool`) instead of their aliases
  (`Int32`, `String`, `Boolean`).
- Always use `nint` and `nuint` for native integer types (not `IntPtr` and `UIntPtr`).
- Avoid `var` - use explicit type declarations, target-typed `new`, and
  [collection expressions](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/collection-expressions)
  together: `List<string> list = new();`, `int[] values = [1, 2, 3];`,
  `Point[] points = [new(1, 2), new(5, 6)];`. The variable's type is always
  spelled out; the `new`/`[]` literal supplies the value.
- Use `is null` and `is not null` for null checks instead of `== null` or `!= null`.
- For enums wrapped in `Value`, always call `Value.Create()` instead of `new Value()`.
- Use the following header for all C# files:

```c#
// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information
```

## Comments and XML documentation

- Avoid putting comments at the end of lines.
- Comments should be before the code they describe, or inside blocks to describe the
  condition the block is handling.
- Create XML comment documentation for all public methods, properties, and types.
- Use `<para>` blocks in `<remarks>` to separate different sections of remarks.
- Indent XML comments by one space for each level of nesting:

```c#
/// <summary>
///  This is the summary.
/// </summary>
/// <remarks>
///  <para>
///   This is a remark.
///  </para>
/// </remarks>
```

- Use `<inheritdoc/>` and `<inheritdoc cref="..."/>` to inherit documentation from base
  classes and interfaces where applicable.
- For method overloads, use `<inheritdoc cref="MethodName"/>` to inherit documentation
  from the method with the most arguments, overriding tags where they differ.
- Use `<see langword="..."/>` tags for language keywords in comments
  (e.g. `<see langword="true"/>` instead of `true`).
- Prefer plain ASCII characters that don't need escaping (`-`, `"`, `...`,
  `->`) over typographic Unicode (`—`, `–`, `"`, `"`, `…`, `→`) or HTML
  entities (`&mdash;`, `&ndash;`, `&hellip;`, `&rarr;`, `&nbsp;`). Use a
  plain `-` (with surrounding spaces when separating clauses) instead of
  em/en-dashes. Only escape when the raw character would actually be
  parsed as markup (e.g. `<` inside an XML doc comment); do not escape
  defensively when it isn't needed.

## Line breaks and whitespace

- Never put multiple statements on a single line.
- Ensure there is always a single blank line between methods and properties.
- Preserve existing spaces and line breaks when making edits, except when fixing
  whitespace issues.
- Indents are 4 spaces for all code except for XML (including XML comments in sources),
  which should have nested tags and content indented by one space per level.
- Lines should never end in or contain only spaces or tabs.
- Lines should be broken before 120 characters if they would otherwise exceed 150
  characters.
- When breaking statements, the next lines should be indented.
- When breaking statements, operators (`+`, `-`, `&&`, `||`, `or`, `and`) should not be
  at the end of the previous line, except for `=>`.
- When breaking lines in a method call, all parameters should be indented on their own
  lines.
- After multiple edits to a file, verify that blank lines and whitespace remain correct.
- When using search-and-replace tools, include 3-5 lines of context before and
  after the target text.

## Testing

Detailed test conventions live in
[.github/instructions/tests.instructions.md](instructions/tests.instructions.md)
(applies to `touki.tests/**/*.cs`). Headline rules: place tests in `touki.tests`;
name them `MethodName_StateUnderTest_ExpectedBehavior`; use FluentAssertions (global
using); access internals directly via `InternalsVisibleTo` and private members via
`TestAccessor`; ref structs can't be used in lambdas - use `try`/`finally`.

Performance-test conventions for `touki.perf/` (BenchmarkDotNet, Release-only,
JIT-naming rule, regression thresholds) live in
[.github/instructions/perf.instructions.md](instructions/perf.instructions.md).

## Working with the user on changes

Two hard requirements, both violated on this repo before:

1. **Never push without explicit user approval.**
2. **Never create a pull request without explicit user approval.**

Local commits are reversible and don't need approval. Anything that
publishes to the remote or to GitHub does.

### Pre-flight before any publishing tool

Re-read the **most recent user message verbatim**. The only acceptable
approvals are an explicit publishing verb in that message: `push`,
`commit and push`, `ship it`, `send it`, `open the PR`, `create the PR`,
`make the PR`, or an equivalent. If the message does not contain one of
those verbs, **stop and ask one short yes/no question.**

Tools that count as **push** (require approval):

- Terminal: `git push` (incl. `--force`, `--force-with-lease`).
- MCP / in-process: `mcp_io_github_git_push_files`,
  `mcp_io_github_git_update_pull_request_branch`.

Create feature branches locally with `git checkout -b`; that's not a push.

Tools that count as **PR create / edit / merge / close** (require
approval):

- Terminal: `gh pr create|edit|merge|close`.
- MCP / in-process: `mcp_io_github_git_create_pull_request`,
  `mcp_io_github_git_update_pull_request`,
  `mcp_io_github_git_create_pull_request_with_copilot`,
  `mcp_io_github_git_merge_pull_request`,
  `github-pull-request_create_pull_request`.

### Phrasings that are NOT approval

None of these authorize a push or a PR. They have all been
mistaken for it on this repo:

- "address the review comments" / "fix the comments" / "look at the
  comments" - edit-only.
- "do the next step" / "let's do X next" / a bare "go ahead" attached
  to a task description - selects which task, not whether to publish.
- "fix the CI failure" / "the PR is failing" - diagnosis or local
  fix, not a push.
- "pull main and work on X" - authorizes the work only.

When in doubt, ask one short yes/no question.

### Workflow

1. **Work on a feature branch.** If `git branch --show-current` reports
   `main`, create a topic branch first (`git checkout -b <name>`). Never
   commit to `main`. If you're handed a commit-in-progress on `main`,
   stop and ask. Default to rebasing the branch on the current
   `origin/main` (`git fetch origin main && git rebase origin/main`)
   before the first push, unless the user says otherwise.
2. Edit.
3. Validate (`dotnet build`, `dotnet test -c Release`).
4. Stage by path and commit locally whenever the change is coherent;
   local commits are reversible and don't need approval.
5. **Stop.** Describe the change and show or summarize the diff.
   **On explicit publish verb**, push or PR. If unsure, ask.

### After a violation

Acknowledge directly without minimizing. **Do not push a follow-up
"fix" commit without explicit approval** - that compounds the
failure. The user decides whether to revert, force-push, or leave it.

### Other rules

- **Stage by path, never `git add -A`/`git add .`** when the working
  tree spans more than one logical change. Run `git status --short`
  first; if topics are intermingled, ask how to split.
- **Run `dotnet test -c Release` before declaring a fix done.**
  Release-mode inlining surfaces bugs Debug doesn't - `Unsafe.As`
  on a method parameter is a known foot-gun on net481 RyuJIT.

### Enforcement

**The user may be running VS Code Copilot in Bypass Approvals mode**
(`chat.permissions.default` = `"autoApprove"` at the user level), or in
Autopilot, or with `chat.tools.global.autoApprove` enabled. The agent
cannot tell from inside a session which mode is active. **Assume the
mechanical backstops below may be disabled** and self-enforce the rule
above on every publishing tool. The agent's pre-flight check is the
only guard that works in every configuration.

- **Terminal (VS Code Copilot agent mode).** [.vscode/settings.json](../.vscode/settings.json)
  contains a denylist (`chat.tools.terminal.autoApprove` with `false`
  entries) for `git push`, `git reset --hard`, `git rebase`, `git merge`,
  `git cherry-pick`, `git tag`, destructive `git branch -d/-D`, and
  `gh pr create|merge|close|edit`. In Default Approvals mode each
  denied command requires an in-chat **Allow** click; that click is
  the approval. **In Bypass Approvals or Autopilot, this denylist is
  ignored at runtime** - the entries remain as documentation of
  which commands cross the publish boundary, and as a defense-in-depth
  tripwire for contributors and agents who *are* in Default Approvals
  mode. Do not rely on the denylist to stop you.
- **MCP and in-process tools are NOT covered by
  `terminal.autoApprove` in any mode.** The chat tool surface has its
  own per-tool approval flow which Bypass Approvals and Autopilot
  also disable. **Do not invoke any GitHub write tool**
  (`mcp_io_github_git_create_pull_request`,
  `mcp_io_github_git_update_pull_request`,
  `mcp_io_github_git_merge_pull_request`,
  `mcp_io_github_git_push_files`,
  `mcp_io_github_git_create_or_update_file`,
  `mcp_io_github_git_delete_file`,
  `mcp_io_github_git_update_pull_request_branch`,
  `github-pull-request_create_pull_request`, etc.) without an explicit
  publishing verb from the user in the most recent message.
- **Branch protection on `main`** (PR required, status checks required,
  force pushes and branch deletion blocked) is the only server-side
  guard that survives every approval mode. It does not stop
  `gh pr create|merge|close|edit`, MCP `create_pull_request`,
  `merge_pull_request`, or pushes to feature branches - only
  direct writes to `main` itself.

The agent's pre-flight check (re-read the user's most recent message
verbatim and confirm it contains an explicit publishing verb) is
load-bearing. If the message does not contain such a verb, stop and ask
one short yes/no question. Do not assume the user "obviously" wants the
change published, and do not assume an approval prompt will appear to
catch a mistake.

## General guidance

- Ensure code is cross-compatible with both .NET 10 and .NET Framework 4.7.2.
- Check `GlobalUsings.cs` for global usings and don't add unnecessary usings.
- **Prefer `Touki.EnumExtensions` over hand-written enum bitwise code** in
  production library code. `AreFlagsSet`, `AreAnyFlagsSet`,
  `IsOnlyOneFlagSet`, `SetFlags`, `ClearFlags` inline to the same
  instructions as `&`/`|`/`==` on both TFMs and avoid the `Enum.HasFlag`
  boxing penalty on net472/net481 (~20&times; faster, zero alloc).
- **When optimizing span-walking helpers**, read the
  [`framework-jit-optimization`](../.agents/skills/framework-jit-optimization/SKILL.md)
  skill and its bundled
  [references/framework-span-performance.md](../.agents/skills/framework-jit-optimization/references/framework-span-performance.md)
  first. The headline rule: on net472/net481 hoist
  `ref T = MemoryMarshal.GetReference(span)` out of the loop and walk with
  `Unsafe.Add<T>(ref, i)` for a 19-44% Framework win at no `unsafe`-keyword cost,
  and prefer one simple implementation unless net10 regresses measurably.
- **Prefer Touki utility types over their BCL counterparts** when building
  strings, buffers, or collections in production library code. They are
  designed to avoid managed allocation on the hot path:
  - `Touki.Text.ValueStringBuilder` instead of `System.Text.StringBuilder`.
    Always seed it with a stack buffer (`stackalloc char[256]` is the
    standard size); it rents from `ArrayPool<byte>` automatically if the
    content outgrows the buffer, and `ToStringAndDispose()` returns the
    final string while releasing the rental. Verified savings of
    58-63&nbsp;% allocated bytes when replacing `StringBuilder` in the
    `GlobMatcherFactory` bytecode encoder, with no measurable throughput
    cost.
  - `Touki.Text.StringSegment` / `Touki.Text.StringSpan` instead of
    substring allocation when slicing existing strings.
  - `Touki.Buffers.*` (rented arrays, span helpers) before reaching for raw
    `new char[]` / `new byte[]` in scratch code.
  - `Touki.Collections.*` (`ContiguousList`, `ArrayPoolList`,
    `SingleOptimizedList`, `EmptyList`) instead of `List<T>` /
    `Dictionary<,>` when the lifetime is bounded.
- **When choosing how a hot path gets a short-lived scratch buffer** (zeroed
  `stackalloc` vs `[SkipLocalsInit]` + `stackalloc` vs `BufferScope<T>` vs an
  `ArrayPool<T>.Shared` rental), follow the
  [`scratch-buffer-strategy`](../.agents/skills/scratch-buffer-strategy/SKILL.md)
  skill for the decision tree and the net481/net10 size crossovers, backed by
  its bundled
  [references/arraypool-performance.md](../.agents/skills/scratch-buffer-strategy/references/arraypool-performance.md).
- When adding a polyfill for a modern .NET API on .NET Framework, follow the
  [`polyfill-dotnet-api`](../.agents/skills/polyfill-dotnet-api/SKILL.md) skill:
  prefer Microsoft-shipped packages, then PolySharp source-gen for compiler
  attributes, and only hand-roll under
  `touki/Framework/Polyfills/<BclNamespace>/` as a last resort. See
  [docs/polyfill-layout.md](../docs/polyfill-layout.md) for the layout rules
  (namespace = BCL namespace; Touki-specific code stays under
  `touki/Framework/Touki/...`) and the `extern alias` recipe.

## Path-specific instructions

Additional rules apply to specific file types and live under
[.github/instructions/](instructions/). Tools that support the
`*.instructions.md` format (Copilot cloud agent, Copilot code review, VS Code) load them
automatically based on each file's `applyTo` glob.

Currently:

- [.github/instructions/msbuild.instructions.md](instructions/msbuild.instructions.md) - rules for `*.csproj`, `*.props`, `*.targets`.
- [.github/instructions/tests.instructions.md](instructions/tests.instructions.md) - conventions for `touki.tests/**/*.cs`.
- [.github/instructions/perf.instructions.md](instructions/perf.instructions.md) - BenchmarkDotNet conventions and the JIT-naming rule for `touki.perf/**/*.cs`.
- [.github/instructions/polyfills.instructions.md](instructions/polyfills.instructions.md) - the two non-negotiable rules for `touki/Framework/Polyfills/**/*.cs`.
