using StoredProcedureAPI.Repository;

namespace StoredProcedureAPI.Models
{
    public class Dataset
    {
        public int DataSetId { get; set; }
        public string DataSetTitle { get; set; } = "";
        public string? Description { get; set; }

        public int SourceType { get; set; }
        public int? BuilderId { get; set; }
        public int? InlineId { get; set; }
        public int? ProcedureExecutionId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }

        public string? SourceName { get; set; }
        public List<DatasetColumn> Columns { get; set; } = new();
    }
}
