namespace StoredProcedureAPI.Models
{
    public class ProcedureExecutionResponse
    {
        public IEnumerable<string> Columns { get; set; } = Array.Empty<string>();
        public IEnumerable<IEnumerable<object?>> Rows { get; set; } = Array.Empty<IEnumerable<object?>>();
        public int RowCount { get; set; }
    }   

}
