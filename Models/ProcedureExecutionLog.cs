namespace StoredProcedureAPI.Models
{
    public class ProcedureExecutionLog
    {
        public int Id { get; set; } // PK
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        public required string SchemaName { get; set; }
        public required string ProcedureName { get; set; }

        public int RowCount { get; set; }
        public int DurationMs { get; set; }

        public List<ProcedureExecutionParameter> Parameters { get; set; } = new();
        public List<ProcedureExecutionColumn> Columns { get; set; } = new();
    }
}