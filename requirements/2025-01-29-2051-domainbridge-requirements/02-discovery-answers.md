# Discovery Answers

## Q1: Are you planning to add new features to the DomainBridge library?
**Answer:** No - this is about fixing existing errors and bugs

## Q2: Will these bug fixes involve changes to the source generator functionality?
**Answer:** Yes

## Q3: Do the bug fixes need to maintain backward compatibility with existing DomainBridge users?
**Answer:** No - We can make breaking changes if necessary

## Q4: Will the bug fixes involve supporting additional .NET Framework versions beyond 4.7.2?
**Answer:** No

## Q5: Are there specific performance or memory usage concerns driving these bug fixes?
**Answer:** No. The bugs I can see currently are around "The type or namespace name 'PLACEHOLDERBridge' does not exist in the namespace '...'". And also "'PLACEHOLDERBridge does not implement interface member 'INTERFACE.METHOD()'". I'm not sure why since we should be source generating and mapping all of the the members/methods to the type that they're wrapping. I wonder if it's getting confused with types with similar names maybe.

## Key Issues Identified:
1. Missing generated bridge types ("PLACEHOLDERBridge does not exist")
2. Interface implementation failures (bridge classes not implementing all interface members)
3. Possible confusion with similar type names