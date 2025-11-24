using StoredProcedureAPI.Models;

namespace StoredProcedureAPI.Repository
{
    public interface IExecutionLogRepository
    {
        Task LogAsync(ProcedureExecutionLog log, CancellationToken cancellationToken = default);

        // Existing
        Task<IEnumerable<ProcedureExecutionLog>> GetRecentAsync(int top = 50, CancellationToken cancellationToken = default);
        Task<ProcedureExecutionLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<ProcedureExecutionLog>> QueryAsync(
            string? schemaName = null,
            string? procedureName = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            int top = 100,
            CancellationToken cancellationToken = default);

        // first or default (latest single entry)
        Task<ProcedureExecutionLog?> GetLatestAsync(CancellationToken cancellationToken = default);
    }
}
