# IL copy opcode and pattern catalog

Reference for the [il-copy-inspection](../SKILL.md) skill. The opcodes and patterns
that correspond to a struct value copy in compiled IL, and how to tell each apart
from its non-copying address-based counterpart.

## The defensive-copy signature

A *defensive copy* is the one the compiler inserts silently when a non-`readonly`
instance member is invoked on a read-only struct location (an `in` parameter, a
`readonly` field, a `ref readonly` local). It is the highest-value find because it
is invisible in source. For `void M(in Pooled p) => p.Mutate();` where `Mutate` is
not `readonly`, the compiler emits a **synthesized temporary**:

```cil
ldarg.1            // &p   - address of the in-parameter (no copy yet)
ldobj      Pooled  // copy *p onto the stack
stloc.0            // spill the copy into a compiler temp local
ldloca.s   0       // &temp - address of the COPY
call       instance void Pooled::Mutate()   // mutate the copy, then discard it
```

The tell is the **`ldobj` -> `stloc <temp>` -> `ldloca <temp>` -> `call instance`**
chain on a receiver that started as an address (`ldarg`/`ldflda`/`ldsflda` of a
read-only location). The mutation lands on the temp and is thrown away. Contrast the
no-copy form, where a `readonly` member (or a mutable receiver) is called directly
on the address with no intervening `ldobj`/`stloc`:

```cil
ldarg.1            // &p
call       instance int32 Pooled::Peek()    // readonly member - no copy
```

## Explicit copy opcodes

| Opcode | Meaning | Copy? |
| ------ | ------- | ----- |
| `ldobj <type>` | Load a value type from an address onto the stack | **Yes** - copies the value |
| `stobj <type>` | Store a value type from the stack through an address | **Yes** |
| `cpobj <type>` | Copy a value type from one address to another | **Yes** |
| `box <type>` | Box a value type into a reference | **Yes** - copy + heap allocation |
| `unbox.any <type>` | Unbox to a value on the stack | **Yes** - copies out |
| `ldfld <field>` | Load a field **by value** | **Yes** when the field is a struct |
| `ldflda <field>` | Load a field **address** | No - address, no copy |
| `ldsfld` / `ldsflda` | Static field, by value / by address | `ldsfld` copies; `ldsflda` does not |
| `ldloc` / `ldloca` | Local, by value / by address | `ldloc` of a struct copies; `ldloca` does not |
| `ldarg` / `ldarga` | Argument, by value / by address | `ldarg` of a struct copies; `ldarga` does not |
| `ldelem <type>` / `ldelema` | Array element, by value / by address | `ldelem` copies; `ldelema` does not |

The recurring distinction is **value form vs address form**: the `...a` suffix
(`ldflda`, `ldloca`, `ldarga`, `ldelema`, `ldsflda`) takes an address and does
**not** copy. The non-`a` form of a struct loads a copy.

## By-value argument / return / assignment

- **By-value argument.** A struct pushed onto the stack (via `ldloc`/`ldfld`/etc.,
  not `ldloca`) into a call whose parameter is not `ref`/`in`/`out` is copied into
  the callee. The callee's signature (no `&` on the parameter type) confirms by-value.
- **By-value return.** `ret` with a struct value on the stack returns a copy. A
  method that returns `ref` puts an address on the stack instead - no copy.
- **Assignment / field store.** `stfld`/`stloc`/`stsfld` of a struct value, or
  `stobj`/`cpobj` through an address, copies into the destination.

## Boxing

`box <ValueType>` is the easiest copy to spot and almost always unintended for a
resource-owning struct: it copies the value onto the GC heap. It appears whenever a
value type is converted to `object`, an interface, or `dynamic`, or wrapped in
`Nullable<T>` patterns. Boxing is also the one copy whose runtime cost (an
allocation) is directly visible at the runtime layer via `[MemoryDiagnoser]`.

## Caveats when reading IL

- **Move vs copy is still inferred.** IL shows a copy opcode but not whether the
  source value had another owner. A `ldloc`/`ldobj` feeding a `ret` may be a
  deliberate transfer. Cross-reference the source line (via the PDB) before calling
  it unintended - the same limit the source analyzer documents.
- **The JIT may elide it.** `ldobj`/`stloc` of a small struct can be optimized away
  by the JIT. IL presence proves the *compiler* copied; confirm a *runtime* cost at
  the asm (`DisassemblyDiagnoser`) or trace layer.
- **Generics emit shared IL.** The body of a generic method shows one copy of `T`;
  whether `T` is a large struct, a small struct, or a reference type changes the
  real cost. Record the open generic and reason per value-type instantiation.
- **Debug vs Release.** Debug IL adds spills and temporaries that look like copies.
  Always read optimized Release IL for a representative picture.
- **Compiler-synthesized members.** Async/iterator state machines hoist captured
  structs into generated fields; the copy lives in the `MoveNext` body, not the
  user method. Disassemble the generated nested type to see it.

## Mapping IL offset to source line

Each IL instruction has an offset; the portable PDB's **sequence points** map offset
ranges to `(document, startLine, startColumn)`. To attribute a copy:

- With a disassembler: use a mode that interleaves source (`ilspycmd` with debug
  info, ILSpy's "IL with C#").
- Programmatically: open the PDB with `System.Reflection.Metadata.MetadataReader`,
  read the method's `MethodDebugInformation.GetSequencePoints()`, and find the
  sequence point whose IL range contains the copy opcode's offset. This is the same
  offset-to-line mechanism a profiler uses, applied statically.
