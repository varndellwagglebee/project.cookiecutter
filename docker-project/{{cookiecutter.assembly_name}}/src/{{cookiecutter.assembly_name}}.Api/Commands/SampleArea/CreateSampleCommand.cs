﻿{% if cookiecutter.include_audit =='yes'%}
using Audit.Core;
{% endif %}
using Hyperbee.Pipeline;
using Hyperbee.Pipeline.Commands;
using Hyperbee.Pipeline.Context;
using {{cookiecutter.assembly_name}}.Api.Commands.Infrastructure;
using {{cookiecutter.assembly_name}}.Api.Commands.Middleware;
using {{cookiecutter.assembly_name}}.Api.Identity;
using {{cookiecutter.assembly_name}}.Data.Abstractions.Services;
using {{cookiecutter.assembly_name}}.Data.Abstractions.Entity;
using {{cookiecutter.assembly_name}}.Data.Abstractions.Services.Models;
using Microsoft.Extensions.Logging;

namespace {{cookiecutter.assembly_name}}.Api.Commands.SampleArea;

public record CreateSample( string Name, string Description );

public interface ICreateSampleCommand : ICommandFunction<CreateSample, SampleDefinition>;

public class CreateSampleCommand : ServiceCommandFunction<CreateSample, SampleDefinition>, ICreateSampleCommand
{
    private readonly ISampleService _sampleService;

    public CreateSampleCommand(
        ISampleService sampleService,
        IPipelineContextFactory pipelineContextFactory,
        ILogger<CreateSampleCommand> logger ) :
        base( pipelineContextFactory, logger )
    {
        _sampleService = sampleService;
    }

    protected override FunctionAsync<CreateSample, SampleDefinition> CreatePipeline()
    {
        return PipelineFactory
            .Start<CreateSample>()
            .WithLogging()
            .PipeAsync( CreateSampleAsync )
            .CancelOnFailure( Validate<Sample> )
            .PipeAsync( InsertSampleAsync )
            .Build();
    }

    private async Task<Sample> CreateSampleAsync( IPipelineContext context, CreateSample sample )
    {

        return await Task.FromResult( new Sample
        {
            Name = sample.Name,
            Description = sample.Description,
            CreatedBy = "test user"
        } );
    }

    {%if cookiecutter.include_audit =='yes'%}
    {% include 'templates/audit/api_sample_create.cs' %} 
    {%else%}
    private async Task<SampleDefinition> InsertSampleAsync( IPipelineContext context, Sample sample )
    {
       
        sample.Id = await _sampleService.CreateSampleAsync( sample );

        return new SampleDefinition(
            sample.Id,
            sample.Name,
            sample.Description
        );
    } 
    {%endif%}
}
