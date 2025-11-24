using StoredProcedureAPI.DTOs;

namespace StoredProcedureAPI.Services
{
    public interface IMappingService
    {
        TDestination Map<TDestination>(object source);

        StoredProcedureAPI.DTOs.JSONResponseDTO<TDestination> MapResponse<TSource, TDestination>(StoredProcedureAPI.DTOs.JSONResponseDTO<TSource> source);
    }
}
