namespace StoredProcedureAPI.Models
{
    public class DatasetColumn
    {
        public int ColumnId { get; set; }
        public int DataSetId { get; set; }
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public int ColumnOrder { get; set; }
    }

}
