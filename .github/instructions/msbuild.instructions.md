---
applyTo: '**/*.*proj, **/*.*proj.user, **/*.targets, **/*.props'
---

# MSBuild Instructions for AI Systems

## Core Principles

- **Create valid XML. Invalid XML is never acceptable.**
- Always give only the final, simplest solution

### Understand MSBuild's Declarative Nature
- MSBuild is primarily declarative, not imperative. Prefer built-in attributes and metadata operations over complex conditional logic
- Many operations that seem to require loops or complex logic can be accomplished with simple item transformations, batching, or metadata operations
- Before writing custom tasks or complex targets, check if MSBuild already provides the functionality through attributes or metadata

### Think in Terms of Set Operations
- MSBuild item operations are essentially set operations
- Don't think in terms of loops or iterations
- Your job is to specify the sets and criteria, not implement the algorithm
- In MSBuild, the direction of set operations matters significantly
- The engine handles the iteration and comparison internally
- Think: "Add to / remove from set A all items that match items in set B based on criteria C"
- The sets involved can be from the same or different item groups
- With `Remove` operations, the syntax is `<ItemToModify Remove="@(ItemsToRemove)" .../>` where:
  - `ItemToModify` is the collection being modified
  - `@(ItemsToRemove)` identifies potential matches to remove from the collection
  - Matching is by the item's include or `MatchOnMetadata` if specified
- This is conceptually different from filtering where you would keep items that don't match
- Remember: `<A Remove="@(B)" .../>` means "from set A, remove items that match items in set B" (not "remove B from A")

### Context Awareness: Targets vs. Global Scope
- **Inside targets**: Dynamic evaluation occurs; you can use tasks, conditions are evaluated at execution time, and items/properties can be modified
- **Outside targets** (global scope): Static evaluation occurs; items and properties are evaluated during the evaluation phase before any targets run
- Item modifications using `Update`, `Remove`, and metadata changes can occur both inside and outside targets, but timing differs
- Property functions and item functions work in both contexts but are evaluated at different phases

## Item Operations Best Practices

### Leverage Built-in Item Attributes
Instead of creating complex logic, use MSBuild's built-in item attributes:
- `Include`: Add items to an ItemGroup
- `Exclude`: Exclude specific items from an Include pattern
- `Remove`: Remove items from an existing ItemGroup (use with MatchOnMetadata)
- `Update`: Modify metadata of existing items
- `KeepMetadata`: Retain only specified metadata
- `RemoveMetadata`: Remove specified metadata
- `KeepDuplicates`: Control duplicate handling
- `MatchOnMetadata`: Specify which metadata to use for matching during Remove operations

### Item and Property Metadata
- Access item metadata using `%(ItemType.MetadataName)` syntax
- Well-known metadata (Filename, Extension, FullPath, etc.) is automatically available on all file-based items
- Use `@(ItemType->'%(Metadata)')` for transformations when you need the transformed result, not for comparisons
- Batching automatically occurs when using `%(MetadataName)` in conditions or task parameters
- Items carry all their metadata with them - don't lose it through unnecessary transformations

### Avoid Unnecessary Intermediate ItemGroups
- If items already exist with the metadata you need, use them directly
- Don't create intermediate transformed items unless the transformation is actually needed for the final result
- Transformations like `@(Item->'%(Metadata)')` create strings, losing all other metadata
- When you need to match items based on metadata, use the original items, not transformed versions

### Prefer Simple Solutions
- If a solution seems overly complex, you're likely missing a built-in MSBuild feature
- Single-line item operations are often possible for what seems like complex requirements
- Avoid writing custom inline tasks unless absolutely necessary
- Use property functions `$([MSBuild]::FunctionName())` for simple operations instead of tasks

## Common Patterns and Anti-Patterns

### Correct Patterns
✅ **DO:**
- **Create valid XML. Invalid XML is never acceptable.**
- Ensure each XML element has unique attributes; do not repeat the same attribute in a single tag
- Use `Condition` attributes on individual items, properties, or targets
- Use item batching with `%(Metadata)` for iteration
- Use `Remove` with `MatchOnMetadata` directly on original items
- Leverage `@(ItemType->...)` transformations only when you need the transformed output
- Use `BeforeTargets` and `AfterTargets` on `<Target>`s when ordering is required
- Utilize well-known metadata without re-computing paths
- Match on multiple metadata values when needed for uniqueness (e.g., `MatchOnMetadata="Filename;Extension"`)

