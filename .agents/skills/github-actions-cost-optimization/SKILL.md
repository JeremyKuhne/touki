---
compatibility: Requires access to the repository's GitHub Actions workflows; precise estimates require current GitHub billing rates and representative job durations.
description: Audit and reduce GitHub Actions runner usage and normalized cost without weakening required validation. Use when asked to reduce CI cost, spend, minutes, or job count; optimize workflow triggers, matrices, runner selection, caches, or artifact retention; eliminate duplicate Actions runs; or decide which checks should be automatic versus scheduled or manual. Not for application runtime performance, which belongs in a performance-testing workflow.
license: MIT
metadata:
    applicability: git-github
    binding: optional-overlay
    github-path: skills/github-actions-cost-optimization
    github-pinned: v0.13.0
    github-ref: refs/tags/v0.13.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: 1fac6778d86ec0879641e2b593e881f49b43c648
    maturity: canary
    portability: portable
    related: engineering-baseline, security-review
    requires: none
    risk: local-write
name: github-actions-cost-optimization
---
# GitHub Actions cost optimization

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

Reduce the cost and latency of GitHub Actions while preserving the checks that
make a change safe to merge and release. Optimize the repository's full change
lifecycle, not one conspicuous job: duplicate triggers, repeated setup, matrix
fan-out, retries, and retained storage can outweigh the longest individual step.

This skill applies to requests such as "reduce our CI spend", "why does this
workflow use so many minutes?", "move expensive checks out of every PR", and
"choose cheaper runners without losing coverage". It should not trigger for
"make this parser faster" or "reduce allocations in this method"; those are
application performance questions.

## Non-negotiable rule

State the validation invariants before proposing savings. A cheaper workflow
that no longer tests the merge candidate, a supported platform, a release
configuration, or a security boundary is not an optimization. Treat changes to
CodeQL frequency, untrusted-code permissions, branch protection, and merge
bypass paths as security or governance decisions, not accounting details.

For public repositories, standard GitHub-hosted runners may have no invoiced
minute charge. Still report normalized list-price cost and runner usage so
alternatives remain comparable and the workflow stays economical if repository
visibility or billing policy changes. Keep actual invoiced cost distinct from
that normalized comparison. Public and private repositories can receive
different hardware for the same runner label; when they do, label the normalized
number as a synthetic rate-weighted comparison, not a visibility-change forecast.

## Workflow

1. Read [audit.md](audit.md). Inventory workflows, triggers, required checks,
   merge paths, runner types, matrices, setup duplication, storage, and recent
   durations. Identify every automatic run caused by one logical change.
2. Write down the load-bearing invariants and classify each check as automatic
   blocking, scheduled/manual breadth, or release-only. Do not reclassify a check
   merely because its runner is expensive.
3. Read [cost-model.md](cost-model.md). Calculate the current per-PR, per-merge,
   and periodic cost from current official rates and observed job durations.
   State assumptions and show both actual and normalized cost where they differ.
4. Read [optimizations.md](optimizations.md). Rank changes by savings,
   confidence, and validation risk. Prefer removing work over making redundant
   work slightly faster.
5. Implement the smallest coherent set of workflow changes. Keep repository-
   specific commands, required status names, platform obligations, and ruleset
   details in the consuming repository's overlay.
6. Validate workflow syntax, local build/test equivalents, required-context
   behavior, and at least one hosted run for every retained native platform.
   Compare observed durations with the estimate and record any gap.

## Required output

Report:

- the current and proposed job/trigger topology;
- observed durations, runner hardware, invocation assumptions, and the rate
  source/date;
- actual and normalized cost per PR, merged change, and periodic run;
- validation invariants preserved automatically and breadth moved elsewhere;
- expected savings, residual risks, and the hosted checks still required;
- workflow files changed and deterministic validation performed.

Do not present a precise percentage without exposing its inputs. Do not claim
that a manual or scheduled workflow preserves coverage unless it has a named
owner, trigger, and occasion or cadence.

## Remote boundary

Workflow edits are local and reversible. Changing repository rulesets, required
status checks, budgets, merge-queue settings, or Actions retention settings is a
remote operation: propose the exact change and wait for explicit approval.
Committing, pushing, and opening a pull request remain separate approval
boundaries defined by the consuming repository.

## Sub-pages

- [audit.md](audit.md) - inventory, validation invariants, and evidence
  collection.
- [cost-model.md](cost-model.md) - actual versus normalized cost calculations
  and the reporting table.
- [optimizations.md](optimizations.md) - ordered savings levers, guardrails, and
  validation traps.
