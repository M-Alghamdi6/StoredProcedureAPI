using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using StoredProcedureAPI.Models;

namespace StoredProcedureAPI.Repository
{
    public class DatasetRepository : IDatasetRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<DatasetRepository>? _logger;

        // SourceFlags mapping (1=QueryBuilder, 2=INLINEQuery, 3=Stored Procedure)
        private const int SourceTypeBuilder = 1;
        private const int SourceTypeInline = 2;
        private const int SourceTypeProcedure = 3;

        public DatasetRepository(IConfiguration config, ILogger<DatasetRepository>? logger = null)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");
            _logger = logger;
        }

        public async Task<int> CreateFromProcedureExecutionAsync(int procedureExecutionId, string title, string? description, CancellationToken ct = default)
        {
            ValidateTitle(title);

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            try
            {
                const string checkExecSql = "SELECT 1 FROM dbo.ProcedureExecutionLog WHERE Id = @Id;";
                var exists = await conn.ExecuteScalarAsync<int?>(
                    new CommandDefinition(checkExecSql, new { Id = procedureExecutionId }, tx, cancellationToken: ct));
                if (exists is null)
                    throw new InvalidOperationException($"ProcedureExecutionLog {procedureExecutionId} not found.");

                const string loadColsSql = @"
SELECT ColumnOrdinal, ColumnName, DataType
FROM dbo.ProcedureExecutionColumn
WHERE ExecutionId = @ExecId
ORDER BY ColumnOrdinal;";
                var execColumns = (await conn.QueryAsync(
                    new CommandDefinition(loadColsSql, new { ExecId = procedureExecutionId }, tx, cancellationToken: ct))).ToList();

                if (execColumns.Count == 0)
                    throw new InvalidOperationException("Execution has no column metadata.");

                const string insertDatasetSql = @"
INSERT INTO dbo.Datasets
(DataSetTitle, Description, SourceType, BuilderId, InlineId, ProcedureExecutionId)
VALUES (@Title, @Desc, @SourceType, NULL, NULL, @ProcExecId);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int datasetId = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(insertDatasetSql, new
                    {
                        Title = title,
                        Desc = description,
                        SourceType = SourceTypeProcedure,
                        ProcExecId = procedureExecutionId
                    }, tx, cancellationToken: ct));

                const string insertColSql = @"
INSERT INTO dbo.DatasetColumns
(DataSetId, ColumnName, DataType, ColumnOrder)
VALUES (@DataSetId, @ColumnName, @DataType, @ColumnOrder);";

                int order = 1;
                foreach (var c in execColumns)
                {
                    await conn.ExecuteAsync(new CommandDefinition(insertColSql, new
                    {
                        DataSetId = datasetId,
                        ColumnName = c.ColumnName,
                        DataType = c.DataType,
                        ColumnOrder = order++
                    }, tx, cancellationToken: ct));
                }

                await tx.CommitAsync(ct);
                return datasetId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateFromProcedureExecutionAsync failed. ProcedureExecutionId={Id}", procedureExecutionId);
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<int> CreateFromBuilderAsync(int builderId, string title, string? description, CancellationToken ct = default)
        {
            ValidateTitle(title);

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            try
            {
                const string checkBuilderSql = "SELECT 1 FROM dbo.QueryBuilderData WHERE Id = @Id;";
                var exists = await conn.ExecuteScalarAsync<int?>(
                    new CommandDefinition(checkBuilderSql, new { Id = builderId }, tx, cancellationToken: ct));
                if (exists is null)
                    throw new InvalidOperationException($"QueryBuilderData {builderId} not found.");

                const string insertDatasetSql = @"
INSERT INTO dbo.Datasets
(DataSetTitle, Description, SourceType, BuilderId, InlineId, ProcedureExecutionId)
VALUES (@Title, @Desc, @SourceType, @BuilderId, NULL, NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int datasetId = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(insertDatasetSql, new
                    {
                        Title = title,
                        Desc = description,
                        SourceType = SourceTypeBuilder,
                        BuilderId = builderId
                    }, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
                return datasetId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateFromBuilderAsync failed. BuilderId={Id}", builderId);
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<int> CreateFromInlineAsync(int inlineId, string title, string? description, CancellationToken ct = default)
        {
            ValidateTitle(title);

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            try
            {
                const string checkInlineSql = "SELECT 1 FROM dbo.InLineQueriesData WHERE Id = @Id;";
                var exists = await conn.ExecuteScalarAsync<int?>(
                    new CommandDefinition(checkInlineSql, new { Id = inlineId }, tx, cancellationToken: ct));
                if (exists is null)
                    throw new InvalidOperationException($"InLineQueriesData {inlineId} not found.");

                const string insertDatasetSql = @"
INSERT INTO dbo.Datasets
(DataSetTitle, Description, SourceType, BuilderId, InlineId, ProcedureExecutionId)
VALUES (@Title, @Desc, @SourceType, NULL, @InlineId, NULL);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int datasetId = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(insertDatasetSql, new
                    {
                        Title = title,
                        Desc = description,
                        SourceType = SourceTypeInline,
                        InlineId = inlineId
                    }, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
                return datasetId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateFromInlineAsync failed. InlineId={Id}", inlineId);
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<int> CreateUnifiedAsync(
            int sourceType,
            string title,
            string? description,
            int? builderId,
            int? inlineId,
            int? procedureExecutionId,
            IEnumerable<(string ColumnName, string DataType)>? initialColumns,
            CancellationToken ct = default)
        {
            ValidateTitle(title);

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            try
            {
                int datasetId;

                switch (sourceType)
                {
                    case SourceTypeBuilder:
                        if (builderId is null || builderId <= 0)
                            throw new ArgumentException("BuilderId is required for builder source.", nameof(builderId));

                        const string checkBuilderSql = "SELECT 1 FROM dbo.QueryBuilderData WHERE Id = @Id;";
                        var builderExists = await conn.ExecuteScalarAsync<int?>(
                            new CommandDefinition(checkBuilderSql, new { Id = builderId }, tx, cancellationToken: ct));
                        if (builderExists is null)
                            throw new InvalidOperationException($"QueryBuilderData {builderId} not found.");

                        datasetId = await InsertDatasetAsync(conn, tx, title, description, SourceTypeBuilder, builderId, null, null, ct);

                        if (initialColumns != null)
                            await InsertColumnsAsync(conn, tx, datasetId, initialColumns, ct);
                        break;

                    case SourceTypeInline:
                        if (inlineId is null || inlineId <= 0)
                            throw new ArgumentException("InlineId is required for inline source.", nameof(inlineId));

                        const string checkInlineSql = "SELECT 1 FROM dbo.InLineQueriesData WHERE Id = @Id;";
                        var inlineExists = await conn.ExecuteScalarAsync<int?>(
                            new CommandDefinition(checkInlineSql, new { Id = inlineId }, tx, cancellationToken: ct));
                        if (inlineExists is null)
                            throw new InvalidOperationException($"InLineQueriesData {inlineId} not found.");

                        datasetId = await InsertDatasetAsync(conn, tx, title, description, SourceTypeInline, null, inlineId, null, ct);

                        if (initialColumns != null)
                            await InsertColumnsAsync(conn, tx, datasetId, initialColumns, ct);
                        break;

                    case SourceTypeProcedure:
                        if (procedureExecutionId is null || procedureExecutionId <= 0)
                            throw new ArgumentException("ProcedureExecutionId is required for procedure source.", nameof(procedureExecutionId));

                        const string checkExecSql = "SELECT 1 FROM dbo.ProcedureExecutionLog WHERE Id = @Id;";
                        var execExists = await conn.ExecuteScalarAsync<int?>(
                            new CommandDefinition(checkExecSql, new { Id = procedureExecutionId }, tx, cancellationToken: ct));
                        if (execExists is null)
                            throw new InvalidOperationException($"ProcedureExecutionLog {procedureExecutionId} not found.");

                        const string loadColsSql = @"
SELECT ColumnOrdinal, ColumnName, DataType
FROM dbo.ProcedureExecutionColumn
WHERE ExecutionId = @ExecId
ORDER BY ColumnOrdinal;";
                        var execColumns = (await conn.QueryAsync(
                            new CommandDefinition(loadColsSql, new { ExecId = procedureExecutionId }, tx, cancellationToken: ct))).ToList();
                        if (execColumns.Count == 0)
                            throw new InvalidOperationException("Execution has no column metadata.");

                        datasetId = await InsertDatasetAsync(conn, tx, title, description, SourceTypeProcedure, null, null, procedureExecutionId, ct);

                        var mapped = execColumns.Select(c => (ColumnName: (string)c.ColumnName, DataType: (string)c.DataType));
                        await InsertColumnsAsync(conn, tx, datasetId, mapped, ct);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(sourceType), "Unknown source type.");
                }

                await tx.CommitAsync(ct);
                return datasetId;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        private async Task<int> InsertDatasetAsync(
            SqlConnection conn,
            SqlTransaction tx,
            string title,
            string? description,
            int sourceType,
            int? builderId,
            int? inlineId,
            int? procedureExecutionId,
            CancellationToken ct)
        {
            const string sql = @"
INSERT INTO dbo.Datasets
(DataSetTitle, Description, SourceType, BuilderId, InlineId, ProcedureExecutionId)
VALUES (@Title, @Desc, @SourceType, @BuilderId, @InlineId, @ProcExecId);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, new
                {
                    Title = title,
                    Desc = description,
                    SourceType = sourceType,
                    BuilderId = builderId,
                    InlineId = inlineId,
                    ProcExecId = procedureExecutionId
                }, tx, cancellationToken: ct));
        }

        private async Task InsertColumnsAsync(
            SqlConnection conn,
            SqlTransaction tx,
            int datasetId,
            IEnumerable<(string ColumnName, string DataType)> columns,
            CancellationToken ct)
        {
            const string insertColSql = @"
INSERT INTO dbo.DatasetColumns
(DataSetId, ColumnName, DataType, ColumnOrder)
VALUES (@DataSetId, @ColumnName, @DataType, @ColumnOrder);";

            int order = 1;
            foreach (var c in columns)
            {
                await conn.ExecuteAsync(new CommandDefinition(insertColSql, new
                {
                    DataSetId = datasetId,
                    ColumnName = c.ColumnName,
                    DataType = c.DataType,
                    ColumnOrder = order++
                }, tx, cancellationToken: ct));
            }
        }

        public async Task<Dataset?> GetDatasetAsync(int datasetId, CancellationToken ct = default)
        {
            const string sql = @"
SELECT d.DataSetId,
       d.DataSetTitle,
       d.Description,
       d.SourceType,
       d.BuilderId,
       d.InlineId,
       d.ProcedureExecutionId,
       d.CreatedAt,
       d.ModifiedAt,
       sf.SourceName
FROM dbo.Datasets d
LEFT JOIN dbo.SourceFlags sf ON sf.SourceType = d.SourceType
WHERE d.DataSetId = @Id;

SELECT ColumnId,
       DataSetId,
       ColumnName,
       DataType,
       ColumnOrder
FROM dbo.DatasetColumns
WHERE DataSetId = @Id
ORDER BY ColumnOrder;";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            using var multi = await conn.QueryMultipleAsync(new CommandDefinition(sql, new { Id = datasetId }, cancellationToken: ct));

            var dataset = await multi.ReadFirstOrDefaultAsync<Dataset>();
            if (dataset == null) return null;

            dataset.Columns = (await multi.ReadAsync<DatasetColumn>()).ToList();
            return dataset;
        }

        public async Task<IEnumerable<Dataset>> GetRecentAsync(int top = 50, CancellationToken ct = default)
        {
            const string sql = @"
SELECT TOP (@Top)
       d.DataSetId,
       d.DataSetTitle,
       d.Description,
       d.SourceType,
       d.BuilderId,
       d.InlineId,
       d.ProcedureExecutionId,
       d.CreatedAt,
       d.ModifiedAt,
       sf.SourceName
FROM dbo.Datasets d
LEFT JOIN dbo.SourceFlags sf ON sf.SourceType = d.SourceType
ORDER BY d.DataSetId DESC;";

            await using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<Dataset>(new CommandDefinition(sql, new { Top = top }, cancellationToken: ct));
        }

        public async Task<bool> UpdateDatasetAsync(int datasetId, string title, string? description, CancellationToken ct = default)
        {
            ValidateTitle(title);

            const string sql = @"
UPDATE dbo.Datasets
SET DataSetTitle = @Title,
    Description  = @Desc,
    ModifiedAt   = SYSUTCDATETIME()
WHERE DataSetId = @Id;";

            await using var conn = new SqlConnection(_connectionString);
            var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Title = title,
                Desc = description,
                Id = datasetId
            }, cancellationToken: ct));
            return affected == 1;
        }

        public async Task<bool> ReplaceColumnsAsync(int datasetId, IEnumerable<(string ColumnName, string DataType)> columns, CancellationToken ct = default)
        {
            var cols = columns.ToList();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            try
            {
                const string checkSql = "SELECT 1 FROM dbo.Datasets WHERE DataSetId = @Id;";
                var exists = await conn.ExecuteScalarAsync<int?>(
                    new CommandDefinition(checkSql, new { Id = datasetId }, tx, cancellationToken: ct));
                if (exists is null)
                    return false;

                const string deleteSql = "DELETE FROM dbo.DatasetColumns WHERE DataSetId = @Id;";
                await conn.ExecuteAsync(new CommandDefinition(deleteSql, new { Id = datasetId }, tx, cancellationToken: ct));

                const string insertSql = @"
INSERT INTO dbo.DatasetColumns
(DataSetId, ColumnName, DataType, ColumnOrder)
VALUES (@DataSetId, @ColumnName, @DataType, @ColumnOrder);";

                int order = 1;
                foreach (var c in cols)
                {
                    await conn.ExecuteAsync(new CommandDefinition(insertSql, new
                    {
                        DataSetId = datasetId,
                        ColumnName = c.ColumnName,
                        DataType = c.DataType,
                        ColumnOrder = order++
                    }, tx, cancellationToken: ct));
                }

                const string touchSql = "UPDATE dbo.Datasets SET ModifiedAt = SYSUTCDATETIME() WHERE DataSetId = @Id;";
                await conn.ExecuteAsync(new CommandDefinition(touchSql, new { Id = datasetId }, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ReplaceColumnsAsync failed. DataSetId={Id}", datasetId);
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<IEnumerable<SourceFlag>> GetSourceFlagsAsync(CancellationToken ct = default)
        {
            const string sql = "SELECT SourceType, SourceName FROM dbo.SourceFlags ORDER BY SourceType;";
            await using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<SourceFlag>(new CommandDefinition(sql, cancellationToken: ct));
        }

        private static void ValidateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required.", nameof(title));
        }
    }
}
