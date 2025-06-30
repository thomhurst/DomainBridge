# Expert Requirements Answers

## Q6: Should the source generator create bridge classes for ALL nested types found in the target type's object graph, even if they're several levels deep?
**Answer:** Yes. Otherwise I believe they can't pass through the app domain if they're not serializable. We're using MarshalByRef to work around this.

## Q7: When a bridge class implements an interface, should it implement ALL interface members including those from inherited interfaces and explicitly implemented members?
**Answer:** Yes. Any members, methods, interfaces etc that the original wrapped type has, the bridge wrapper should also have, and forward to the wrapped object. This makes it maximise compatibility with the original object and mean functionality shouldn't be any more limited.

## Q8: Should the generator handle naming conflicts when multiple types with the same name exist in different namespaces by using fully qualified names?
**Answer:** Yes. We don't want any conflicts that might break functionality.

## Q9: When generating bridge types for generic types with complex constraints, should the generator preserve all type constraints and variance modifiers?
**Answer:** No (default) - MarshalByRefObject constraints may conflict with original constraints, focus on functionality over exact type matching.

## Q10: Should the generator fail gracefully with descriptive diagnostic messages when it encounters types it cannot bridge (e.g., ref structs, pointers)?
**Answer:** Yes

## Key Requirements Summary:
1. Complete type graph bridging - all nested types must be bridged
2. Full interface implementation - including inherited and explicit implementations
3. Namespace conflict resolution using fully qualified names
4. Prioritize functionality over exact type constraint matching for generics
5. Clear diagnostic messages for unbridgeable types