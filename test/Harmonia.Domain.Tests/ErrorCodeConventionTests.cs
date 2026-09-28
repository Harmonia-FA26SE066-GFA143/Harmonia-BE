using System.Reflection;
using System.Text.RegularExpressions;
using Harmonia.Domain.Common;
using Harmonia.Domain.Exceptions;

namespace Harmonia.Domain.Tests;

/// <summary>
/// Convention checks that cover every code and every exception added later, with no new test needed.
/// </summary>
public partial class ErrorCodeConventionTests
{
    private static readonly string[] AllCodes = typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    public static TheoryData<Type> DomainExceptionTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in typeof(DomainException).Assembly.GetTypes()
                     .Where(t => t.IsSubclassOf(typeof(DomainException)) && !t.IsAbstract))
        {
            data.Add(type);
        }

        return data;
    }

    [Fact]
    public void ErrorCodes_AreScreamingSnakeCase()
    {
        var invalid = AllCodes.Where(c => !ScreamingSnake().IsMatch(c)).ToArray();

        Assert.Empty(invalid);
    }

    [Fact]
    public void ErrorCodes_AreUnique()
    {
        var duplicates = AllCodes.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void ErrorCodes_AreAllDocumented()
    {
        // 02-naming.md: a code missing from doc/error-codes.md shows up raw on the FE.
        var documented = ReadDocumentedCodes();

        Assert.Empty(AllCodes.Except(documented));
    }

    [Fact]
    public void DocumentedCodes_AllExistInErrorCodes()
    {
        var documented = ReadDocumentedCodes();

        Assert.Empty(documented.Except(AllCodes));
    }

    [Theory]
    [MemberData(nameof(DomainExceptionTypes))]
    public void DomainException_CarriesKnownCodeAndEnglishMessage(Type exceptionType)
    {
        var exception = (DomainException)Activator.CreateInstance(exceptionType)!;

        Assert.Contains(exception.Code, AllCodes);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.True(exception.Message.All(char.IsAscii), $"{exceptionType.Name} message must be English.");
    }

    [Theory]
    [MemberData(nameof(DomainExceptionTypes))]
    public void DomainException_NameEndsWithException(Type exceptionType)
    {
        Assert.EndsWith("Exception", exceptionType.Name);
    }

    private static HashSet<string> ReadDocumentedCodes()
    {
        var doc = File.ReadAllText(Path.Combine(SolutionRoot(), "doc", "error-codes.md"));

        return DocTableCode().Matches(doc).Select(m => m.Groups[1].Value).ToHashSet();
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Harmonia.Solution.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Harmonia.Solution.slnx not found above the test output.");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$")]
    private static partial Regex ScreamingSnake();

    [GeneratedRegex(@"^\|\s*`([A-Z0-9_]+)`\s*\|", RegexOptions.Multiline)]
    private static partial Regex DocTableCode();
}
