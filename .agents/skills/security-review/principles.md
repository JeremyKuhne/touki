# Core principles and severity

Detail for the [security-review](SKILL.md) skill.

## Core principles

1. **Test safe properties, even when current code is already
   correct.** A safe property = the method terminates within a stated
   bound, returns a defined error for malformed input, returns the
   documented default for empty input, doesn't read/write past the
   buffer, or doesn't crash. These tests must pass against current
   code; commit them as the regression lock.
2. **Never write a test that pins observable wrong output.** A test
   asserting "this silently truncates" locks the bug in. If current
   code is wrong, skip the test until the fix lands and pin the
   *corrected* output then.
3. **Test the shape, not the specific exploit.** "Input at the
   declared limit" lasts forever; "exact pattern that triggered CVE-X"
   decays.
4. **Boundary tests come in pairs.** One at the limit (succeeds), one
   just over (fails in a defined way).
5. **Surface findings before patching production code.** When a
   finding needs a production change, output the options report,
   **end your turn, and do not produce the patch or any failing test
   on that turn**. Resume after the user replies with an approval
   verb (`go`, `apply A`, `fix it`). Passing safe-property tests
   (principle 1) may ship on the same turn as the report; failing
   tests may not.

## Severity rubric

Label every finding before the report.

- **High** - memory corruption, OOB read/write, type confusion,
  lifetime escape, RCE, disclosure of secrets/addresses, auth bypass.
  Fix on the same PR.
- **Medium** - DoS (CPU, memory, stack, FDs), crashing unhandled
  exception, parser accepting malformed input as valid, ambiguous
  parsing that smuggles a different shape past validation. Usually
  fix on the same PR; deferral needs a documented mitigation.
- **Low** - silent truncation still bounded by a downstream BCL
  slice check, documented contract violations on edge cases,
  non-sensitive error-message leakage. Safe to defer; still pin the
  *corrected* behavior with a regression test once the fix lands.
