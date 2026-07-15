# Draft: [API Proposal] Add ClassRecord.TryGetRawValue

Target repository: `dotnet/runtime`

The text below follows the current `API Suggestion` issue template. The template
adds the `api-suggestion` label automatically.

## Background and motivation

`System.Formats.Nrbf.ClassRecord` exposes `HasMember(string)` for checking whether
a serialized member is present and `GetRawValue(string)` for reading its value.
Consumers that handle payloads written from different versions of a type need both
operations because a serialized member can be absent:

```csharp
if (classRecord.HasMember(memberName))
{
    object? value = classRecord.GetRawValue(memberName);
    // Process value.
}
```

This recommended version-tolerant pattern performs two lookups in the same internal
member-name dictionary. The duplicate lookup becomes measurable when materializing
objects with many fields or graphs containing many instances of the same class.

A `TryGetRawValue` API would express the operation directly, distinguish an absent
member from a present member whose value is `null`, and permit a single dictionary
lookup. It would also avoid exception-driven lookup through `GetRawValue`, which is
inappropriate for expected version differences.

A 32-member benchmark over a decoded `ClassRecord` produced these fixed `MediumRun`
results (`IterationCount=15`, `LaunchCount=2`, `WarmupCount=10`):

| Runtime | Two-lookup mean | Error | One-lookup lower bound | Error | Ratio | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| .NET 10 x64 RyuJIT | 330.9 ns | 4.35 ns | 187.5 ns | 2.25 ns | 0.57 | 0 B |
| .NET Framework 4.8.1 x64 RyuJIT | 1.030 us | 0.018 us | 0.682 us | 0.010 us | 0.66 | 0 B |

The known-present row is a lower bound rather than a safe replacement: calling
`GetRawValue` directly throws when a version-tolerant payload omits a field. It shows
the upper-bound opportunity from removing the duplicate probe: 43% of the measured
member-access time on .NET 10 x64 RyuJIT and 34% on .NET Framework 4.8.1 x64 RyuJIT
for this shape, without adding allocation. These percentages do not measure the proposed
API itself. A prototype `TryGetRawValue` benchmark is required to quantify the realized
savings, including its Boolean/out-parameter overhead.

The same investigation found that changing the assignment primitive is not a suitable
alternative. `FormatterServices.PopulateObjectMembers` requires a per-object values
array; including that array made a 32-field operation 11% slower on .NET 10 x64 RyuJIT
and raised allocation from 144 B to 424 B. On .NET Framework 4.8.1 x64 RyuJIT it did
not establish a meaningful throughput improvement and raised allocation from 144 B to
425 B.

## API Proposal

```csharp
namespace System.Formats.Nrbf;

public abstract class ClassRecord : SerializationRecord
{
    public bool TryGetRawValue(string memberName, out object? value);
}
```

Proposed semantics:

- Returns `true` when `memberName` was present in the serialized payload.
- Returns `false` when `memberName` was absent and sets `value` to `null`.
- A return value of `true` does not imply that `value` is non-null. A serialized
  member that is present with a null value returns `true` and sets `value` to `null`.
- When the member is present, `value` has exactly the same value and representation
  that `GetRawValue(memberName)` would return. This includes primitive values,
  strings, `ClassRecord` instances, and array records.
- Throws `ArgumentNullException` with `ParamName == "memberName"` when `memberName`
    is null. This deliberately reports the public parameter name rather than exposing the
    internal dictionary's `"key"` parameter name.

No nullability attribute such as `NotNullWhen(true)` should be applied to `value`,
because a present serialized member can legitimately contain null.

A possible implementation sketch, not part of the proposed public API, is:

```csharp
public object? GetRawValue(string memberName)
    => GetRawValue(ClassInfo.MemberNames[memberName]);

public bool TryGetRawValue(string memberName, out object? value)
{
    ArgumentNullException.ThrowIfNull(memberName);

    if (!ClassInfo.MemberNames.TryGetValue(memberName, out int index))
    {
        value = null;
        return false;
    }

    value = GetRawValue(index);
    return true;
}

private object? GetRawValue(int index)
{
    object? value = MemberValues[index];
    return value is SerializationRecord record ? record.GetValue() : value;
}
```

Factoring the existing conversion through an index-based helper keeps
`GetRawValue` and `TryGetRawValue` behavior aligned while ensuring that the new API
performs one member-name dictionary lookup.

## API Usage

Version-tolerant member inspection becomes one operation:

```csharp
if (classRecord.TryGetRawValue("OptionalMember", out object? value))
{
    // The member was present. value can still be null.
    Process(value);
}
```

A deserializer can skip fields that were not present in an older payload without
probing the member-name dictionary twice:

```csharp
foreach (FieldInfo field in FormatterServices.GetSerializableMembers(type))
{
    if (!classRecord.TryGetRawValue(field.Name, out object? rawValue))
    {
        continue;
    }

    object? value = MaterializeValue(rawValue);
    field.SetValue(instance, value);
}
```

The Boolean result disambiguates an absent member from a present null member:

```csharp
bool present = classRecord.TryGetRawValue("MiddleName", out object? middleName);

// present == false: the payload did not contain MiddleName.
// present == true && middleName is null: the payload contained a null MiddleName.
```

## Alternative Designs

### Change `GetRawValue` to return null for a missing member

This would be a breaking behavioral change and could not distinguish a missing member
from a member that is present with a null value.

### Catch `KeyNotFoundException` from `GetRawValue`

Missing members are expected during version-tolerant deserialization. Using exceptions
for this path adds exception and allocation pressure and makes malformed or adversarial
payload shapes more expensive.

### Enumerate `MemberNames` first

A consumer could build a local set or dictionary from `MemberNames`, but this adds
per-record or per-layout allocation. Linear searches avoid allocation but turn field
matching into quadratic work for wide classes. Neither option can reuse the internal
name-to-index lookup already owned by `ClassRecord`.

### Add `GetRawValueOrDefault`

A default-returning API would retain the missing-versus-present-null ambiguity and would
not communicate member presence to version-tolerant callers.

### Add `TryGetMember<T>` or Try variants for every typed getter

A generic API introduces questions about type mismatches, nullable value types, and
conversion behavior. Adding Try variants for all typed getters would create a large API
family. `TryGetRawValue` is the minimal counterpart to the existing general-purpose
`GetRawValue` API and addresses serializers and inspection tools that already perform
the `HasMember` plus `GetRawValue` sequence.

### Expose a member index or the internal member dictionary

An index API would expose record-layout details and require additional contracts around
index stability and bounds. Exposing the dictionary would leak mutable or implementation-
specific state. The proposed API keeps the current abstraction boundary.

## Risks

This is an additive API with low compatibility risk. It exposes no value that cannot
already be obtained through `HasMember` and `GetRawValue`.

The main usability risk is interpreting `true` as implying a non-null value. The XML
documentation and examples should explicitly state that `true` means the member was
present, not that its value was non-null.

The implementation should preserve `GetRawValue` conversion semantics and use one
`TryGetValue` operation. Implementing it as `HasMember` followed by `GetRawValue` would
provide the API convenience but miss the performance and algorithmic motivation.

No new caching is required. The proposal does not retain additional type, record, or
member-name state and does not change parsing or malformed-payload validation.

## Suggested API tests

- A missing member returns `false` and writes `null` to `value`.
- A present member whose serialized value is null returns `true` and writes `null`.
- Primitive, string, class-record, and array-record members return the same value as
  `GetRawValue`.
- A member-reference record has the same resolved-value behavior as `GetRawValue`.
- A null `memberName` throws `ArgumentNullException` with
    `ParamName == "memberName"`.
- Calling the method does not mutate the record or member enumeration.
