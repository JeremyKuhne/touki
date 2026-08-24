---
core: cswin32-interop
core-pin: v0.16.1
---

# Touki overlay - cswin32-interop

Repo-specific companion to the vendored [cswin32-interop](SKILL.md) skill. The
`SKILL.md` and its six sibling pages (`blittable-signatures.md`,
`types-and-constants.md`, `composition.md`, `library-layering.md`,
`ownership-and-units.md`, `gating.md`) are a **pinned copy of the portable core**
from [JeremyKuhne/agent-skills](https://github.com/JeremyKuhne/agent-skills) (see
the `metadata.github-*` provenance in `SKILL.md`). Do not hand-edit the core -
`gh skill update` would flag the drift. Everything touki-specific lives here.

> **Pinned to a release.** The core is pinned to the commons **v0.16.1** tag. Pull
> later upstream changes with `gh skill update cswin32-interop` (review the diff,
> re-pin to the new tag).

## Concrete bindings for the core's placeholders

- **Generating project**: [touki/touki.csproj](../../../touki/touki.csproj) is the
  only project that references `Microsoft.Windows.CsWin32`
  (`PrivateAssets="all"`), on **both** TFMs. Do not add the generator to
  `touki.tests`, `touki.perf`, or the analyzer projects - they reach the
  projection through `InternalsVisibleTo`.
- **Target frameworks**: `net10.0` (`NET`) and `$(DotNetFrameworkVersion)` =
  `net472` (`NETFRAMEWORK`); tests run the Framework leg as `net481`. Never
  describe a projection as "net481-only".
- **Generator version**: `Microsoft.Windows.CsWin32` is pinned centrally in
  [Directory.Packages.props](../../../Directory.Packages.props) at a version
  satisfying the paired `cswin32-com` skill's `0.3.296` minimum.
- **API list**: [touki/NativeMethods.txt](../../../touki/NativeMethods.txt) - the
  clipboard surface (`OpenClipboard` ... `GetClipboardData`), the global-memory
  allocator (`GlobalAlloc`/`GlobalLock`/`GlobalUnlock`/`GlobalSize`/`GlobalFree`),
  `GetCurrentThreadId`, `WaitForMultipleObjectsEx`, `Sleep`, and the
  `CLIPBOARD_FORMAT` / `GLOBAL_ALLOC_FLAGS` / `WIN32_ERROR` / `E_HANDLE`
  projections.
- **Generator options**:
  [touki/NativeMethods.json](../../../touki/NativeMethods.json) sets
  `public: false`, `allowMarshaling: false`, `useSafeHandles: false`,
  `className: PInvoke`, and `preserveSigMethods: ["*"]`. The generated surface is
  internal, so it never appears in `KlutzyNinja.Touki`'s public API.
- **Working example to copy**:
  [WindowsClipboardProvider.cs](../../../touki/Touki/Io/Providers/WindowsClipboardProvider.cs)
  calls the generated `Windows.Win32.PInvoke` surface identically on both TFMs;
  its tests are
  [WindowsClipboardProviderTests.cs](../../../touki.tests/Touki/Io/Providers/WindowsClipboardProviderTests.cs).
- **Non-Windows natives stay on `[LibraryImport]` / `[DllImport]`** per the core's
  rule 4: [LinuxClipboardProvider.cs](../../../touki/Touki/Io/Providers/LinuxClipboardProvider.cs)
  and [MacClipboardProvider.cs](../../../touki/Touki/Io/Providers/MacClipboardProvider.cs)
  (libc / Objective-C runtime), and
  [NativeMemory.cs](../../../touki/Framework/Polyfills/System.Runtime.InteropServices/NativeMemory.cs)
  (`ucrtbase`, Framework-only). CA1401/SYSLIB1054 wiring for those is recorded in
  [touki/GlobalSuppressions.cs](../../../touki/GlobalSuppressions.cs).
- **Coding style**: follow [AGENTS.md](../../../AGENTS.md) (no `var`, target-typed
  `new()`, C# keyword type names, `nint`/`nuint`, `is null` / `is not null`,
  indented XML docs).

## Gating in touki

Windows-only providers are selected at runtime, not excluded at compile time, so
generated declarations compile into the cross-platform assembly on both TFMs. Use
the platform guards described in [gating.md](gating.md) rather than trimming the
source item; the Unix oracle suites (see
[`run-tests-on-wsl`](../run-tests-on-wsl/SKILL.md)) build the same files.

For the scratch buffers the core's `gating.md` mentions, follow
[`scratch-buffer-strategy`](../scratch-buffer-strategy/SKILL.md) - it carries the
net481/net10 size crossovers this repo measured.

## Cross-references (the core's "Related skills")

- [`dotnet-polyfills`](../dotnet-polyfills/SKILL.md) and
  [`polyfill-dotnet-api`](../polyfill-dotnet-api/SKILL.md) - the Framework
  polyfills that consume parts of this projection.
- [`scratch-buffer-strategy`](../scratch-buffer-strategy/SKILL.md) - buffers on
  the native boundary.
- [`security-review`](../security-review/SKILL.md) - required for any change to
  the native boundary: allocator ownership, byte-versus-element lengths, and the
  `Unsafe`/`MemoryMarshal`/`Marshal` audit.
- [`il-copy-inspection`](../il-copy-inspection/SKILL.md) - confirming struct
  copies around blittable projections.

The paired [`cswin32-com`](../cswin32-com/SKILL.md) skill is vendored for future
raw COM work. Its [overlay](../cswin32-com/overlay.md) records that Touki does
not yet provide a struct-based COM surface or the support helpers used in the
core's examples.

## Updating

Pull upstream changes to the core with `gh skill update cswin32-interop` (review
the diff, re-pin). Keep touki-specific additions in this file, not in the core.
