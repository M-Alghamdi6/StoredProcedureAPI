using AutoMapper;
using StoredProcedureAPI.DTOs;

namespace StoredProcedureAPI.Services
{
    public class MappingService : IMappingService
    {
        private readonly IMapper _mapper;

        public MappingService(IMapper mapper)
        {
            _mapper = mapper;
        }

        public TDestination Map<TDestination>(object source)
        {
            return _mapper.Map<TDestination>(source);
        }

        public JSONResponseDTO<TDestination> MapResponse<TSource, TDestination>(JSONResponseDTO<TSource> source)
        {
            return new JSONResponseDTO<TDestination>
            {
                StatusCode = source.StatusCode,
                Message = source.Message,
                Data = source.Data == null ? default : _mapper.Map<TDestination>(source.Data),
                Id = source.Id
            };
        }
    }
}
