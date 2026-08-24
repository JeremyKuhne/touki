# Artifact patterns

Load only the section for the current artifact. These are selection rules, not
templates. Remove any field that does not change the reader's decision.

## Short reply or coordination note

State the answer, availability, decision, or requested action. Add one causal or
boundary fact when needed, then stop. Do not add a greeting, thanks, recap, or
offer unless the relationship or request calls for one.

## Email

Give the subject a purpose and object, such as a decision, blocked state, status,
or FYI. Put the state or request in the first two sentences. Add only the context
needed to respond. End with the response contract when one exists: who acts,
what is needed, when, and what happens without it.

Use only supplied relationship facts, warmth, apologies, availability,
commitments, and deadlines. Move a circular or emotionally charged exchange to
a synchronous channel when that is authorized and practical, then preserve the
resulting facts and decision in the durable record.

## Status, incident, or decision note

Lead with the current state or decision needed. Follow with the affected
scenario and practical consequence, then the evidence boundary, current action,
required response, and next checkpoint. Put chronology and full logs below that
summary.

Use honest status words. `Blocked` requires an external dependency. `Resolved`
requires verified recovery. Tie the next update to a time or evidence trigger;
do not invent one merely to complete the format.

## Commit message

Derive the subject from the actual diff and match the repository's established
style. A durable default is a short imperative subject, a blank line, then a body
only when motivation, prior behavior, compatibility, or a non-obvious consequence
needs to survive the diff.

Do not invent an issue reference, bug, performance result, validation claim, or
compatibility rationale. Avoid listing files or narrating implementation already
clear from the patch.

## Issue

For a defect, state the observable failure and affected scenario, then impact,
minimal reproduction, expected behavior, evidence, and material unknowns. Keep a
root-cause hypothesis separate from the observed failure.

For a proposal, state the concrete gap, scenarios, constraints, bounded outcome,
alternatives, and risks. Code or API shape comes after the reader can see why the
surface is needed.

## Pull request

Optimize for review. Use a short prose summary when the primary change and its
reason form one causal argument; do not fragment that explanation into bullet
labels. Identify adjacent changes and explicit non-goals, report validation that
actually ran and known gaps, then surface compatibility, risk, and review focus.
Use bullets for discrete validation results and numbered review-focus items when
their order or priority matters.

Format the body as remote Markdown, not as a hard-wrapped repository document.
Keep each prose paragraph and each individual list item on one physical source
line. Use line breaks only between logical blocks or where Markdown syntax or
content requires them, such as headings, separate list items, tables, and fenced
code blocks.

Do not fill every repository template section when it carries no information.
Do not call work low risk, comprehensive, fixed, or fully tested without the
evidence that establishes that claim.

## Review comment or thread reply

Keep one concern per comment. Use the smallest form that closes the reasoning
gap:

1. Label the force: blocker, requirement, question, suggestion, nit, or FYI.
2. Name the exact observed code, behavior, claim, or result.
3. Explain the consequence or governing constraint.
4. State the smallest adequate response when one is required.

Address the artifact rather than the author's intent or competence. Ask a
diagnostic question only when the answer can change the disposition. A fix reply
states what changed and the observed validation. A disagreement states the
evidence and boundary. Neither needs ceremonial praise.

For `Blocking` or `Required`, state the correction directly rather than
softening it into an optional-sounding request. Name the concrete consequence,
such as another outbound request after cancellation, rather than using an
abstract label such as "breaks semantics" in place of the mechanism.

## Design discussion

Lead with the constraint that controls the result. Separate the valid concern
from the part that does not govern this scenario, then connect the evidence
boundary to the narrower alternative. Once one mechanism establishes why the
proposal is invalid, do not restate the same isolation, compatibility, or risk
point in another paragraph before reaching the recommendation.

## Source comment

Write a comment only when the code cannot express a contract, invariant,
external constraint, non-obvious reason, compatibility boundary, ownership or
lifetime rule, or removal condition clearly enough. Document observable
behavior when the original rationale is unknown; do not invent author intent.

Prefer a clearer name or smaller function when that removes the need for the
comment. A TODO needs concrete work plus an issue, owner, date, or removal event
that makes it actionable.

## Public API documentation

Put the most important consumer-visible behavior first. Document purpose,
parameter meaning and bounds, defaults and sentinel values, return and absence
behavior, exceptions, nullability, ownership, lifetime, mutation, concurrency,
ordering, prerequisites, platform or version limits, and performance only when
the implementation or approved contract establishes them.

Compile examples, run procedures, and verify links. Do not infer a public
contract from a plausible signature or make every member look documented with
generic text.

## Longer documentation

Choose the form by reader job:

| Reader job | Form | Controlling content |
| --- | --- | --- |
| Learn through a guided experience | Tutorial | Reliable sequence and visible results |
| Complete a real task | How-to | Goal-directed actions and decisions |
| Look up system truth | Reference | Precise, complete, consistently structured contract |
| Understand why | Explanation | Context, mechanism, alternatives, and implications |

Do not force all four forms into one document. Use information-carrying headings
and progressive detail. A summary states the conclusion; it does not announce
what the document will discuss.
