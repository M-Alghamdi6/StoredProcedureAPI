using Dapper;
using Microsoft.Data.SqlClient;

public class DashboardService
{
    private readonly string _connectionString;

    public DashboardService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public async Task<object?> ExecuteProcedureAsync(
        string schema,
        string procedure,
        Dictionary<string, object> parameters)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();

            // 1. Confirm this is a stored procedure
            var isStoredProcedure = await conn.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*)
                  FROM sys.procedures
                  WHERE SCHEMA_NAME(schema_id) = @Schema
                    AND name = @Proc",
                new { Schema = schema, Proc = procedure });

            if (isStoredProcedure == 0)
                throw new Exception("Invalid stored procedure");

            // 2. Reject dangerous stored procedures
            var definition = await conn.ExecuteScalarAsync<string>(
                @"SELECT m.definition
                  FROM sys.procedures p
                  JOIN sys.sql_modules m ON p.object_id = m.object_id
                  WHERE SCHEMA_NAME(p.schema_id) = @Schema
                    AND p.name = @Proc",
                new { Schema = schema, Proc = procedure });

            if (ContainsUnsafeCode(definition))
                throw new Exception("Procedure contains unsafe operations.");

            // 3. Build parameters safely  
            var dyn = new DynamicParameters();
            foreach (var p in parameters)
                dyn.Add("@" + p.Key, p.Value);

            //  4. Execute with timeout (10 seconds)
            var result = await conn.QueryAsync(
                $"{schema}.{procedure}",
                dyn,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: 10
            );

            return result;
        }
    }

    private bool ContainsUnsafeCode(string? definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
            return false;

        string text = definition.ToUpper();

        string[] badWords = {
            "INSERT ", "UPDATE ", "DELETE ", "DROP ", "ALTER ",
            "MERGE ", "EXEC(", "EXECUTE(", "SP_EXECUTESQL"
        };

        return badWords.Any(b => text.Contains(b));
    }
}
