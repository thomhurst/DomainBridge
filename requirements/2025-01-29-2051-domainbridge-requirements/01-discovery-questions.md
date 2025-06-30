# Discovery Questions

These questions help understand the problem space and user needs for the DomainBridge requirements gathering process.

## Q1: Are you planning to add new features to the DomainBridge library?
**Default if unknown:** Yes (requirements gathering typically focuses on new functionality)

## Q2: Will these requirements involve changes to the source generator functionality?
**Default if unknown:** Yes (source generators are the core feature of DomainBridge)

## Q3: Do the requirements need to maintain backward compatibility with existing DomainBridge users?
**Default if unknown:** Yes (breaking changes should be avoided in libraries)

## Q4: Will the requirements involve supporting additional .NET Framework versions beyond 4.7.2?
**Default if unknown:** No (AppDomains are specific to .NET Framework, not available in .NET Core/.NET 5+)

## Q5: Are there specific performance or memory usage concerns driving these requirements?
**Default if unknown:** No (focus on functionality first, optimize if needed)