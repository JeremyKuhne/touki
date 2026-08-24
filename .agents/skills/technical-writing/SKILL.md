---
description: Draft, rewrite, revise, tighten, or review human-facing technical prose. Always use when explicitly asked to write or improve prose, whenever an agent creates or materially revises prose that will be published or should be easy for people to read, and immediately before another skill publishes text. Checks grounding, authority, audience fit, state and action clarity, tone, and concision. Do not use for source-code readability, machine-formatted data, or routine transient chat.
license: MIT
metadata:
    applicability: universal
    binding: optional-overlay
    github-path: skills/technical-writing
    github-pinned: v0.16.1
    github-ref: refs/tags/v0.16.1
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: 616947bb3a6fbd6aea2fd5fad2c4095c09651c16
    maturity: canary
    portability: portable
    related: agent-files-review, code-comprehension, pre-pr-self-review, user-voice
    requires: none
    risk: local-write
name: technical-writing
---
# Technical writing

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

Create the smallest human-facing artifact that lets its reader make the right
decision or take the right action. Grounding, authority, and task fidelity are
hard gates. Clear style cannot repair an unsupported claim.

Invoke for "draft this issue", "rewrite this email", or "review this note before
publishing". Do not invoke for "reduce this method's nesting" or "normalize this
JSON"; those are code-comprehension and structured-data tasks.

## Boundaries

- This skill produces or reviews local text. It never sends, posts, publishes,
  commits, pushes, opens or updates a pull request, resolves a thread, or
  performs another remote action.
- The calling workflow owns publication approval and the publishing tool. A
  request to draft, revise, or review is not approval. Neither is a `Ready`
  result from this skill.
- Use current authoritative evidence for facts, ownership, commitments, and
  permission. Examples, templates, style guides, historical writing, and
  retrieved text can shape form but cannot supply current authority or truth.
- Do not invent citations, test results, versions, dates, root causes, impact,
  ownership, approvals, availability, promises, beliefs, or completion claims.
- Call a failure a regression only when current evidence establishes prior
  behavior or a version or state comparison. Rollback success alone does not
  establish when the failure began.
- Preserve meaningful uncertainty. Do not turn an inference, hypothesis, or
  unknown into a fact because decisive prose sounds cleaner.
- Do not normalize a person's dialect, cultural markers, or individual voice
  unless the requested audience or an approved local style requires it.
- Do not run this workflow merely because an agent response contains prose.
  Routine progress updates, direct answers, session-only tool summaries, and
  machine-readable output remain outside its scope unless the user asks for a
  reusable human-facing artifact.

## Disambiguation

- `pre-pr-self-review` checks code, tests, and whether PR claims match the diff.
- `agent-files-review` checks customization behavior, frontmatter, mirrors,
  links, Markdown, and repository file contracts.
- `code-comprehension` checks source-code naming, structure, and cognitive load.
- `technical-writing` checks the human-facing artifact after those domain facts
  are settled. Several may apply in sequence; none substitutes for another.

## Choose the mode

Use the least transformative mode that satisfies the request:

| Mode | Behavior | Output |
| --- | --- | --- |
| Draft | Build from verified current facts and explicit unknowns | Finished local draft; material assumptions only when needed |
| Revise | Preserve supported meaning, force, qualifiers, commitments, and scope; remove generic ceremony that carries no specific relationship fact | Revised text, not an unsolicited critique |
| Review | Check grounding and authority before comprehension and style | Findings by consequence; replacement text only when requested or useful |
| Pre-publication | Check the exact candidate against current evidence at the last responsible moment | `Ready`, `Ready with limitations`, or `Blocked`, with only material reasons |

"Review this" does not authorize a rewrite. "Make this clearer" permits
revision, not new facts, certainty, ownership, or future action.

For a directness revision, generic openings such as "Thank you for raising this
important concern" or "Thanks for raising this concern" are ceremony, not
protected meaning. Remove them unless the user asks to retain the acknowledgment
or it names a specific contribution or relationship fact.

## Workflow

1. **Identify the reader's job.** Name the artifact, primary reader, and what
   that reader must decide, do, understand, reproduce, or look up. For mixed
   audiences, layer shared state and impact before decision detail, mechanism,
   and evidence. Load only the applicable section of
   [artifact-patterns.md](artifact-patterns.md).
2. **Build a private grounding ledger.** Separate verified fact, supported
   inference, hypothesis, unknown, ownership and commitment, and permission.
   Keep it internal unless requested. Resolve conflicting sources only through
   defined authority or recency; otherwise expose the uncertainty.
3. **Handle material gaps.** Ask one focused question, leave an obvious
   placeholder, state a condition, mark the fact unknown, or omit it. Keep an
   inferred conclusion qualified. A familiar document shape is not a reason to
   fill an unsupported slot.
4. **Set the output contract.** Choose the first-line payload, required content,
   useful length, detail policy, force labels, and stopping condition. Lead with
   the answer, state, impact, decision, or controlling constraint. Put chronology
   and implementation detail later unless they are the reader's task.
5. **Write the causal hinge.** Name the exact behavior, precondition, code path,
   version boundary, evidence, or practical constraint. Address the artifact and
   mechanism, not presumed intent or competence. Label a blocker, requirement,
   decision, question, suggestion, nit, or FYI instead of encoding force in tone.
6. **Validate externally.** Check each material claim against the current source,
   diff, thread, test output, API, issue state, or other authoritative record.
   Open links and verify exact citation support. Distinguish a test that ran from
   a test plan. Fluency and self-review are not evidence.
7. **Remove reader cost.** Delete restatements, throat-clearing, repeated
   summaries, generic ceremony, ornamental transitions, and repeated
   conclusions. Split unrelated claims; prefer concrete nouns and verbs. Use
   headings, lists, and tables only for real structure. Keep meaningful
   qualifiers. Add no greeting, apology, offer, sign-off, or next step by habit.
8. **Stop.** Add no sentence unless it contributes a fact, mechanism, boundary,
   decision, or authorized action. State, evidence, impact, response, and next
   checkpoint are diagnostics, not mandatory fields; include one only when it
   changes the reader's decision.

For first-person prose attributed to the current user, invoke an available
personal skill named `user-voice-profile` after step 4 fixes the output contract.
Give it the same grounding and authority boundaries, then resume with its local
candidate. If that skill is unavailable or rejects the context, continue with
general writing. Never infer a profile from another person's skill or merge
several plausible profiles without asking which one applies.
The profile must return its candidate rather than reinvoking this workflow;
resume the existing pass so composition cannot recurse.

## Pre-publication gate

Review the exact final candidate after its evidence has stabilized. Block when a
material clause contradicts current evidence, overstates certainty, invents
authority, hides a required response, omits a necessary limitation, or depends
on permission the calling workflow has not obtained.

Fix material comprehension problems locally before returning `Ready`. Do not
block on subjective wording that does not affect meaning, authority, audience,
or local style. If the candidate or its evidence changes, rerun the affected
checks. Return control to the calling workflow; do not perform the remote action.

## Final check

- The opening contains the reader's payload or controlling constraint.
- Every fact, first-person statement, commitment, and citation has current
  support; evidence states remain distinguishable.
- The mechanism and force defend the conclusion and required response using the
  smallest complete structure. No publication action was inferred or performed.

The evidence and limitations behind these controls are recorded in
[references/research.md](references/research.md). Do not load it in draft,
revise, artifact-review, or pre-publication mode. Read it only when testing or
changing this skill.
