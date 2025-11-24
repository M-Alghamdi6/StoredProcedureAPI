using Microsoft.Data.SqlClient;
using StoredProcedureAPI.Models;
using System.Data;

public class SomeServiceCapturingLog
{
    public async Task<ProcedureExecutionLog> ExecuteAndBuildLogAsync(
        string connectionString,
        string schema,
        string procName,
        Dictionary<string, object?> parameters,
        CancellationToken ct)
    {
        var log = new ProcedureExecutionLog
        {
            SchemaName = schema,
            ProcedureName = procName
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand($"{schema}.{procName}", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        foreach (var kv in parameters)
        {
            cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        // Column metadata (first result set)
        var schemaTable = reader.GetSchemaTable();
        if (schemaTable != null)
        {
            int ordinal = 0;
            foreach (System.Data.DataRow row in schemaTable.Rows)
            {
                log.Columns.Add(new ProcedureExecutionColumn
                {
                    ColumnOrdinal = ordinal++,
                    ColumnName = (string)row["ColumnName"],
                    DataType = ((Type)row["DataType"]).Name,
                    IsNullable = (bool)row["AllowDBNull"]
                });
            }
        }

        int rowCount = 0;
        while (await reader.ReadAsync(ct))
        {
            rowCount++;
        }

        sw.Stop();

        log.RowCount = rowCount;
        log.DurationMs = (int)sw.ElapsedMilliseconds;

        // Parameter values (after execution to include output)
        foreach (SqlParameter sqlParam in cmd.Parameters)
        {
            log.Parameters.Add(new ProcedureExecutionParameter
            {
                ParameterName = sqlParam.ParameterName,
                DataType = sqlParam.SqlDbType.ToString(),
                IsOutput = (sqlParam.Direction == ParameterDirection.Output || sqlParam.Direction == ParameterDirection.InputOutput),
                IsNullable = sqlParam.IsNullable,
                ParameterValue = sqlParam.Value == DBNull.Value ? null : sqlParam.Value?.ToString()
            });
        }

        return log;
    }
}