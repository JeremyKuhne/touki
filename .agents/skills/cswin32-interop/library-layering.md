# Support-library layering

Detail for [cswin32-interop](SKILL.md). A public CsWin32 composition surface works
best as one layer in a directed package stack, not as the place where every
shared helper accumulates.

## The four layers

| Layer | Owns | Must not own |
| --- | --- | --- |
| Managed foundation | Cross-domain buffers, pooling, collections, enum helpers, compiler/runtime polyfills, and other utilities that remain useful without a public Win32 API. | The canonical public `Windows.Win32.PInvoke` or public Win32 type identity. |
| Win32 composition owner | The public `PInvoke`, shared generated Win32 types, Windows-specific lifetime/ownership helpers, common COM wrappers, and owner-only generated hooks. | Product- or domain-specific UI and workflow behavior. |
| Domain extender | Additional generated APIs projected through `extensionReceiver`, domain wrappers, and extension members on owner-provided types. | Duplicate owner types, cross-assembly partial declarations, or a competing public `PInvoke`. |
| Consumer | Application behavior that calls the composed surface. | Another generated projection unless it is intentionally a separate namespace/ABI domain. |

The conceptual layer order is foundation -> owner -> extender -> consumer;
package references point back down that stack. An extender may also reference
the foundation directly when its source uses that surface. Declare that
dependency explicitly rather than relying on the owner's transitive dependency.

A foundation package may use platform interop internally. The boundary is its
**public ownership contract**: an internal generated `PInvoke` does not compete
with the composition owner, while a public `Windows.Win32.PInvoke` does.

## Decide where a helper belongs

Move a helper toward the lowest reusable layer that can own it without importing
higher-level policy:

- A pooled scratch buffer, allocation-free flag operation, or .NET Framework
   BCL polyfill belongs in the managed foundation.
- A `HANDLE` close helper, `HRESULT` mapping, `PWSTR` ownership operation,
   owned COM-pointer scope, `SAFEARRAY`/`VARIANT` support, or owner-side CCW hook
   belongs in the Win32 owner when multiple extenders can use it.
- A windowing framework, accessibility model, dialog abstraction, or
  domain-specific COM adapter belongs in the extender.
- An implementation-only vtable provider needed by one extender stays there
  under a unique name; it does not justify a duplicate imported interface.

Before promoting code, remove dependencies on the higher layer and ask whether
its public namespace and exception/ownership contract make sense for every
consumer of the lower layer. "Used by two projects" is not enough if both uses
carry the same product policy.

## API identity and compatibility

Moving a public type from an extender assembly into the owner is not merely a
source relocation. Even with the same namespace and type name, its assembly
identity changes. Treat the move as a binary and often source breaking change
unless the old assembly provides type forwarding and compatible facade members.
Also call out removed static facades, renamed nested types, and changed extension
lookup in release notes.

Do not leave both definitions public during migration. Two assemblies exporting
the same fully qualified type create ambiguity instead of compatibility.

## Release order and package metadata

Publish from the bottom up:

1. publish the managed foundation when its required surface changed;
2. publish the Win32 owner against that foundation;
3. restore the owner from the real feed into the extender, then publish the
   extender;
4. validate an application that references only the top-level package.

Keep the owner and extender on compatible CsWin32 versions and language levels.
If the owner is prerelease, the extender package must also be prerelease; NuGet
warns when a stable package depends on a prerelease package. Inspect the packed
extender nuspec to confirm the owner and foundation dependency versions before
publishing.

## Migration sequence

For an existing library that duplicates owner candidates:

1. inventory generated types, hand-authored partials, helpers, and static
   facades; classify each into the four layers;
2. add the shared public surface and generated hooks to the owner, with direct
   tests, and publish it first;
3. restore that published owner into an empty package root using normal feeds;
4. configure `extensionReceiver`, remove duplicate types, and convert downstream
   augmentation to extension blocks or uniquely named provider types;
5. audit every raw pointer and native buffer while changing call shapes; moving
   ownership does not preserve caller obligations automatically;
6. pack the extender and build a clean-cache consumer that references only the
   extender package.

For the final restore, use `--no-cache` and a fresh `--packages` directory. Check
`project.assets.json`, the package's `.nupkg.metadata` source, and the nuspec so a
locally cached rehearsal package cannot masquerade as the published dependency.
