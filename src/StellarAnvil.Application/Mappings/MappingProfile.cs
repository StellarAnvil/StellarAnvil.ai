using AutoMapper;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Domain.Entities;

namespace StellarAnvil.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // TeamMember mappings
        CreateMap<TeamMember, TeamMemberDto>();
        CreateMap<CreateTeamMemberDto, TeamMember>();
        CreateMap<UpdateTeamMemberDto, TeamMember>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Task mappings
        CreateMap<Domain.Entities.Task, TaskDto>()
            .ForMember(dest => dest.AssigneeName, opt => opt.MapFrom(src => src.Assignee != null ? src.Assignee.Name : null))
            .ForMember(dest => dest.WorkflowName, opt => opt.MapFrom(src => src.Workflow.Name));
        CreateMap<CreateTaskDto, Domain.Entities.Task>();
        CreateMap<UpdateTaskDto, Domain.Entities.Task>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // MCP Configuration mappings
        CreateMap<McpConfiguration, McpConfigurationDto>();
        CreateMap<CreateMcpConfigurationDto, McpConfiguration>();
        CreateMap<UpdateMcpConfigurationDto, McpConfiguration>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}
