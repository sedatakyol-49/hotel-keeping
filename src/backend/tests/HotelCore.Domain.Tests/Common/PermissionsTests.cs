using System.Reflection;
using AwesomeAssertions;
using HotelCore.Domain.Common;

namespace HotelCore.Domain.Tests.Common;

/// <summary>
/// <see cref="Permissions"/> butunluk testleri. <c>Permissions.All</c> hem seed'in hem de
/// authorization policy kaydinin TEK kaynagidir; sabit eklenip listeye yazilmazsa izin
/// sessizce "yok" sayilir. Bu test o sessiz hatayi derleme sonrasi yakalar.
/// </summary>
public sealed class PermissionsTests
{
    private static IReadOnlyList<FieldInfo> PermissionConstants { get; } =
        typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .ToArray();

    [Fact]
    public void All_contains_every_declared_permission_constant()
    {
        var declared = PermissionConstants
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        Permissions.All.Should().BeEquivalentTo(declared);
    }

    [Fact]
    public void All_has_no_duplicates()
    {
        Permissions.All.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_is_not_empty()
    {
        Permissions.All.Should().NotBeEmpty();
        PermissionConstants.Should().NotBeEmpty();
    }

    [Fact]
    public void Every_permission_key_follows_the_module_dot_action_format()
    {
        foreach (var key in Permissions.All)
        {
            key.Should().NotBeNullOrWhiteSpace();

            var parts = key.Split('.');
            parts.Should().HaveCount(2, "izin anahtari formati 'Modul.Aksiyon' olmalidir: {0}", key);
            parts.Should().OnlyContain(p => p.Length > 0);
            parts.Should().OnlyContain(p => char.IsUpper(p[0]), "her parca PascalCase olmalidir: {0}", key);
        }
    }

    [Fact]
    public void Permission_keys_contain_no_surrounding_whitespace()
    {
        Permissions.All.Should().OnlyContain(key => key == key.Trim());
    }

    [Fact]
    public void Constant_names_match_their_permission_key_without_the_dot()
    {
        // Ornek: InvoicesApprove -> "Invoices.Approve". Isim/deger kaymasini engeller.
        foreach (var field in PermissionConstants)
        {
            var value = (string)field.GetRawConstantValue()!;
            value.Replace(".", string.Empty, StringComparison.Ordinal)
                .Should().Be(field.Name, "sabit adi ile izin anahtari ayni olmalidir: {0}", field.Name);
        }
    }

    [Fact]
    public void All_is_exposed_as_a_read_only_snapshot()
    {
        // Policy kaydi/seed calisirken listenin degistirilememesi gerekir.
        Permissions.All.Should().BeAssignableTo<IReadOnlyList<string>>();
        typeof(Permissions).GetProperty(nameof(Permissions.All))!.SetMethod.Should().BeNull();
    }
}
