using AutoMapper;

namespace Backup.App.Mapper;

public class Mapper
{
    public static IMapper Setup()
    {
        MapperConfiguration config = new(cfg =>
        {
            cfg.AddProfile<Posts>();
            cfg.AddProfile<Source>();
        });

        return new AutoMapper.Mapper(config);
    }
}
