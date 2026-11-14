namespace Harmonia.API.Tests;

public static class TestPaths
{
    public static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Harmonia.Solution.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Harmonia.Solution.slnx not found above the test output.");
    }
}
