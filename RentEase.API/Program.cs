using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PropertyLeasing.API.Data;
using PropertyLeasing.API.Hubs;
using PropertyLeasing.API.Models;
using PropertyLeasing.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PropertyLeasingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<AppIdentityDbContext>()
    .AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/maintenance"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddScoped<JwtService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Property Leasing API",
        Version = "v1",
        Description = "IT8118 Advanced Programming - Brief B"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token here."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await EnsureDevelopmentDatabasesAsync(app.Services, app.Environment);
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Property Leasing API v1"));

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MaintenanceHub>("/hubs/maintenance");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var db = services.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.MigrateAsync();
    }
    catch { }

    try
    {
        var db = services.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Document' AND COLUMN_NAME = 'Status')
            BEGIN
                ALTER TABLE [Document] ADD [Status] NVARCHAR(50) NOT NULL
                    CONSTRAINT [DF_Document_Status] DEFAULT 'Submitted' WITH VALUES;
            END;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Document' AND COLUMN_NAME = 'RejectionReason')
                ALTER TABLE [Document] ADD [RejectionReason] NVARCHAR(500) NULL;
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260519120000_AddDocumentReviewStatus')
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260519120000_AddDocumentReviewStatus', N'9.0.0');
        ");
    }
    catch { }

    try
    {
        var db = services.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Property' AND COLUMN_NAME = 'TotalSizeSqm')
                ALTER TABLE [Property] ADD [TotalSizeSqm] FLOAT NULL;
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260523120000_AddPropertyTotalSizeSqm')
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260523120000_AddPropertyTotalSizeSqm', N'9.0.0');
        ");
    }
    catch { }

    try
    {
        var db = services.GetRequiredService<PropertyLeasingDbContext>();
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Property' AND COLUMN_NAME = 'Amenities')
                ALTER TABLE [Property] ADD [Amenities] NVARCHAR(250) NULL;
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260525120000_AddPropertyAmenities')
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260525120000_AddPropertyAmenities', N'9.0.0');
        ");
    }
    catch { }

    try
    {
        await ContextSeed.SeedRolesAndUsersAsync(services);
    }
    catch { }
}

app.Run();

static async Task EnsureDevelopmentDatabasesAsync(IServiceProvider services, IWebHostEnvironment environment)
{
    using var scope = services.CreateScope();
    var scopedServices = scope.ServiceProvider;

    var identityDb = scopedServices.GetRequiredService<AppIdentityDbContext>();
    await identityDb.Database.EnsureCreatedAsync();

    var appDb = scopedServices.GetRequiredService<PropertyLeasingDbContext>();

    var schemaPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "SQL", "01_PropertyLeasingDB_Schema.sql"));
    var seedPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "SQL", "02_PropertyLeasingDB_SeedData.sql"));

    if (!File.Exists(schemaPath) || !File.Exists(seedPath))
    {
        return;
    }

    var connectionString = appDb.Database.GetConnectionString();
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return;
    }

    var appConnectionBuilder = new SqlConnectionStringBuilder(connectionString);
    var databaseName = appConnectionBuilder.InitialCatalog;
    if (string.IsNullOrWhiteSpace(databaseName))
    {
        return;
    }

    var masterConnectionBuilder = new SqlConnectionStringBuilder(connectionString)
    {
        InitialCatalog = "master"
    };

    await using (var masterConnection = new SqlConnection(masterConnectionBuilder.ConnectionString))
    {
        await masterConnection.OpenAsync();

        await using var existsCommand = new SqlCommand(
            "SELECT COUNT(1) FROM sys.databases WHERE name = @name",
            masterConnection);
        existsCommand.Parameters.AddWithValue("@name", databaseName);

        var databaseExists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0;
        if (!databaseExists)
        {
            await using var createCommand = new SqlCommand(
                $"CREATE DATABASE [{databaseName}]",
                masterConnection);
            await createCommand.ExecuteNonQueryAsync();
        }
    }

    await using var connection = new SqlConnection(appConnectionBuilder.ConnectionString);
    await connection.OpenAsync();

    await using (var hasTableCommand = new SqlCommand(
        "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Property'",
        connection))
    {
        var hasPropertyTable = Convert.ToInt32(await hasTableCommand.ExecuteScalarAsync()) > 0;
        if (hasPropertyTable)
        {
            return;
        }
    }

    var scripts = new[]
    {
        RemoveDatabaseBootstrapStatements(await File.ReadAllTextAsync(schemaPath), databaseName),
        await File.ReadAllTextAsync(seedPath)
    };

    foreach (var scriptText in scripts)
    {
        foreach (var batch in SplitSqlBatches(scriptText))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var command = new SqlCommand(batch, connection)
            {
                CommandTimeout = 120
            };
            await command.ExecuteNonQueryAsync();
        }
    }
}

static string RemoveDatabaseBootstrapStatements(string script, string databaseName)
{
    var lines = script.Replace($@"USE {databaseName};", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("USE master;", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Split(Environment.NewLine);

    var filteredLines = new List<string>();
    var skipDropBlock = false;

    foreach (var rawLine in lines)
    {
        var line = rawLine.Trim();

        if (line.StartsWith("IF EXISTS (SELECT name FROM sys.databases", StringComparison.OrdinalIgnoreCase))
        {
            skipDropBlock = true;
            continue;
        }

        if (skipDropBlock)
        {
            if (line.StartsWith("DROP DATABASE", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("GO", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(line))
            {
                if (line.Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    skipDropBlock = false;
                }
                continue;
            }

            skipDropBlock = false;
        }

        if (line.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (line.Equals("GO", StringComparison.OrdinalIgnoreCase) &&
            (filteredLines.Count == 0 || string.IsNullOrWhiteSpace(filteredLines[^1])))
        {
            continue;
        }

        filteredLines.Add(rawLine);
    }

    return string.Join(Environment.NewLine, filteredLines);
}

static IEnumerable<string> SplitSqlBatches(string script)
{
    using var reader = new StringReader(script);
    var current = new StringBuilder();
    string? line;

    while ((line = reader.ReadLine()) is not null)
    {
        if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
        {
            yield return current.ToString();
            current.Clear();
            continue;
        }

        current.AppendLine(line);
    }

    if (current.Length > 0)
    {
        yield return current.ToString();
    }
}
