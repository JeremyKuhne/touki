---
description: Address feedback on an existing pull request - review comments, requested changes, CI failures, or any post-PR follow-up work. Use when the user says "address the review", "fix the comments", "address Copilot's feedback", "fix the CI failure", or any similar phrasing. Distinct from `create-pr`, which covers opening the *initial* PR.
license: MIT
metadata:
    github-path: skills/address-pr-feedback
    github-pinned: v0.5.0
    github-ref: refs/tags/v0.5.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: ca7e22e8fbf08581099ae31c44e14421adbf1ef2
    portability: semi-portable
name: address-pr-feedback
---
# Address PR feedback

This skill is the post-PR counterpart to the `create-pr` skill. Both share the
**same** publish gate: neither `git commit` nor `git push` runs without an
explicit publishing verb from the user. The difference is what each skill
authorizes you to *edit*:

- `create-pr` authorizes preparing a new PR from in-progress work -
  branching, staging, proposing a commit message. The commit and push
  still wait on approval.
- This skill authorizes editing files in response to review feedback or
  CI failures on an existing PR. Same approval gate before commit/push.

Your repo's agent guidance (the "Working with the user on changes" rules in
`AGENTS.md`) is the source of truth for the commit/push approval rule. Re-read it
at the start of every invocation; this skill is the decision-point reminder, not
a replacement.

## Recognizing approval

Carefully read the most recent user message and identify whether it
contains an explicit publishing verb before you stage, commit, or push.
Pattern-match the words, do not infer intent.

**Approval** - verbs that authorize publishing the current change:
`commit`, `push`, `update the PR`, `ship it`, `send it`, `yes push`, or
direct synonyms when paired with a publishing intent.

**Not approval** - everything else, including these phrasings that
have repeatedly caused violations:

- "Address the review comments." / "Reply to the comments on the PR." /
  "Fix the comments / fix the CI failure."
- "Look at Copilot's feedback / see what they said."
- "See what you can do about this." / "Try a different approach."
- "Do the next step" / "finish the rollout" / "go ahead to the next
  thing" / a bare "go ahead" attached to a task description.
- A reviewer (human or Copilot) leaving a new comment, or a failing
  check on the PR.

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
   tests. The applicable validation rules are still the `pre-pr-self-review`
   checklist - a follow-up round needs the same checks as the initial PR.
4. **Stop. Describe.** Summarize what you changed, why, and what (if
   anything) you chose not to act on. Do **not** run `git add`, `git
   commit`, or `git push`.
5. **Wait** for an explicit publishing verb (see "Recognizing approval"
   above).
6. **Only then** stage by path, commit with a message that summarizes the
   round of changes, and push. The staging/commit/push mechanics are the
   same as in the `create-pr` skill (its "Commit changes" and "Push the
   branch" steps).

## When you've already violated the rule

Acknowledge the violation directly without minimizing. Do **not** push a
follow-up commit to "fix" the situation without explicit approval -
that compounds the failure. The user decides whether to revert,
force-push, or leave the commit in place.

## Related

- Your repo's agent guidance (`AGENTS.md`) - the rule itself.
- The `create-pr` skill - opening the initial PR (different approval
  semantics).
- The `pre-pr-self-review` skill - the validation checklist that applies
  to both initial and follow-up rounds.