### Anti-Patterns to Avoid
❌ **DON'T:**
- Create invalid XML (e.g. duplicate attributes, repeated attribute names in a single element)
- Create foreach loops using targets when batching would suffice
- Transform items to strings when you need to compare metadata
- Create intermediate item groups just for comparison purposes when direct metadata matching would suffice
- Building complex string representations when metadata-based matching is available
- Using unnecessary item transformations that lose metadata when original items can be used directly
- Manually parse paths when well-known metadata exists
- Write complex string manipulation when property functions are available
- Assume you need a custom task for item filtering or transformation
- Mix evaluation-phase and execution-phase operations incorrectly
- Use only "Filename" in MatchOnMetadata when "Extension" is also needed for uniqueness

## Advanced Features

### Item Functions
MSBuild provides built-in item functions that should be used instead of custom logic:
- `@(ItemType->Count())`: Get count of items
- `@(ItemType->Distinct())`: Remove duplicates
- `@(ItemType->DistinctWithCase())`: Case-sensitive distinct
- `@(ItemType->Reverse())`: Reverse item order
- `@(ItemType->AnyHaveMetadataValue())`: Check metadata existence
- `@(ItemType->ClearMetadata())`: Remove all metadata
- `@(ItemType->HasMetadata())`: Filter by metadata existence
- `@(ItemType->WithMetadataValue())`: Filter by metadata value
- `@(ItemType->Metadata())`: Extract specific metadata

### Property Functions
Use property functions for string and path operations:
- `$([System.String]::...)`: String manipulation
- `$([System.IO.Path]::...)`: Path operations
- `$([MSBuild]::...)`: MSBuild-specific functions
- `$([System.DateTime]::...)`: Date/time operations

### Conditional Execution
- All conditions reduce to case-insensitve string comparisons
- Understand condition evaluation order and short-circuiting
- Use `Exists()` for file/directory existence checks
- Combine conditions with `And`, `Or`, not `&&`, `||`
- Remember that `''` (empty string) evaluates to false in conditions

## Debugging and Validation

### Help Users Debug
When providing MSBuild solutions:
- Suggest binary logs (`-bl`) for comprehensive debugging
- Mention `<Message>` tasks to output intermediate values
- Mention `-preprocess` or `-pp` to see the evaluated project file

### Debugging Item Operations
When an item operation isn't working as expected, check:
1. Are you comparing items with metadata or transformed strings?
2. Are all the metadata values you're matching on present in both ItemGroups?
3. Would outputting the items with `<Message Text="@(ItemName->'%(Identity): %(MetadataName)')" />` help visualize what's being compared?
4. Are you matching on all necessary metadata for uniqueness (e.g., both Filename AND Extension)?

### Cross-Version Compatibility
- Note which features require specific MSBuild versions
- Default to widely-supported syntax unless newer features are specifically needed
- Mention when using features from MSBuild 15.0+ (VS 2017) or newer

## Response Structure Guidelines

### When Answering MSBuild Questions
1. First, identify if the operation should happen in a target or global scope
2. Check if built-in attributes or functions can accomplish the goal
3. Look for direct operations before creating intermediate items
4. Prefer declarative solutions over imperative ones
5. Provide the simplest working solution first
6. Only add complexity if specifically required
7. Explain why the solution works, referencing the MSBuild evaluation/execution model when relevant

### Code Examples
**Never create invalid XML. Every example must be fully valid and parseable.**
- Always show complete, *minimal* examples that can be directly used
- Include appropriate XML structure (`<Project>`, `<PropertyGroup>`, `<ItemGroup>`, `<Target>`)
- Add comments explaining non-obvious behavior
- Test syntax validity (proper attribute names, closing tags, etc.)
- Demonstrate the most direct approach without unnecessary intermediate steps

### Item Metadata Preservation Reminder
- Remember that `%(Filename)` and `%(Extension)` are separate metadata, presume that the user intends both when they specify just file name
- If you need both for uniqueness (like matching complete file names), specify both
- Well-known metadata exists on all file-based items - use it directly rather than recreating it