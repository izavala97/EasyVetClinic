using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Data;

internal static class CommercialPersistence
{
    internal static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException switch
    {
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode is 2067 or 1555,
        SqlException sqlServer => sqlServer.Number is 2601 or 2627,
        _ => false
    };
}
