namespace StoredProcedureAPI.Models
{
    public class ProcedureExecutionParameter
    {
        // Set by repository after master log insert
        public int ExecutionId { get; set; }

        public required string ParameterName { get; set; }
        public required string DataType { get; set; }
        public bool IsOutput { get; set; }
        public bool IsNullable { get; set; }

        public string? ParameterValue { get; set; }
        public string? OutputValue { get; set; } // for future output parameter capture
    }
}