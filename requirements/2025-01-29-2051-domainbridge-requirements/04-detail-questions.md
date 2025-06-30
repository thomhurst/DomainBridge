# Expert Requirements Questions

Based on my analysis of the DomainBridge source generator code, here are the critical questions to understand the expected behavior for fixing the bugs:

## Q6: Should the source generator create bridge classes for ALL nested types found in the target type's object graph, even if they're several levels deep?
**Default if unknown:** Yes (TypeCollector already recursively processes all referenced types to ensure complete isolation)

## Q7: When a bridge class implements an interface, should it implement ALL interface members including those from inherited interfaces and explicitly implemented members?
**Default if unknown:** Yes (bridge classes should be drop-in replacements for the original types)

## Q8: Should the generator handle naming conflicts when multiple types with the same name exist in different namespaces by using fully qualified names?
**Default if unknown:** Yes (BridgeTypeResolver already uses fully qualified names to avoid ambiguity)

## Q9: When generating bridge types for generic types with complex constraints, should the generator preserve all type constraints and variance modifiers?
**Default if unknown:** No (MarshalByRefObject constraints may conflict with original constraints, focus on functionality over exact type matching)

## Q10: Should the generator fail gracefully with descriptive diagnostic messages when it encounters types it cannot bridge (e.g., ref structs, pointers)?
**Default if unknown:** Yes (better developer experience to know why generation failed rather than getting cryptic compilation errors)