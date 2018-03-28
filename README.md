# MicroElements.Swashbuckle.FluentValidation
Use FluentValidation rules instead ComponentModel attributes to define swagger schema.

Note: For WebApi see: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation.WebApi

## Latest Builds, Packages
[![NuGet](https://img.shields.io/nuget/v/MicroElements.Swashbuckle.FluentValidation.svg)](https://www.nuget.org/packages/MicroElements.Swashbuckle.FluentValidation/)
[![Travis](https://img.shields.io/travis/micro-elements/MicroElements.Swashbuckle.FluentValidation/master.svg?label=travis%20build)](https://travis-ci.org/micro-elements/MicroElements.Swashbuckle.FluentValidation)

## Usage

### 1. Reference packages in your web project:
```xml
    <PackageReference Include="FluentValidation.AspNetCore" Version="7.5.2" />
    <PackageReference Include="MicroElements.Swashbuckle.FluentValidation" Version="0.2.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="2.3.0" />
```

### 2. Change Startup.cs

```csharp
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                // Adds fluent validators to Asp.net
                .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<CustomerValidator>());

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "My API", Version = "v1" });
                // Adds fluent validation rules to swagger
                c.AddFluentValidationRules();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app
                .UseMvc()
                // Adds swagger
                .UseSwagger();

            // Adds swagger UI
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            });
        }
```

## Sample application
See sample project: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/tree/master/src/SampleWebApi

## Credits

Initial version of this project was based on
[Mujahid Daud Khan](https://stackoverflow.com/users/1735196/mujahid-daud-khan) answer on StackOwerflow:
https://stackoverflow.com/questions/44638195/fluent-validation-with-swagger-in-asp-net-core/49477995#49477995
