using Kwestie.Infrastructure;
using Kwestie.Application.Authentication.Register;
using Kwestie.Application.Authentication.Login;
using Kwestie.Application.Authentication.Refresh;
using Kwestie.Application.Authentication.Logout;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Kwestie");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Kwestie is required. Configure it with .NET User Secrets for local development.");
}
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRefreshTokens(builder.Configuration);
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<LoginUserHandler>();
builder.Services.AddScoped<RefreshSessionHandler>();
builder.Services.AddScoped<LogoutSessionHandler>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
