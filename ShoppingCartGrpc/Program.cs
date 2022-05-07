using DiscountGrpc.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShoppingCartGrpc.Data;
using ShoppingCartGrpc.Mapper;
using ShoppingCartGrpc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcConfigs:DiscountUrl"]);
});
builder.Services.AddScoped<DiscountService>();

builder.Services.AddDbContext<ShoppingCartContext>(options =>
{
    options.UseInMemoryDatabase("ShoppingCart");
});

builder.Services.AddAutoMapper(typeof(ShoppingCartProfile));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://localhost:7064";
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateAudience = false
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
var shoppingCartContext = services.GetRequiredService<ShoppingCartContext>();
ShoppingCartContextSeed.SeedAsync(shoppingCartContext);

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
app.MapGrpcService<ShoppingCartService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
