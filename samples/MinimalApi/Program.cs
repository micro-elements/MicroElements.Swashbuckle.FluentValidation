using FluentValidation;
using FluentValidation.AspNetCore;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Asp.Net stuff
services.AddControllers();
services.AddEndpointsApiExplorer();

// Add Swagger
services.AddSwaggerGen();

// Add FV
services.AddFluentValidationAutoValidation();
services.AddFluentValidationClientsideAdapters();

// Add FV validators
services.AddValidatorsFromAssemblyContaining<Program>();

// Add FV Rules to swagger
services.AddFluentValidationRulesToSwagger();

var app = builder.Build();

// Use Swagger
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();