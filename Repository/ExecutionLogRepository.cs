using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using StoredProcedureAPI.Models;
using System.Data;

namespace StoredProcedureAPI.Repository
{
    public class ExecutionLogRepository : IExecutionLogRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ExecutionLogRepository>? _logger;

        public ExecutionLogRepository(IConfiguration config, ILogger<ExecutionLogRepository>? logger = null)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");
            _logger = logger;
        }

        public async Task LogAsync(ProcedureExecutionLog log, CancellationToken cancellationToken = default)
        {
            const string insertMasterSql = @"
INSERT INTO dbo.[ProcedureExecutionLog]
([ExecutedAt],[SchemaName],[ProcedureName],[RowCount],[DurationMs])
VALUES (@ExecutedAt,@SchemaName,@ProcedureName,@RowCount,@DurationMs);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            const string insertParamSql = @"
INSERT INTO dbo.[ProcedureExecutionParameter]
([ExecutionId],[ParameterName],[DataType],[IsOutput],[IsNullable],[ParameterValue])
VALUES (@ExecutionId,@ParameterName,@DataType,@IsOutput,@IsNullable,@ParameterValue);";

            const string insertColumnSql = @"
INSERT INTO dbo.[ProcedureExecutionColumn]
([ExecutionId],[ColumnOrdinal],[ColumnName],[DataType],[IsNullable])
VALUES (@ExecutionId,@ColumnOrdinal,@ColumnName,@DataType,@IsNullable);";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            try
            {
                var masterId = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(insertMasterSql, log, tx, cancellationToken: cancellationToken));

                log.Id = masterId;

                foreach (var p in log.Parameters)
                {
                    p.ExecutionId = masterId;
                    await conn.ExecuteAsync(new CommandDefinition(insertParamSql, new
                    {
                        p.ExecutionId,
                        p.ParameterName,
                        p.DataType,
                        p.IsOutput,
                        p.IsNullable,
                        p.ParameterValue
                    }, tx, cancellationToken: cancellationToken));
                }

                foreach (var c in log.Columns)
                {
                    c.ExecutionId = masterId;
                    await conn.ExecuteAsync(new CommandDefinition(insertColumnSql, new
                    {
                        c.ExecutionId,
                        c.ColumnOrdinal,
                        c.ColumnName,
                        c.DataType,
                        c.IsNullable
                    }, tx, cancellationToken: cancellationToken));
                }

                await tx.CommitAsync(cancellationToken);
            }
            catch (SqlException sqlEx)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger?.LogError(sqlEx, "Failed logging execution: {Message}", sqlEx.Message);
                throw;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<IEnumerable<ProcedureExecutionLog>> GetRecentAsync(int top = 50, CancellationToken cancellationToken = default)
        {
            const string masterSql = @"
SELECT TOP (@Top) Id, ExecutedAt, SchemaName, ProcedureName, [RowCount], DurationMs
FROM dbo.[ProcedureExecutionLog]
ORDER BY Id DESC;";

            await using var conn = new SqlConnection(_connectionString);
            var masters = (await conn.QueryAsync<ProcedureExecutionLog>(
                new CommandDefinition(masterSql, new { Top = top }, cancellationToken: cancellationToken))).ToList();

            if (masters.Count == 0) return masters;

            var ids = masters.Select(m => m.Id).ToArray();

            var paramsSql = @"
SELECT ExecutionId, ParameterName, DataType, IsOutput, IsNullable, ParameterValue
FROM dbo.[ProcedureExecutionParameter]
WHERE ExecutionId IN @Ids;";

            var colsSql = @"
SELECT ExecutionId, ColumnOrdinal, ColumnName, DataType, IsNullable
FROM dbo.[ProcedureExecutionColumn]
WHERE ExecutionId IN @Ids;";

            var paramRows = await conn.QueryAsync<ProcedureExecutionParameter>(paramsSql, new { Ids = ids });
            var colRows = await conn.QueryAsync<ProcedureExecutionColumn>(colsSql, new { Ids = ids });

            var map = masters.ToDictionary(m => m.Id);
            foreach (var p in paramRows) if (map.TryGetValue(p.ExecutionId, out var m)) m.Parameters.Add(p);
            foreach (var c in colRows) if (map.TryGetValue(c.ExecutionId, out var m)) m.Columns.Add(c);

            return masters;
        }

        public async Task<ProcedureExecutionLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            const string masterSql = @"
SELECT Id, ExecutedAt, SchemaName, ProcedureName, [RowCount], DurationMs
FROM dbo.[ProcedureExecutionLog]
WHERE Id = @Id;";

            await using var conn = new SqlConnection(_connectionString);
            var master = await conn.QueryFirstOrDefaultAsync<ProcedureExecutionLog>(
                new CommandDefinition(masterSql, new { Id = id }, cancellationToken: cancellationToken));
            if (master == null) return null;

            var paramsSql = @"
