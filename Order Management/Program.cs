using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Order_Management.Common;
using Order_Management.Data;
using Order_Management.Features.Orders;
using Order_Management.Features.Products;
using Order_Management.Logging;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

    builder.Services.Configure<ApiBehaviorOptions>(o =>
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            return new BadRequestObjectResult(new ErrorResponse(
                ctx.HttpContext.TraceIdentifier, "VALIDATION_ERROR", "Payload tidak valid.", errors));
        });

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddScoped<CreateOrderHandler>();
    builder.Services.AddScoped<UpdateStatusHandler>();
    builder.Services.AddScoped<CancelOrderHandler>();
    builder.Services.AddScoped<ListOrderHandler>();
    builder.Services.AddScoped<GetOrderHandler>();

    builder.Services.AddScoped<CreateProductHandler>();
    builder.Services.AddScoped<GetProductHandler>();
    builder.Services.AddScoped<ListProductsHandler>();
    builder.Services.AddScoped<DeleteProductHandler>();
    
    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db, sp.GetRequiredService<ILogger<Program>>());
    }
    
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.Run();
}
catch (Exception ex) when ( ex is not HostAbortedException)
{
    Log.Fatal(ex, "Aplikasi berhenti karena error tak terduga");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
