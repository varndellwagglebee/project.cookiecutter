
public class Startup : IStartupRegistry
{
    public IConfiguration Configuration { get; }

    public Startup( IConfiguration configuration )
    {
        Configuration = configuration;
    }

    public void ConfigureContainer( ServiceRegistry services )
    {
        IdentityModelEventSource.ShowPII = true; // show pii info in logs for debugging openid

        // auto-registrations by convention

        services.Scan( _ =>
        {
            _.TheCallingAssembly();
            _.WithDefaultConventions();
        } );

        // configure services
        services.AddCors( c => c.AddPolicy( "CorsAllowAll", build =>
        {
            build.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        } ) );

        services.AddHttpContextAccessor();

        services.AddControllers()
            .AddJsonOptions( x =>
            {
                x.JsonSerializerOptions.Converters.Add( new JsonStringEnumConverter() );
                x.JsonSerializerOptions.Converters.Add( new Infrastructure.JsonBoolConverter() );
                x.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                x.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            } );

        services.AddOptions<JsonOptions>().Configure( x =>
        {
            x.SerializerOptions.Converters.Add( new JsonStringEnumConverter() );
            x.SerializerOptions.Converters.Add( new Infrastructure.JsonBoolConverter() );
            x.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            x.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            x.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        } );

        {% if cookiecutter.database == "PostgreSql" %}
        services.AddHealthChecks()
            .AddNpgSql( Configuration["{{cookiecutter.database}}:ConnectionString"]! );
        {% elif cookiecutter.database == "MongoDb" %}
        services.AddHealthChecks()
            .AddMongoDb(
                sp => new MongoClient( Configuration["MongoDb:ConnectionString"] ),
                name: "MongoDb Health",
                failureStatus: HealthStatus.Degraded
            );
        {% endif %}
       
        services.AddApiVersioning( options =>
        {
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.DefaultApiVersion = ApiVersion.Default;
            //Add X-Version in Header
            options.ApiVersionReader = new HeaderApiVersionReader( "X-Version" );
        } );


        // swagger
        services.AddSwagger( Configuration );

        services.AddDataProtection();
       
        {% if cookiecutter.include_oauth == "yes" %}
        // security
        services.AddAuthentication( options => //BF review hyperbee AddSecurity implementation
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        } )
        .AddJwtBearer( options =>
        {
            options.Authority = $"https://{Configuration["OAuth:Domain"]}/"; // Issuer
            options.Audience = Configuration["OAuth:Audience"];
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = ClaimTypes.NameIdentifier
            };
        } );

        services.AddAuthorization();
        {% endif %}

        services.AddPipeline( ( factoryServices, rootProvider ) =>
        {
            factoryServices.ProxyService<Api.Identity.IPrincipalProvider>( rootProvider );
            factoryServices.ProxyService<IValidatorProvider>( rootProvider );
        } );

        // include implementation registries
        services.IncludeStartupRegistry<Api.Startup>( Configuration );
    }

    public void Configure( IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime applicationLifetime, ILoggerFactory logFactory )
    {
        ContainerDiagnostics( app, env );

        if ( env.IsDevelopment() )
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseSerilogRequestLogging(); // must be called before UseEndpoints

        app.UseRouting();
        app.UseCors( c => // must be called before UseResponseCaching
        {
            c.AllowAnyOrigin();
            c.AllowAnyMethod();
            c.AllowAnyHeader();
        } );

        app.UseResponseCaching();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseHttpsRedirection();

        app.UseUncaughtExceptionHandler( c => // important: register middleware before endpoints
        {
            c.IncludeExceptionDetails = Configuration.GetValue( "HttpServices:ForwardExceptions", false );
            c.LogException = true;
        } );

        app.UseRequestLocalization( new RequestLocalizationOptions()
        {
            DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture( "en-US" ),
            SupportedCultures = [new CultureInfo( "en-US" )]
        } );

        app.UseEndpoints( c =>
        {
            c.MapControllers();
            c.MapHealthChecks( Infrastructure.HealthChecksFilter.HealthCheckEndpoint );
        } );    // Must follow call to UseRouting()

        // Swagger
        app.UseSwagger();

        if ( env.IsDevelopment() || env.IsStaging() )
        {
            app.UseSwaggerUI( c =>
            {
                c.RoutePrefix = string.Empty; // serve the Swagger UI at the app root (http://localhost:<port>/)
                c.SwaggerEndpoint( "/swagger/v1/swagger.json", "{{cookiecutter.assembly_name}} API V1" );
             
                {% if cookiecutter.include_oauth == "yes" %}
                c.OAuthAppName( Configuration["Api:AppName"] );
                c.OAuthScopeSeparator( " " );
                c.OAuthUsePkce();
                {% endif %}
             
                if ( !env.IsDevelopment() ) return;
             
                {% if cookiecutter.include_oauth == "yes" %}
                // preset id and secret in dev
                c.OAuthClientId( Configuration["OAuth:Swagger:ClientId"] );
                c.OAuthClientSecret( Configuration["OAuth:Swagger:ClientSecret"] );
                {% endif %}
            } );
        }
    }

    private static void ContainerDiagnostics( IApplicationBuilder app, IHostEnvironment env )
    {
#if CONTAINER_DIAGNOSTICS
        if ( !env.IsDevelopment() )
            return;

        var container = (IContainer) app.ApplicationServices;
        Console.WriteLine( container.WhatDidIScan() );
        Console.WriteLine( container.WhatDoIHave() );
#endif
    }
   
}