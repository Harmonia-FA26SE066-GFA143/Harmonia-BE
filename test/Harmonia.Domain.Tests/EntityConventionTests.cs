using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Harmonia.Domain.Common;

namespace Harmonia.Domain.Tests;

/// <summary>Rules from 02-naming.md / 05-file-placement.md, checked over every entity including future ones.</summary>
public class EntityConventionTests
{
    private static readonly Type[] Entities = typeof(BaseEntity).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace == "Harmonia.Domain.Entities")
        .ToArray();

    [Fact]
    public void Entities_Exist()
    {
        Assert.NotEmpty(Entities);
    }

    [Fact]
    public void Entities_InheritBaseEntity()
    {
        var offenders = Entities.Where(t => !t.IsSubclassOf(typeof(BaseEntity))).Select(t => t.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Entities_HaveNoDataAnnotations()
    {
        var offenders = Entities
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.GetCustomAttributes().Any(a =>
                a is ValidationAttribute
                || a.GetType().Namespace?.StartsWith("System.ComponentModel.DataAnnotations") == true))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void BooleanProperties_StartWithIsHasOrCan()
    {
        var offenders = Entities
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.PropertyType == typeof(bool) || p.PropertyType == typeof(bool?))
                .Select(p => $"{t.Name}.{p.Name}"))
            .Where(name =>
            {
                var property = name[(name.IndexOf('.') + 1)..];
                return !property.StartsWith("Is") && !property.StartsWith("Has") && !property.StartsWith("Can");
            });

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoChoirEntityOrChoirId()
    {
        // The system serves exactly one choir.
        Assert.DoesNotContain(Entities, t => t.Name == "Choir");
        Assert.DoesNotContain(Entities.SelectMany(t => t.GetProperties()), p => p.Name == "ChoirId");
    }

    [Fact]
    public void Enums_HaveNoEnumSuffix()
    {
        var offenders = typeof(BaseEntity).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Name.EndsWith("Enum"))
            .Select(t => t.Name);

        Assert.Empty(offenders);
    }
}
