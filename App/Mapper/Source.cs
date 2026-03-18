using Backup.App.Models.Config.Request;

namespace Backup.App.Mapper;

public class Source : AutoMapper.Profile
{
    public Source()
    {
        RegisterMaps();
    }

    public void RegisterMaps()
    {
        CreateMap<Models.Config.Source, Models.Config.Source>()
            .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember is not null));

        CreateMap<Request, Request>()
            .ForMember(dest => dest.Headers, opt => opt.Ignore())
            .AfterMap(
                (src, dest) =>
                {
                    foreach (var kvp in src.Headers)
                        dest.Headers[kvp.Key] = kvp.Value;
                }
            )
            .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember is not null));

        CreateMap<Query, Query>()
            .ForMember(dest => dest.Variables, opt => opt.Ignore())
            .ForMember(dest => dest.Features, opt => opt.Ignore())
            .ForMember(dest => dest.FieldToggles, opt => opt.Ignore())
            .AfterMap(
                (src, dest) =>
                {
                    src.Variables ??= [];
                    src.Features ??= [];
                    src.FieldToggles ??= [];

                    foreach (var kvp in src.Variables)
                        src.Variables[kvp.Key] = Coerce(kvp.Value);

                    dest.Variables ??= [];

                    foreach (var kvp in dest.Variables)
                        dest.Variables[kvp.Key] = Coerce(kvp.Value);

                    foreach (var kvp in src.Variables)
                        dest.Variables[kvp.Key] = kvp.Value;

                    foreach (var kvp in src.Features)
                        dest.Features[kvp.Key] = kvp.Value;

                    foreach (var kvp in src.FieldToggles)
                        dest.FieldToggles[kvp.Key] = kvp.Value;
                }
            )
            .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember is not null));
    }

    public static object? Coerce(object? o)
    {
        if (o is not string s)
            return o;

        if (bool.TryParse(s, out var b))
            return b;

        if (int.TryParse(s, out var i))
            return i;

        if (long.TryParse(s, out var l))
            return l;

        if (double.TryParse(s, out var d))
            return d;

        return s;
    }
}
