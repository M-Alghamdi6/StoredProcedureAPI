namespace StoredProcedureAPI.Models
{
    // Renamed from ProcedureExecutionColumnLog to align with table name
    public class ProcedureExecutionColumn
    {
        // Set by repository after master log insert
        public int ExecutionId { get; set; }

        public int ColumnOrdinal { get; set; }
        public required string ColumnName { get; set; }
        public required string DataType { get; set; }
        public bool IsNullable { get; set; }
    }
}