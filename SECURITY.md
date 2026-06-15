# Security Policy

## Reporting a vulnerability

Please report security vulnerabilities privately - not in public issues.

Use GitHub's private vulnerability reporting: open the **Security** tab of this
repository and choose **Report a vulnerability**, or go directly to
<https://github.com/JeremyKuhne/touki/security/advisories/new>. This opens a
private advisory visible only to the maintainer.

Include, as far as you can:

- a description of the issue and its impact;
- the version (NuGet package or commit) affected;
- steps to reproduce, ideally with a minimal code sample or input;
- any known workaround.

Touki is a low-level library whose APIs parse and transform untrusted input -
glob and extended-glob patterns, format strings, path strings, and spans of
bytes/chars. Parsing and decoding issues triggered by crafted input - crashes,
unbounded allocation, catastrophic backtracking, or other denial-of-service
shapes - are in scope, on both the .NET 10 and .NET Framework 4.7.2 targets.

## Supported versions

Security fixes land on the `main` branch and ship in the next release. Only the
latest released version line is supported; there is no long-term back-port
stream, so upgrade to the newest `KlutzyNinja.Touki` /
`KlutzyNinja.Touki.TestSupport` release to pick up a security fix.

## Response

This is an open-source project maintained on a best-effort basis. You can expect
an initial acknowledgement; please allow a reasonable window for a fix before any
public disclosure.
