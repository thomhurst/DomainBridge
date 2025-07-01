# Discovery Questions

These questions will help understand the specific source generation errors you're encountering.

## Q1: Are you seeing these errors when building your own project that uses DomainBridge?
**Default if unknown:** Yes (errors typically appear in consumer projects, not in DomainBridge itself)

## Q2: Are the interface implementation errors specifically for types marked with [AppDomainBridgeable] attribute?
**Default if unknown:** Yes (this is the newer pattern that generates interface implementations)

## Q3: Do the namespace collision errors mention duplicate definitions for types ending with "Bridge"?
**Default if unknown:** Yes (bridge proxy classes are the primary generated output)

## Q4: Are you using both [DomainBridge] and [AppDomainBridgeable] attributes in the same project?
**Default if unknown:** No (most projects use one pattern consistently)

## Q5: Do the errors appear immediately after adding the DomainBridge NuGet package to your project?
**Default if unknown:** No (errors typically appear after marking types with attributes)