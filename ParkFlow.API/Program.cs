using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var optionsBuilder = new DbContextOptionsBuilder<ParkFlow.Persistence.AppDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using (var context = new ParkFlow.Persistence.AppDbContext(optionsBuilder.Options))
{
    var vehicles = context.Set<Vehicle>().ToList();
    Console.WriteLine($"[DIAGNOSTIC] Total vehicles in DB: {vehicles.Count}");
    foreach (var v in vehicles)
    {
        Console.WriteLine($"[DIAGNOSTIC] - Id: {v.Id}, OwnerId: {v.OwnerId}, Plate: {v.PlateNumber}, Brand: {v.Brand}, IsPrimary: {v.IsPrimary}");
    }
}
