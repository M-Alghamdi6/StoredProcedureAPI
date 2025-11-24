using StoredProcedureAPI.Models;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;

namespace StoredProcedureAPI.Repository
{
    // Repository for interacting with SQL Server stored procedures and schema metadata
    public class ProcedureRepository : IProcedureRepository
    {
        private readonly string _connectionString;

        // Constructor: initializes connection string from configuration
        public ProcedureRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }

        // Retrieves all database schemas (read-only metadata)
        public async Task<IEnumerable<SchemaModel>> GetSchemasAsync()
        {
            await using var connection = new SqlConnection(_connectionString);
            var sql = "SELECT name AS SchemaName FROM sys.schemas ORDER BY name";
            return await connection.QueryAsync<SchemaModel>(sql);
        }

        // Retrieves all stored procedures for a given schema
        // Validates input and checks schema existence before querying
        public async Task<IEnumerable<StoredProcedure>> GetProceduresBySchemaAsync(string schemaName)
        {
            // Validate schema name input
            if (string.IsNullOrWhiteSpace(schemaName))
                throw new ArgumentException("Schema name must be provided.", nameof(schemaName));

            schemaName = schemaName.Trim();

            // Basic SQL identifier validation: letters/underscore, followed by letters/digits/underscore
            if (!System.Text.RegularExpressions.Regex.IsMatch(schemaName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                return Enumerable.Empty<StoredProcedure>();

            await using var connection = new SqlConnection(_connectionString);

            // Fast existence check for the schema
            var schemaExists = await connection.ExecuteScalarAsync<int>(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.schemas WHERE name = @SchemaName) THEN 1 ELSE 0 END",
                new { SchemaName = schemaName });

            if (schemaExists == 0)
                return Enumerable.Empty<StoredProcedure>();

            // Query for procedures in the specified schema
            var sql = @"
                SELECT p.name AS [Name]
                FROM sys.procedures p
                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                WHERE s.name = @SchemaName
                ORDER BY p.name";

            return await connection.QueryAsync<StoredProcedure>(sql, new { SchemaName = schemaName });
        }

        // Retrieves parameter metadata for a given stored procedure
        // Ensures parameter names start with '@'
        public async Task<IEnumerable<ProcedureParameter>> GetParametersAsync(string schemaName, string procedureName)
        {
            await using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    p.name AS ParameterName,
                    t.name AS DataType,
                    p.max_length AS MaxLength,
                    p.is_output AS IsOutput,
                    p.is_nullable AS IsNullable
                FROM sys.parameters p
                INNER JOIN sys.objects o ON p.object_id = o.object_id
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                INNER JOIN sys.types t ON p.user_type_id = t.user_type_id
                WHERE s.name = @SchemaName AND o.name = @ProcedureName
                ORDER BY p.parameter_id";
            var result = await connection.QueryAsync<ProcedureParameter>(sql, new { SchemaName = schemaName, ProcedureName = procedureName });

            // Ensure ParameterName starts with '@'
            foreach (var p in result)
                if (!p.ParameterName.StartsWith("@"))
                    p.ParameterName = "@" + p.ParameterName;

            return result;
        }

        // Executes a stored procedure and returns the result as a list of dictionaries
        // Each dictionary represents a row with column name/value pairs
        public async Task<List<IDictionary<string, object>>> ExecuteProcedureAsync(
            string schemaName,
            string procedureName,
            DynamicParameters parameters,
            int commandTimeoutSeconds = 30,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            var fullName = $"{schemaName}.{procedureName}";
            // Use QueryAsync with CommandType.StoredProcedure
            var rows = (await connection.QueryAsync(fullName, parameters, commandType: CommandType.StoredProcedure, commandTimeout: commandTimeoutSeconds))
                       .Select(r => (IDictionary<string, object>)r)
                       .Select(dict => dict.ToDictionary(kv => kv.Key, kv => kv.Value))
                       .Select(d => (IDictionary<string, object>)new Dictionary<string, object>(d))
                       .ToList();

            return rows;
        }

        // Checks if a schema exists in the database
        public async Task<bool> SchemaExistsAsync(string schemaName)
        {
            const string sql = @"
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM sys.schemas WHERE name = @SchemaName
        ) THEN 1 ELSE 0 END";

            await using var conn = new SqlConnection(_connectionString);
            return await conn.ExecuteScalarAsync<int>(sql, new { SchemaName = schemaName }) == 1;
        }

        // Checks if a stored procedure exists in the specified schema
        public async Task<bool> ProcedureExistsAsync(string schemaName, string procedureName)
        {
            const string sql = @"
        SELECT CASE WHEN EXISTS (
            SELECT 1
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            WHERE s.name = @SchemaName
              AND p.name = @ProcedureName
        ) THEN 1 ELSE 0 END";

            await using var conn = new SqlConnection(_connectionString);
            return await conn.ExecuteScalarAsync<int>(sql, new
            {
                SchemaName = schemaName,
                ProcedureName = procedureName
            }) == 1;
        }
    }
}