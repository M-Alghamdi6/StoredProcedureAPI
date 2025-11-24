namespace StoredProcedureAPI.Models
{
    public class ProcedureParameter
    {
        // NOTE: sys.parameters.name includes the '@' prefix.
        // We'll normalize to include '@' in validation.
        public required string ParameterName { get; set; }
        public required string DataType { get; set; }
        public int MaxLength { get; set; }
        public bool IsOutput { get; set; }
        public bool IsNullable { get; set; }
    }
}

