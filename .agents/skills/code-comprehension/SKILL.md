---
description: Evidence-based readability and cognitive-load review of code. Use when asked to "review this for readability", "is this too complex", "reduce nesting or cognitive load", "what is a reasonable method length / parameter count / nesting depth", or when judging whether code will be hard to understand. Screens code against research-backed thresholds and prioritizes the factors that actually drive comprehension - naming first, then structure that loads working memory.
license: MIT
metadata:
    applicability: universal
    binding: optional-overlay
    github-path: skills/code-comprehension
    github-pinned: v0.10.0
    github-ref: refs/tags/v0.10.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: d2e1be7b63a31345f054a1d0785a67d5ba2aa604
    maturity: canary
    portability: portable
    related: pre-pr-self-review
    requires: none
    risk: advisory
name: code-comprehension
---
# Code comprehension review

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

A research-backed screen for how hard code is to *understand* (not how hard it is
to test). Four decades of studies - including recent eye-tracking, EEG, and fMRI
work - converge on a few high-confidence rules. Lead with those; treat the
threshold numbers as **screening heuristics, not hard limits**, and let a
consuming repo's own style guide or `.editorconfig` win on any conflict.

## The high-confidence rules (apply these first)

1. **Naming is the #1 factor.** Across 40 years of studies, identifier quality
   beats every structural metric. **Misleading names are worse than meaningless
   ones.** Fix misleading or cryptic names before touching anything else; prefer
   full words over abbreviations.
2. **Cognitive load comes from working memory, not path count.** Deep nesting,
   long parameter lists, and dense data-flow (many live variables a reader must
   track) strain comprehension. McCabe's cyclomatic complexity is a weak proxy
   for human effort - use it for testing, not readability.
3. **Indentation and layout matter a lot.** Consistent indentation roughly
   *halves* reading time versus unindented control flow; blank lines between
   logical chunks help.
4. **Complexity perception saturates.** Past a point (around cognitive complexity
   15) more complexity barely changes perceived difficulty - so spend effort
   *avoiding the extremes*, not micro-optimizing already-simple code.
5. **Small reviews work.** Review effectiveness drops faster than linearly with
   size; keep a change under roughly 200 lines so a reviewer can read it linearly.

## Screening thresholds

Flag for review at "Review", refactor at "Refactor". Values between "Low" and "Review" are "moderate" — usually acceptable, but worth a quick "why" check.
Numbers aggregate research and industry-tool convergence; adjust to the team and language.

| Factor | Low | Review | Refactor | Measure |
| --- | --- | --- | --- | --- |
| Nesting depth | 1-2 | 4-5 | >=6 | max nested control blocks |
| Cognitive complexity (Sonar) | <=5 | 11-15 | >=16 | rule-based, penalizes nesting |
| Method length | <=20 | 51-100 | >100 | SLOC (no blanks/comments) |
| Parameter count | <=3 | 6-8 | >=9 | params per method |
| Boolean terms in an expression | <=2 | 5-6 | >=7 | operands / sub-expressions |
| Line length | <=80 | 101-120 | >=121 | columns |
| Data-flow (DepDegree) | few | many | dense, long-range | live def-use links |
| Coupling (CBO) | <=5 | 10-14 | >=15 | classes a class depends on |
| Cohesion (LCOM4) | 1 | 3-4 | >=5 | connected components in a class |

Cyclomatic complexity (McCabe) <=10 is the universal *testing* threshold - keep
it, but don't use it to argue about readability.

## Review priority order

1. **Names** - scan for misleading identifiers first, then cryptic abbreviations;
   ensure consistent vocabulary.
2. **Working-memory load** - nesting depth, parameter count, and data-flow
   (variable lifespans, cross-branch sharing). Extract methods, introduce
   well-named temporaries, or use a parameter object.
3. **Size** - over-long methods and over-large changesets; decompose.
4. **Layout** - consistent indentation, blank lines between chunks, line length.
5. **Stop at the extremes** - don't churn already-simple code to shave a metric.

Tailor to the audience: novices are hit hardest by nesting, recursion, and poor
names; default a mixed team to the novice-friendly choice.

## Evidence

The factor-by-factor research, the metric glossary, industry-tool thresholds, and
the full citation list are in [references/research.md](references/research.md).
The threshold numbers above are screening heuristics drawn from that evidence; the
high-confidence rules at the top are the parts to rely on. A consuming repository
binds its own style guide and any house thresholds in its overlay.
