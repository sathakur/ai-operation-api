using AIInventory.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(
        builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddScoped<AzureInventoryService>();
builder.Services.AddScoped<FunctionInventoryService>();
builder.Services.AddScoped<AzureAgentToolService>();
builder.Services.AddScoped<ChatPresentationBuilder>();
builder.Services.AddScoped<FoundryAgentService>();

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
    options.AddPolicy(
        "FrontendPolicy",
        policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        status = "ok",
        service = "AIInventory.Api"
    }));

app.Run();
