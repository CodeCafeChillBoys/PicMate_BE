using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PhoneGrapher.Infrastructure;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Api.Hubs;
using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập JWT token. Ví dụ: eyJhbGciOiJIUzI1NiIs..."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontend", policy => {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var allowedOrigins = new List<string>
        {
            "http://localhost:5173", 
            "https://localhost:5173", 
            "http://localhost:8081",
            "https://localhost:8081",
            "https://pic-mate-fe.vercel.app",
            "https://pic-mate-beryl.vercel.app"
        };

        var frontendUrl = builder.Configuration["FrontendUrl"];
        if (!string.IsNullOrWhiteSpace(frontendUrl))
        {
            var cleanUrl = frontendUrl.Trim().TrimEnd('/');
            if (!allowedOrigins.Contains(cleanUrl))
            {
                allowedOrigins.Add(cleanUrl);
            }
        }

        foreach (var origin in configuredOrigins)
        {
            var cleanUrl = origin?.Trim()?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(cleanUrl) && !allowedOrigins.Contains(cleanUrl))
            {
                allowedOrigins.Add(cleanUrl);
            }
        }

        policy.WithOrigins(allowedOrigins.ToArray())
              .SetIsOriginAllowed(origin => 
                  origin.EndsWith(".vercel.app") || 
                  origin.StartsWith("http://localhost:") || 
                  origin.StartsWith("https://localhost:") ||
                  origin.StartsWith("http://127.0.0.1:") ||
                  origin.StartsWith("https://127.0.0.1:"))
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PhoneGrapherDbContext>();
    dbContext.Database.Migrate();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status500InternalServerError ? "Unexpected error" : exception?.Message,
            Detail = app.Environment.IsDevelopment() ? exception?.ToString() : null
        });
    });
});


app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PhoneGrapher API v1");
    c.RoutePrefix = "swagger";
});

app.UseStaticFiles();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger/index.html"));
app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Run();
