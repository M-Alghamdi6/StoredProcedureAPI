using System.Net;

namespace StoredProcedureAPI.DTOs
{
    public class JSONResponseDTO<T>
    {
        public HttpStatusCode StatusCode { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public int? Id { get; set; }
    }
}
