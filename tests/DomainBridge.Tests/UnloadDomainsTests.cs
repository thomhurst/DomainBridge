namespace DomainBridge.Tests;

public class UnloadDomainsTests
{
    [After(Assembly)]
    public static void Unload()
    {
        DerivedServiceBridge.UnloadDomain();
        ConcreteServiceBridge.UnloadDomain();
        GrandChildServiceBridge.UnloadDomain();
        ServiceWithInterfacesBridge.UnloadDomain();
        NestedDataServiceBridge.UnloadDomain();
        StaticFieldTestBridge.UnloadDomain();
        StringServiceBridge.UnloadDomain();
        SecurityTestServiceBridge.UnloadDomain();
        PerformanceTestServiceBridge.UnloadDomain();
    }
}