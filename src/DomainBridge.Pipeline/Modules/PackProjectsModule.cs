using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace DomainBridge.Pipeline.Modules;

[DependsOn<PackageFilesRemovalModule>]
[DependsOn<NugetVersionGeneratorModule>]
[DependsOn<RunUnitTestsModule>]
public class PackProjectsModule : Module<List<CommandResult>>
{
    protected override async Task<List<CommandResult>?> ExecuteAsync(IPipelineContext context, CancellationToken cancellationToken)
    {
        var results = new List<CommandResult>();
        
        var packageVersion = await GetModule<NugetVersionGeneratorModule>();
        
        var projectFiles = GetProjectFiles();
        
        foreach (var projectFile in projectFiles)
        {
            results.Add(await context.DotNet().Pack(new DotNetPackOptions { 
                ProjectSolution = projectFile.FullName, 
                Configuration = Configuration.Release, 
                Properties =
                [
                    ("PackageVersion", packageVersion.Value)!, 
                    ("Version", packageVersion.Value)!
                ],
                IncludeSource = true
            }, cancellationToken));
        }

        return results;
    }

    private IEnumerable<FileInfo> GetProjectFiles()
    {
        yield return Sourcy.DotNet.Projects.DomainBridge_Core;
        yield return Sourcy.DotNet.Projects.DomainBridge_Attributes;
    }
}