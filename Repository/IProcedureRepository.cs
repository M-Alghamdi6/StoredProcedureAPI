

using StoredProcedureAPI.Models;
using Dapper;
using System.Data;

namespace StoredProcedureAPI.Repository
{
    public interface IProcedureRepository
    {
        Task<IEnumerable<SchemaModel>> GetSchemasAsync();
        Task<IEnumerable<StoredProcedure>> GetProceduresBySchemaAsync(string schemaName);
        Task<IEnumerable<ProcedureParameter>> GetParametersAsync(string schemaName, string procedureName);
        Task<List<IDictionary<string, object>>> ExecuteProcedureAsync(
            string schemaName,
            string procedureName,
            DynamicParameters parameters,
            int commandTimeoutSeconds = 30,
            CancellationToken cancellationToken = default);
        Task<bool> SchemaExistsAsync(string schemaName);
        Task<bool> ProcedureExistsAsync(string schemaName, string procedureName);
    }
}