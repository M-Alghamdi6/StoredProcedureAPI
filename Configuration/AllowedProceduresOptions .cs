namespace StoredProcedureAPI.Configuration
{
    public class AllowedProceduresOptions : Dictionary<string, string[]>
    {
        // Key: schema (lowercase)
        // Value: array of allowed procedure names (case sensitive as stored)
    }
}
