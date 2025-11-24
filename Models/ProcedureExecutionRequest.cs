namespace StoredProcedureAPI.Models
{
    public class ProcedureExecutionRequest
    {
        public Dictionary<string, object?> Parameters { get; set; } = new();
        public bool? UseCache { get; set; }

        // New
        public bool? SaveToFile { get; set; }
        public string? DatasetName { get; set; }
    }
}
