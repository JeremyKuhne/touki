---
compatibility: Requires the repository's test runner; timing and allocation checks should run on every supported target framework.
description: Security-focused review of pending changes. Audit any change that could affect safety or correctness under abusive input or unchecked preconditions - oversized values, malformed structures, integer/length overflow, catastrophic backtracking, allocation pressure, other denial-of-service shapes, and any use of `unsafe` code or the `Unsafe` / `MemoryMarshal` / `Marshal` static helpers (which trade compiler safety guarantees for speed and need extra scrutiny). Add regression tests that pin safe behavior even when the current implementation already handles the input correctly. Use when asked to "assess for security vulnerabilities", "do a security review", "check for ReDoS / DoS", "audit untrusted input handling", or before publishing any change that adds or modifies code that parses, decodes, encodes, compiles, marshals, or reinterprets memory.
license: MIT
metadata:
    applicability: universal
    binding: optional-overlay
    github-path: skills/security-review
    github-pinned: v0.10.0
    github-ref: refs/tags/v0.10.0
    github-repo: https://github.com/JeremyKuhne/agent-skills
    github-tree-sha: b5f145ccbc662182f38554418b5c8b7057c16baa
    maturity: canary
    portability: portable
    related: pre-pr-self-review, performance-testing, fuzz-testing
    requires: none
    risk: local-write
name: security-review
---
# Security review

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

Surface and pin behavior under **malformed input** and **caller-validated
APIs** (everything the C# compiler doesn't check for you).

## When to run

- The user asks for a security assessment / vulnerability review.
- A change adds or modifies a member that accepts caller-supplied data
  (function args, file contents, network bytes, env vars, anything
  upstream of those).
- A change introduces or modifies `unsafe` code, calls into
  `Unsafe.*` / `MemoryMarshal.*` / `Marshal.*`, `fixed`, raw pointers,
  unbounded `stackalloc`, or any BCL API whose XML doc says "unsafe"
  or "caller must". Applies even when inputs are fully internal -
  preconditions drift across refactors.
- A CVE in a comparable library prompts "do we have the same shape?".

## The discipline in one paragraph

Write tests that pin the **safe property** (terminates within a bound,
returns a defined error for malformed input, never reads/writes past the
buffer) - and make them pass against *current* code as a regression lock.
Never pin observable wrong output; that locks the bug in. Test the input
*shape*, not a specific exploit pattern. Boundary tests come in pairs (at
the limit, just over). When a finding needs a production change, surface
the options report and **end your turn** before patching. The full
principles and the High/Medium/Low rubric are in [principles.md](principles.md).

## Review procedure

1. **Inventory.** `git status --short`; tag each new/modified member that
   takes external input or uses a caller-validated API.
2. **Walk each tagged member** through the [checklist.md](checklist.md)
   categories. The largest category - `unsafe` / `Unsafe.*` /
   `MemoryMarshal.*` and other caller-validated APIs - has its own
   per-API table in [unsafe-apis.md](unsafe-apis.md).
3. **Place tests in `<TypeName>.Security.cs`** beside the production type.
4. **Add safe-property tests now; report findings that need a production
   change before patching.** See [reporting.md](reporting.md) for the
   options-report format and the don'ts.
5. **Run tests on every target framework** - timing bounds and allocation
   behavior differ across BCL versions.

When attention is limited, allocate it by tier (detail in
[checklist.md](checklist.md)):

- **Critical (never skip):** length/size bounds, algorithmic complexity,
  and the caller-validated-API audit.
- **Standard (every member):** integer overflow, malformed structure,
  argument validation.
- **Conditional (when the shape matches):** allocation DoS, in-band
  sentinels, path traversal.

## Related skills

Run alongside a broader pre-PR self-review before any publish. When you need
to *measure* a worst-case input rather than just bound it with a `Stopwatch`,
use the repository's performance-testing skill. (A consuming repository wires
the concrete cross-references in its overlay.)

## Sub-pages

- [principles.md](principles.md) - the five core principles and the
  High / Medium / Low severity rubric.
- [checklist.md](checklist.md) - the nine audit categories with the test
  shape each one demands.
- [unsafe-apis.md](unsafe-apis.md) - the per-API precondition / failure /
  test-shape table for `unsafe`, `Unsafe.*`, `MemoryMarshal.*`, `fixed`,
  `stackalloc`, and `[DllImport]`.
- [reporting.md](reporting.md) - the review procedure, the options-report
  format, and the don'ts that keep tests from pinning bugs.
