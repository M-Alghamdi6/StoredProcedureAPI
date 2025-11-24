using StoredProcedureAPI.Models;

namespace StoredProcedureAPI.Utilities
{
    public static class ParameterValidator
    {
        // Accepts parameter metadata and the provided value (object)
        // Returns (isValid, normalizedValue, errorMessage)
        public static (bool, object?, string?) Validate(ProcedureParameter metadata, object? rawValue)
        {
            // If parameter is nullable and value is null/empty => OK
            if (rawValue == null || (rawValue is string s && string.IsNullOrWhiteSpace(s)))
            {
                if (metadata.IsNullable) return (true, null, null);
                // Not nullable => invalid
                return (false, null, $"Parameter {metadata.ParameterName} cannot be null");
            }

            var sqlType = metadata.DataType?.ToLowerInvariant() ?? "";
            // rawValue is guaranteed non-null here (we returned if it was null/empty)
            var raw = rawValue!.ToString() ?? string.Empty;

            try
            {
                switch (sqlType)
                {
                    case "int":
                    case "tinyint":
                    case "smallint":
                        if (int.TryParse(raw, out var i)) return (true, i, null);
                        return (false, null, $"Parameter {metadata.ParameterName} expects an integer.");

                    case "bigint":
                        if (long.TryParse(raw, out var l)) return (true, l, null);
                        return (false, null, $"Parameter {metadata.ParameterName} expects a bigint.");

                    case "decimal":
                    case "numeric":
                    case "money":
                    case "smallmoney":
                        if (decimal.TryParse(raw, out var dec)) return (true, dec, null);
                        return (false, null, $"Parameter {metadata.ParameterName} expects a decimal.");

                    case "float":
                    case "real":
                        if (double.TryParse(raw, out var dbl)) return (true, dbl, null);
                        return (false, null, $"Parameter {metadata.ParameterName} expects a floating point number.");

                    case "bit":
                        if (bool.TryParse(raw, out var b)) return (true, b, null);
                        // also accept 0/1
                        if (raw == "0") return (true, false, null);
                        if (raw == "1") return (true, true, null);
                        return (false, null, $"Parameter {metadata.ParameterName} expects a boolean (true/false).");

                    case "date":
                    case "datetime":
                    case "datetime2":
                    case "smalldatetime":
                    case "datetimeoffset":
                        if (DateTime.TryParse(raw, out var dt)) return (true, dt, null);
                        return (false, null, $"Parameter {metadata.ParameterName} expects a date/time.");

                    case "uniqueidentifier":
                        if (Guid.TryParse(raw, out var g)) return (true, g, null);
                        return (false, null, $"Parameter {metadata.ParameterName} expects a GUID.");

                    default:
                        // For varchar/nvarchar/text and others, enforce MaxLength if provided
                        var str = raw;
                        if (metadata.MaxLength > 0 && metadata.MaxLength < str.Length)
                            return (false, null, $"Parameter {metadata.ParameterName} exceeds max length {metadata.MaxLength}.");
                        return (true, rawValue, null);
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"Validation failed for {metadata.ParameterName}: {ex.Message}");
            }
        }
    }
}
