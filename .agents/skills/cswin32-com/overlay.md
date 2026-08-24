---
core: cswin32-com
core-pin: v0.16.1
---

# Touki overlay - cswin32-com

Repo-specific companion to the vendored [cswin32-com](SKILL.md) skill. The core
and its sibling pages are a pinned copy from the
[agent-skills commons](https://github.com/JeremyKuhne/agent-skills); keep Touki
bindings here rather than editing that payload.

## Concrete bindings

- **Generating project**: [touki/touki.csproj](../../../touki/touki.csproj) is
  the only project that references `Microsoft.Windows.CsWin32`.
- **Generator version**: `Microsoft.Windows.CsWin32` is pinned centrally in
  [Directory.Packages.props](../../../Directory.Packages.props) at a version
  satisfying the core's `0.3.296` minimum.
- **Target frameworks**: production targets `net10.0` and `net472`; Framework
  tests run on `net481`. Do not describe Framework-only behavior as
  "net481-only".
- **Generator configuration**: [touki/NativeMethods.json](../../../touki/NativeMethods.json)
  sets `allowMarshaling: false`, `useSafeHandles: false`, and an internal
  projection. Add metadata interfaces through
  [touki/NativeMethods.txt](../../../touki/NativeMethods.txt).
- **Support helpers**: Touki does not currently provide `ComScope<T>`,
  `IID.Get<T>()`, a COM class-factory helper, or a CCW pointer-acquisition
  helper. Names used by the core are conceptual until equivalent helpers are
  added and tested in Touki.
- **Current surface**: Touki has no struct-based COM consumer, manual COM
  struct, connection-point owner, or CCW bridge. Apply this skill when such a
  surface is introduced; do not add scaffolding merely to instantiate the
  examples.

## Touki requirements

- Follow [cswin32-interop](../cswin32-interop/SKILL.md) for generated P/Invoke
  configuration, blittable signatures, platform gating, and native units.
- Run [security-review](../security-review/SKILL.md) for every raw COM pointer,
  vtable call, ownership transfer, or marshalling boundary.
- Put exact cross-target GUID value tests in
  [touki.tests](../../../touki.tests/) for every manual `IComIID`
  implementation.
- Keep public API documentation and C# style aligned with
  [AGENTS.md](../../../AGENTS.md).

## Updating

Pull upstream changes with `gh skill update cswin32-com`, review the core diff,
and re-pin this overlay with the installed release.
