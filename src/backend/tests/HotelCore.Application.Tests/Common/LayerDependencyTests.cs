using System.Reflection;
using AwesomeAssertions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Common;

namespace HotelCore.Application.Tests.Common;

/// <summary>
/// Clean Architecture "Dependency Rule" regresyon testi (architecture.md §2).
/// Derlenmis assembly'lerin referans grafigi denetlenir; bir gelistirici yanlislikla
/// Application'a Npgsql/Infrastructure referansi eklerse test kirmizi olur.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Permissions).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IAppDbContext).Assembly;

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

    [Fact]
    public void Domain_depends_on_no_other_project_or_third_party_package()
    {
        var references = ReferencedAssemblyNames(DomainAssembly);

        references.Should().NotContain(name => name.StartsWith("HotelCore.", StringComparison.Ordinal));
        references.Should().NotContain(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        references.Should().NotContain(name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        references.Should().NotContain(name => name.StartsWith("FluentValidation", StringComparison.Ordinal));
        references.Should().NotContain(name => name.StartsWith("Mapster", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_depends_on_Domain_only_among_HotelCore_projects()
    {
        var hotelCoreReferences = ReferencedAssemblyNames(ApplicationAssembly)
            .Where(name => name.StartsWith("HotelCore.", StringComparison.Ordinal))
            .ToArray();

        hotelCoreReferences.Should().BeEquivalentTo(new[] { DomainAssembly.GetName().Name });
    }

    [Fact]
    public void Application_does_not_reference_a_database_provider()
    {
        // EF Core cekirdegi porta (DbSet<T>) izin verilir; SAGLAYICI (Npgsql) verilmez.
        var references = ReferencedAssemblyNames(ApplicationAssembly);

        references.Should().NotContain(name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        references.Should().NotContain(name => name.Contains("AspNetCore", StringComparison.Ordinal));
    }
}
