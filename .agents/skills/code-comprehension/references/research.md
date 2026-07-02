# Code comprehension - the evidence

Backing detail for the [code-comprehension](../SKILL.md) skill: the factor-by-factor
research, the metric glossary, industry-tool thresholds, and the citation list.
The skill core distills the high-confidence parts; this page is the evidence and
the lower-confidence frontier, so you can judge how hard to lean on any one number.

> **How to read the thresholds.** They are *screening heuristics* aggregated from
> empirical studies and widely used industry tools, not hard limits. Several
> factors (naming, expression clarity, recursion style) are categorical, so their
> "ranges" describe typical states, not numeric cut-offs.

## Threshold table

| Factor | Low | Moderate | High | Very high | Metric |
| --- | --- | --- | --- | --- | --- |
| Nesting depth | 1-2 | 3 | 4-5 | >=6 | max nested control blocks |
| Cyclomatic complexity (McCabe) | <=5 | 6-10 | 11-20 | >=21 | independent paths (testing proxy) |
| Cognitive complexity (Sonar) | <=5 | 6-10 | 11-15 | >=16 | rule-based understandability |
| Method length | <=20 | 21-50 | 51-100 | >100 | SLOC, no blanks/comments |
| Parameter count | <=3 | 4-5 | 6-8 | >=9 | params per method |
| Line length | <=80 | 81-100 | 101-120 | >=121 | columns |
| Identifier naming | descriptive, consistent | short / abbreviations | single letters in locals | misleading / inconsistent | qualitative |
| Boolean / expression terms | <=2 | 3-4 | 5-6 | >=7 | operands / sub-expressions |
| Recursion | tail / simple | single + obvious base case | mutual / non-obvious termination | deep, intertwined | presence and depth |
| Data-flow (DepDegree) | few def-use links | moderate | many interdependent vars | dense, long-range | live def-use edges |
| Layout / whitespace | consistent indent, blank lines | minor inconsistency | irregular indent, dense | no indent / hard wraps | indent consistency |
| Coupling (CBO) | <=5 | 6-9 | 10-14 | >=15 | classes a class depends on |
| Cohesion (LCOM4) | 1 | 2 | 3-4 | >=5 | connected components in a class |
| Inheritance depth (DIT) | <=2 | 3-4 | 5-6 | >=7 | depth of inheritance tree |

## Metric glossary

- **Cyclomatic complexity (McCabe).** Independent paths through a method (decision
  points plus one). Useful for *testing* effort; a weak proxy for human
  comprehension - a 2023 neuroscience study (222 developers) confirmed poor
  correlation with measured cognitive load.
- **Cognitive complexity (Sonar).** Rule-based score built to mirror human
  understandability: adds for flow breaks, adds *extra* for nesting, ignores
  structures perceived as simple. Validated to correlate with comprehension time.
- **DepDegree (data-flow).** Counts definition-use edges - how many variable
  relationships a reader must track. Relates to brain-measured load more strongly
  than control-flow-only metrics.
- **Coupling Between Objects (CBO).** Classes a class depends on directly; high
  values track with defect density and comprehension difficulty.
- **Lack of Cohesion of Methods (LCOM4).** Connected components in a class's
  method graph; 1 = single responsibility, higher = split candidate.

## Factor details (what / why / guardrail)

1. **Nesting depth and indentation.** Each level adds a simultaneously-active
   condition; indentation is the visual scaffold. Indented code is read materially
   faster (RCTs find unindented control flow roughly doubles reading time). Prefer
   <=2-3 levels; refactor >=4 by extracting methods or flattening.
2. **Cyclomatic vs cognitive complexity.** McCabe for testing; cognitive
   complexity for readability (its nesting penalty is the point). Keep methods at
   <=10 cognitive where feasible.
3. **Method length.** Larger units combine more decisions, names, and data-flow.
   Aim <=20 SLOC, review at 50+, refactor above 100.
