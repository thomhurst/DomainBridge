# Discovery Answers

## Q1: Are you seeing these errors when building your own project that uses DomainBridge?
**Answer:** Yes

## Q2: Are the interface implementation errors specifically for types marked with [AppDomainBridgeable] attribute?
**Answer:** No - when using [DomainBridge]. User clarified: "I don't want to generate interfaces. I want to generate a type that contains all of the public members and interfaces of the wrapped type, and calls to these simply get forwarded on to the wrapped type"

## Q3: Do the namespace collision errors mention duplicate definitions for types ending with "Bridge"?
**Answer:** No - for a type called "EventSource"

## Q4: Are you using both [DomainBridge] and [AppDomainBridgeable] attributes in the same project?
**Answer:** No - just [DomainBridge]

## Q5: Do the errors appear immediately after adding the DomainBridge NuGet package to your project?
**Answer:** No

## Key Insights from Answers:
1. The user is using the classic [DomainBridge(typeof(T))] pattern, not the interface-first approach
2. The namespace collision is specifically with "EventSource" - this suggests a conflict with System.Diagnostics.Tracing.EventSource
3. The user expects the generated proxy to implement all interfaces from the wrapped type and forward calls
4. This is happening in a consumer project, not in DomainBridge itself