SELECT ExecutionId, ParameterName, DataType, IsOutput, IsNullable, ParameterValue
FROM dbo.[ProcedureExecutionParameter]
WHERE ExecutionId = @Id;";

            var colsSql = @"
SELECT ExecutionId, ColumnOrdinal, ColumnName, DataType, IsNullable
FROM dbo.[ProcedureExecutionColumn]
WHERE ExecutionId = @Id
ORDER BY ColumnOrdinal;";

            var parameters = await conn.QueryAsync<ProcedureExecutionParameter>(paramsSql, new { Id = id });
            var columns = await conn.QueryAsync<ProcedureExecutionColumn>(colsSql, new { Id = id });

            master.Parameters.AddRange(parameters);
            master.Columns.AddRange(columns);

            return master;
        }

        public async Task<IEnumerable<ProcedureExecutionLog>> QueryAsync(
            string? schemaName = null,
            string? procedureName = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            int top = 100,
            CancellationToken cancellationToken = default)
        {
            var conditions = new List<string>();
            var args = new DynamicParameters();
            args.Add("Top", top);

            if (!string.IsNullOrWhiteSpace(schemaName))
            {
                conditions.Add("SchemaName = @SchemaName");
                args.Add("SchemaName", schemaName);
            }
            if (!string.IsNullOrWhiteSpace(procedureName))
            {
                conditions.Add("ProcedureName = @ProcedureName");
                args.Add("ProcedureName", procedureName);
            }
            if (fromUtc.HasValue)
            {
                conditions.Add("ExecutedAt >= @FromUtc");
                args.Add("FromUtc", fromUtc.Value);
            }
            if (toUtc.HasValue)
            {
                conditions.Add("ExecutedAt <= @ToUtc");
                args.Add("ToUtc", toUtc.Value);
            }

            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

            var masterSql = $@"
SELECT TOP (@Top) Id, ExecutedAt, SchemaName, ProcedureName, [RowCount], DurationMs
FROM dbo.[ProcedureExecutionLog]
{where}
ORDER BY Id DESC;";

            await using var conn = new SqlConnection(_connectionString);
            var masters = (await conn.QueryAsync<ProcedureExecutionLog>(
                new CommandDefinition(masterSql, args, cancellationToken: cancellationToken))).ToList();

            if (masters.Count == 0) return masters;

            var ids = masters.Select(m => m.Id).ToArray();

            var paramRows = await conn.QueryAsync<ProcedureExecutionParameter>(@"
SELECT ExecutionId, ParameterName, DataType, IsOutput, IsNullable, ParameterValue
FROM dbo.[ProcedureExecutionParameter]
WHERE ExecutionId IN @Ids;", new { Ids = ids });

            var colRows = await conn.QueryAsync<ProcedureExecutionColumn>(@"
SELECT ExecutionId, ColumnOrdinal, ColumnName, DataType, IsNullable
FROM dbo.[ProcedureExecutionColumn]
WHERE ExecutionId IN @Ids;", new { Ids = ids });

            var map = masters.ToDictionary(m => m.Id);
            foreach (var p in paramRows) if (map.TryGetValue(p.ExecutionId, out var m)) m.Parameters.Add(p);
            foreach (var c in colRows) if (map.TryGetValue(c.ExecutionId, out var m)) m.Columns.Add(c);

            return masters;
        }
        // New: first or default (latest single log with children)
        public async Task<ProcedureExecutionLog?> GetLatestAsync(CancellationToken cancellationToken = default)
        {
            const string masterSql = @"
SELECT TOP (1) Id, ExecutedAt, SchemaName, ProcedureName, [RowCount], DurationMs
FROM dbo.[ProcedureExecutionLog]
ORDER BY Id DESC;";

            await using var conn = new SqlConnection(_connectionString);
            var master = await conn.QueryFirstOrDefaultAsync<ProcedureExecutionLog>(
                new CommandDefinition(masterSql, cancellationToken: cancellationToken));

            if (master == null) return null;

            var parameters = await conn.QueryAsync<ProcedureExecutionParameter>(@"
SELECT ExecutionId, ParameterName, DataType, IsOutput, IsNullable, ParameterValue
FROM dbo.[ProcedureExecutionParameter]
WHERE ExecutionId = @Id;", new { Id = master.Id });

            var columns = await conn.QueryAsync<ProcedureExecutionColumn>(@"
SELECT ExecutionId, ColumnOrdinal, ColumnName, DataType, IsNullable
FROM dbo.[ProcedureExecutionColumn]
WHERE ExecutionId = @Id
ORDER BY ColumnOrdinal;", new { Id = master.Id });

            master.Parameters.AddRange(parameters);
            master.Columns.AddRange(columns);
            return master;
        }

    }
}