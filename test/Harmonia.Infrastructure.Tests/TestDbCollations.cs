using System.Globalization;
using Harmonia.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace Harmonia.Infrastructure.Tests;

/// <summary>
/// SQLite knows no SQL Server collation, so a query naming one fails to prepare. Registers a stand-in under the same
/// name that ignores case and diacritics.
/// </summary>
public static class TestDbCollations
{
    public static void Register(SqliteConnection connection) =>
        connection.CreateCollation(HarmoniaDbContext.SearchCollation, (x, y) => string.Compare(
            x, y, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
}
