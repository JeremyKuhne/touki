# KlutzyNinja.Touki.TestSupport

Shared test helpers for projects that build against
[`KlutzyNinja.Touki`](https://www.nuget.org/packages/KlutzyNinja.Touki/) on
both modern .NET and .NET Framework 4.7.2.

Includes utilities such as `TestAccessor` (typed access to private members),
`NoAssertContext`, `ThrowingTraceListener`, and a handful of object/type
extensions used by the touki test suite.

## Targets

- `net10.0`
- `net472`

Like `KlutzyNinja.Touki`, this package ships architecture-neutral
("AnyCPU") assemblies with no native components, so x64 and ARM64 are both
fully supported on Windows, Linux, and macOS.

## Notes

This package is **not AOT-friendly** - it uses reflection to reach private
members. It is intended for use from test projects only. Do not take a
dependency on it from production code.

## License

MIT - see [LICENSE](https://github.com/JeremyKuhne/touki/blob/main/LICENSE).
