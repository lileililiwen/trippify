using Xunit;
namespace Trippify.ArchitectureTests;
public sealed class ArchitectureTests
{
    [Fact] public void Domain_has_no_project_dependencies() => Assert.DoesNotContain(typeof(Trippify.Domain.ModuleMarker).Assembly.GetReferencedAssemblies(), x => x.Name!.StartsWith("Trippify."));
    [Fact] public void Application_does_not_reference_outer_layers() { var r = typeof(Trippify.Application.ModuleMarker).Assembly.GetReferencedAssemblies().Select(x => x.Name); Assert.DoesNotContain("Trippify.Infrastructure", r); Assert.DoesNotContain("Trippify.Api", r); }
}
