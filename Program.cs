using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using StoredProcedureAPI.Repository;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Add services BEFORE Build()
builder.Services.AddControllers();

// Repository registrations
builder.Services.AddScoped<IExecutionLogRepository, ExecutionLogRepository>();
builder.Services.AddScoped<IDatasetRepository, DatasetRepository>();
// If you have an interface for ProcedureRepository, prefer using it; otherwise this is fine:
builder.Services.AddSingleton<ProcedureRepository>();

builder.Services.AddMemoryCache();
builder.Services.AddLogging();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Stored Procedure API",
        Version = "v1",
        Description = "API to explore SQL Server schemas, stored procedures, and parameters."
    });
});

var app = builder.Build();

app.UseMiddleware<StoredProcedureAPI.Middleware.ExceptionHandlingMiddleware>();
app.UseCors();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Stored Procedure API v1");
    c.RoutePrefix = string.Empty;
});

//app.UseHttpsRedirection();

app.MapControllers();

app.Run();
