---
name: address-pr-feedback
description: Address feedback on an existing pull request &mdash; review comments, requested changes, CI failures, or any post-PR follow-up work. Use when the user says "address the review", "fix the comments", "address Copilot's feedback", "fix the CI failure", or any similar phrasing. Distinct from `create-pr`, which covers opening the *initial* PR.
---

# Address PR feedback

This skill is the post-PR counterpart to [`create-pr`](../create-pr/SKILL.md).
Both skills share the **same** publish gate: neither `git commit` nor
`git push` runs without an explicit publishing verb from the user. The
difference is what each skill authorizes you to *edit*:

- `create-pr` authorizes preparing a new PR from in-progress work —
  branching, staging, proposing a commit message. The commit and push
  still wait on approval.
- This skill authorizes editing files in response to review feedback or
  CI failures on an existing PR. Same approval gate before commit/push.

The
["Working with the user on changes"](../../../AGENTS.md#working-with-the-user-on-changes)
section of AGENTS.md is the source of truth for the commit/push approval
rule. Re-read it at the start of every invocation; this skill is the
decision-point reminder, not a replacement.

## Recognizing approval

Skim the most recent user message for an explicit publishing verb before
you stage, commit, or push. Pattern-match the words, do not infer intent.

**Approval** &mdash; verbs that authorize publishing the current change:
`commit`, `push`, `update the PR`, `ship it`, `send it`, `yes push`, or
direct synonyms when paired with a publishing intent.

**Not approval** &mdash; everything else, including these phrasings that
have caused violations on this repo:

- "Address the review comments."
- "Reply to the comments on the PR."
- "Fix the comments / fix the CI failure."
- "Look at Copilot's feedback / see what they said."
- "See what you can do about this."
- "Try a different approach."
- "What about &lt;suggestion&gt;?"
- "Do the next step" / "finish the rollout" / "go ahead to the next
  thing" / a bare "go ahead" attached to a task description.
- A reviewer (human or Copilot) leaving a new comment.
- A failing check on the PR.

If you are uncertain whether a phrase is approval, **it is not approval**.
Stop and ask one short yes/no question.

## Workflow

1. **Fetch the feedback.** Use the GitHub PR tools (or
   `Invoke-RestMethod` against
   `api.github.com/repos/<owner>/<repo>/pulls/<N>/comments`) to read review
   comments. Read PR-level conversation comments and check-run logs too if
   relevant.
2. **Plan the response.** Decide which comments require code changes,
   which are out of scope, and which you disagree with. For comments you
   disagree with, plan a written response rather than silently overriding.
3. **Edit files.** Make the code changes. Run the build and any relevant
   tests. The applicable validation rules are still
   [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) &mdash; a follow-up
   round needs the same checks as the initial PR.
4. **Stop. Describe.** Summarize what you changed, why, and what (if
   anything) you chose not to act on. Do **not** run `git add`, `git
   commit`, or `git push`.
5. **Wait** for an explicit publishing verb (see "Recognizing approval"
   above).
6. **Only then** stage by path, commit with a message that summarizes the
   round of changes, and push. The staging/commit/push mechanics are the
   same as in [`create-pr`](../create-pr/SKILL.md) §3&ndash;§4.

## When you've already violated the rule

Acknowledge the violation directly without minimizing. Do **not** push a
follow-up commit to "fix" the situation without explicit approval &mdash;
that compounds the failure. The user decides whether to revert,
force-push, or leave the commit in place.

## Related

- [AGENTS.md](../../../AGENTS.md) &mdash; the rule itself.
- [`create-pr`](../create-pr/SKILL.md) &mdash; opening the initial PR
  (different approval semantics).
- [`pre-pr-self-review`](../pre-pr-self-review/SKILL.md) &mdash; validation
  checklist that applies to both initial and follow-up rounds.
