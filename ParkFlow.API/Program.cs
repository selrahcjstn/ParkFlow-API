using ParkFlow.Persistence;
using ParkFlow.Application;
using ParkFlow.Infrastructure;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Dependency Injections:
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddControllers();

// SwaggerGen:
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
