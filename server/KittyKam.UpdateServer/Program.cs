using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.OpenApi; // FIX: Add the correct using directive for OpenApiInfo

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KittyKam Update Server API",
        Version = "v1",
        Description = "API for managing firmware updates"
    });
});

var app = builder.Build();

// Enable Swagger in all environments (not just Development)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "KittyKam Update Server API v1");
    options.RoutePrefix = string.Empty; // Serve Swagger UI at the app's root (http://localhost:5000/)
});

// Disable HTTPS redirection for easier local testing
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

app.Run();