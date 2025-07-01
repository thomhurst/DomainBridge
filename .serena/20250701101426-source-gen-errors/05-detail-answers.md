# Detail Answers

## Q6: Should the generated bridge class implement explicit interface implementations when the wrapped type uses them?
**Answer:** Yes

## Q7: When your type "EventSource" causes namespace collisions, is it a custom class in your project's namespace that happens to have the same name as System.Diagnostics.Tracing.EventSource?
**Answer:** No - "it seems to be generating classes per generic type with an incorrect syntax. e.g. EventSource<string>, and then EventSource<int>"

## Q8: Should the bridge generator skip implementing interfaces that are already implemented by MarshalByRefObject (like IDisposable)?
**Answer:** "we should still forward dispose calls to the wrapped type also"

## Q9: For interface members with generic type parameters, should the bridge maintain the exact same generic constraints as the original interface?
**Answer:** Yes

## Q10: When the wrapped type implements multiple interfaces with members of the same name, should the bridge use explicit interface implementation to avoid ambiguity?
**Answer:** Yes

## Critical Finding from Q7:
The user revealed the actual issue - the generator is creating separate bridge classes for each generic type instantiation (EventSource<string>, EventSource<int>) with incorrect syntax. This is a generic type handling problem, not a simple namespace collision.