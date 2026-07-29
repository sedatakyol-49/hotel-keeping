using System.Reflection;
using AwesomeAssertions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Tests.Common;

/// <summary>
/// <see cref="IAppDbContext"/> persistence portunun sozlesme testleri.
/// Application katmani henuz iskelet oldugundan (use-case handler'lari yazilmadi) burada
/// dogrulanan sey davranis degil, handler'larin uzerine yazilacagi PORT SEKLIDIR:
/// yalnizca Domain entity'leri acilir, set'ler salt-okunur ve kayit tek bir async
/// giris noktasindan (iptal token'i ile) yapilir.
/// </summary>
public sealed class AppDbContextPortContractTests
{
    private static IReadOnlyList<PropertyInfo> PortProperties { get; } =
        typeof(IAppDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    [Fact]
    public void Port_exposes_only_DbSet_properties()
    {
        PortProperties.Should().NotBeEmpty();
        PortProperties.Should().OnlyContain(p =>
            p.PropertyType.IsGenericType &&
            p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));
    }

    [Fact]
    public void Every_exposed_set_maps_to_a_domain_entity()
    {
        var domainEntityNamespace = typeof(Invoice).Namespace;

        foreach (var property in PortProperties)
        {
            var entityType = property.PropertyType.GetGenericArguments()[0];
            entityType.Namespace.Should().Be(
                domainEntityNamespace,
                "port yalnizca Domain entity'lerini acmalidir: {0}",
                property.Name);
        }
    }

    [Fact]
    public void No_entity_is_exposed_twice()
    {
        var entityTypes = PortProperties
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToArray();

        entityTypes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Sets_are_read_only_so_handlers_cannot_replace_them()
    {
        PortProperties.Should().OnlyContain(p => p.SetMethod == null);
    }

    [Fact]
    public void Persisting_is_async_only_and_accepts_a_cancellation_token()
    {
        var methods = typeof(IAppDbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .ToArray();

        // Senkron SaveChanges bilerek acilmaz — istek boyunca I/O bloklanmamalidir.
        methods.Should().NotContain(m => m.Name == "SaveChanges");

        var saveChangesAsync = methods.Should().ContainSingle(m => m.Name == nameof(IAppDbContext.SaveChangesAsync)).Subject;
        saveChangesAsync.ReturnType.Should().Be<Task<int>>();
        saveChangesAsync.GetParameters().Should().ContainSingle()
            .Which.ParameterType.Should().Be<CancellationToken>();
    }

    [Fact]
    public void Tenant_scoped_aggregates_that_handlers_need_are_exposed()
    {
        var exposed = PortProperties
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToHashSet();

        // Cekirdek is akislarinin (rezervasyon, faturalama, personel, RBAC) dayandigi kokler.
        Type[] required =
        [
            typeof(Hotel),
            typeof(Employee),
            typeof(Reservation),
            typeof(Room),
            typeof(Invoice),
            typeof(InvoiceLineItem),
            typeof(Payment),
            typeof(InvoiceAuditEntry),
            typeof(HotelInvoiceCounter),
            typeof(User),
            typeof(Role),
            typeof(Permission)
        ];

        exposed.Should().Contain(required);
    }
}