4. **Parameter count.** Parameters are vocabulary a reader holds in mind; count
   correlates with working-memory brain activation. Prefer <=3; 4-5 = "explain
   why"; 6+ = parameter object.
5. **Line length and whitespace.** Long lines reduce scan anchors. <=100 columns
   for code (<=80 for prose); add blank lines between logical chunks.
6. **Identifier naming.** The primary carrier of meaning and the #1 factor in a
   40-year review (named in 16 studies, ahead of every complexity metric).
   Full-word names sped professionals ~19% in one study; misleading names are
   worse than meaningless. Rename misleading identifiers first.
7. **Expression complexity and explaining variables.** For experts, flat vs nested
   boolean forms are often equivalent; well-named intermediate variables help when
   an expression is genuinely hard. Prefer <=4 boolean terms; avoid long fluent
   chains without breaks.
8. **Recursion.** Readers must simulate the call stack and termination. Prefer
   tail/simple recursion; make the base case and state explicit; consider an
   iterative form when depth or state is non-obvious.
9. **Data-flow dependencies.** Interleaved definitions and uses force
   back-referencing. Keep variable lifespans short; reduce cross-branch sharing;
   one purpose per variable per block.
10. **Regularity / repetition.** Readers invest most in the first occurrence of a
    pattern; later repeats cost less. Prefer consistent idioms; avoid needless
    irregularity.
11. **Coupling and cohesion.** Keep CBO <=9; LCOM4 = 1 ideal, refactor at >=3.
    High coupling and low cohesion both track with maintenance and comprehension
    problems.

## Industry-tool convergence

- Cyclomatic complexity: **10** (near-universal).
- Method length: **~25 lines** (tools vary 20-30).
- Parameter count: **4**.
- Nesting depth: **4** (SonarQube, ESLint).
- File length: **250-500 lines** (wide variation).

Defaults by tool: SonarQube (cognitive 15, method 50), CodeClimate (method
complexity 5, file 250), ESLint (complexity 20, max-depth 4), Visual Studio
maintainability index (green 20-100, red 0-9).

## Process factors (well-supported)

- **Review size.** Effectiveness drops faster than linearly with changeset size;
  under ~200 lines enables linear reading. Tool-assisted review yields about 2x
  the accepted comments of over-the-shoulder review.
- **Audience.** Novices are hit hardest by nesting, recursion, and weak names and
  benefit most from consistent indentation; default mixed teams to the
  novice-friendly choice.

## Frontier (interesting, lower confidence)

Treat these as directional, not as guardrails - small samples, single studies, or
hard-to-reproduce instrumentation:

- **Physiological measures.** Eye-tracking shows non-linear read patterns; EEG
  theta rises and alpha falls with effort; fNIRS detects prefrontal load. Reported
  accuracies (e.g. ~89% load detection, ~97% expertise prediction) come from small
  lab studies.
- **Paradigm effects.** Reported comprehension deltas - reactive vs OOP (~15-25%),
  async/await debugging (~30%), static vs dynamic typing trade-offs - are single
  studies with modest cohorts.
- **ML smell detection.** Transformer models report 90%+ precision but 50-60%
  recall; many flagged issues are never refactored (the actionability gap).

## Citations

The claims above trace to (among others): Sonar Cognitive Complexity whitepaper;
Munoz Baron et al. 2020 (cognitive-complexity meta-analysis); Peitek et al. 2021
(fMRI); Beyer et al. 2014 (DepDegree); Hofmeister et al. 2017 and Avidan &
Feitelson 2017 (naming); the 40-year naming review (2022); Hanenberg et al. 2024
and Miara et al. 1983 (indentation); Buse & Weimer 2010 (readability model);
Jbara & Feitelson 2017 (regularity); Stoeckert et al. 2024 (recursion); and ICPC
2025 review-strategy work. Each entry is searchable by author and year for the
full source.
