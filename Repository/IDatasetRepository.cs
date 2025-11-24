using StoredProcedureAPI.Models;
namespace StoredProcedureAPI.Repository
{
    public interface IDatasetRepository
    {
        Task<int> CreateFromProcedureExecutionAsync(int procedureExecutionId, string title, string? description, CancellationToken ct = default);
        Task<int> CreateFromBuilderAsync(int builderId, string title, string? description, CancellationToken ct = default);
        Task<int> CreateFromInlineAsync(int inlineId, string title, string? description, CancellationToken ct = default);

        Task<Dataset?> GetDatasetAsync(int datasetId, CancellationToken ct = default);
        Task<IEnumerable<Dataset>> GetRecentAsync(int top = 50, CancellationToken ct = default);
        Task<bool> UpdateDatasetAsync(int datasetId, string title, string? description, CancellationToken ct = default);
        Task<bool> ReplaceColumnsAsync(int datasetId, IEnumerable<(string ColumnName, string DataType)> columns, CancellationToken ct = default);
        Task<IEnumerable<SourceFlag>> GetSourceFlagsAsync(CancellationToken ct = default);
    }

}
