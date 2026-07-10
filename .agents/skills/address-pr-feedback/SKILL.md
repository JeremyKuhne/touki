---
compatibility: Requires git and either a GitHub integration or authenticated gh for remote pull-request operations.
description: Address feedback on an existing pull request - review comments, requested changes, CI failures, or any post-PR follow-up work. Use when the user says "address the review", "fix the comments", "address Copilot's feedback", "fix the CI failure", or any similar phrasing. Distinct from `create-pr`, which covers opening the *initial* PR.
license: MIT
metadata:
    applicability: git-github
    binding: optional-overlay
    github-path: skills/address-pr-feedback
    github-pinned: v0.10.0
    github-ref: refs/tags/v0.10.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: ceb544da854ef7ba911679b8af8bca2631150ef9
    maturity: canary
    portability: portable
    related: create-pr, pre-pr-self-review, agent-files-review
    requires: none
    risk: remote-write
name: address-pr-feedback
---
# Address PR feedback

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

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

1. **Fetch the feedback.** Read review comments, PR conversation, and check-run
   logs via the GitHub PR tools (or `Invoke-RestMethod` against
   `api.github.com/repos/<owner>/<repo>/pulls/<N>/comments`). With multiple
   review passes, fetch the **latest** review's comments (filter by the newest
   review id) so you act on the current round.

   Automated reviewers (e.g. Copilot) post asynchronously - on open, on push, or
   when requested - a minute or two after the trigger. If one was requested but
   hasn't posted, say so and act when the user reports comments (or check once);
   don't poll. Verify their comments per step 2 - they produce confident false
   positives.
2. **Plan, and verify each comment.** Don't fix something just because a reviewer
   (especially a bot) flagged it: confirm the claim against the code, and prove
   it when checkable (a REPL check, a build, a test) - a fix to a false positive
   can introduce the bug the reviewer imagined. Classify each:
   - **Valid** - real issue; fix it.
   - **Nit** - minor; fix if cheap, else note it.
   - **Out of scope** - plan a written reply.
   - **False positive / disagree** - plan a written explanation, not a change.
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
7. **Resolve the threads, with explanations.** Replying and resolving are remote
   actions, so they ride in the same approved publish step as the push; honor
   explicit scoping ("push and resolve only", "don't re-request") and report
   what you did. For each comment, reply then resolve:
   - **Fixed** - one line on what changed (reference the commit or behavior).
   - **False positive / won't-fix** - the rationale or the evidence. Leave a
     thread open only to invite a human onto a contested point, and say so.
   With the PR tool, use its resolve action; with `gh`, resolve via the GraphQL
   `resolveReviewThread` mutation on the thread node id (not the comment id).
8. **Re-request review when non-trivial.** After real code changes, request a
   fresh pass from the same reviewer - also a remote action in the publish step.
   Skip it for trivial rounds (typo, reword, one-line nit) to avoid an endless
   trickle, and say which you did.

**When to stop.** Later auto-review passes drift toward nits and false positives.
Once comments stop being substantive, stop re-requesting, say so, and let the
user merge.

## When you've already violated the rule

Acknowledge the violation directly without minimizing. Do **not** push a
follow-up commit to "fix" the situation without explicit approval -
that compounds the failure. The user decides whether to revert,
force-push, or leave the commit in place.

## Related

- Your repo's agent guidance (`AGENTS.md`) - the rule itself.
- The `create-pr` skill - opening the initial PR (same publish gate,
  different edit scope).
- The `pre-pr-self-review` skill - the validation checklist that applies
  to both initial and follow-up rounds.
- The `agent-files-review` skill - for a CI failure from the *agent-files*
  workflow specifically; its checklist owns the frontmatter, mirror, and
  link rules.
