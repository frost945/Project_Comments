using Comments.Api.Middleware;
using Comments.Application.Interfaces;
using Comments.Application.Services;
using Comments.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Filters;


var builder = WebApplication.CreateBuilder();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()

      // Audit logs  (file only)
      .WriteTo.Logger(audit => audit
          .MinimumLevel.Information() // Audit logs are always Information level
          .Filter.ByIncludingOnly(Matching.WithProperty("AuditUser"))
          .WriteTo.Async(a => a.File(
              "logs/audit-user-.txt",
              rollingInterval: RollingInterval.Day,
              buffered: false)) //for production, to set buffered: true
                                //for dev, to set buffered: false, to display logs immediately
      )

    // Technical logs (console and file), excluding audit
    // Log level is taken from configuration file appsettings.json
    .WriteTo.Logger(tech => tech
        .Filter.ByExcluding(Matching.WithProperty("AuditUser"))
        .WriteTo.Async(a => a.File(
            "logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            buffered: true))
        .WriteTo.Console()
    )

    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDbContext<CommentsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddSwaggerGen();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ImageService>();
builder.Services.AddScoped<TextFileService>();

var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    headers.Append("X-Content-Type-Options", "nosniff");
    headers.Append("X-Frame-Options", "DENY");
    headers.Append("X-XSS-Protection", "1; mode=block");
    headers.Append("Content-Security-Policy",
          "default-src 'self'; " +                                  
          "script-src 'self' 'unsafe-inline'; " +                  
          "style-src 'self' 'unsafe-inline'; " +                   
          "img-src 'self' data:; " +                               
          "font-src 'self'; " +                                    
          "connect-src 'self' ; " +
          "frame-ancestors 'none'; " +                             
          "base-uri 'self'; " +
          "form-action 'self';"                                     
      );
    headers.Append("X-Download-Options", "noopen");
    headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    headers.Append("Cross-Origin-Opener-Policy", "same-origin");
    headers.Append("Cross-Origin-Resource-Policy", "same-origin");
    headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Project_Comments v1"));

    // In development, remove CSP to allow Swagger UI to function properly
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Remove("Content-Security-Policy");
        await next();
    });
}

app.UseDefaultFiles();

// Ensure upload directories exist
var textFilesPath = Path.Combine(
    builder.Environment.WebRootPath, "uploads", "textfiles");
Directory.CreateDirectory(textFilesPath);

var imagesPath = Path.Combine(
    builder.Environment.WebRootPath, "uploads", "images");
Directory.CreateDirectory(imagesPath);

app.UseStaticFiles();

// Static files for text files (with download settings)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "uploads", "textfiles")),
    RequestPath = "/uploads/textfiles",
    OnPrepareResponse = ctx =>
    {
        // For text files, set a download title
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads/textfiles") &&
            ctx.File.Name.EndsWith(".txt"))
        {
            var fileName = Path.GetFileName(ctx.File.Name);
            ctx.Context.Response.Headers.Append("Content-Disposition",
                $"attachment; filename=\"{fileName}\"");
            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=3600");
        }
    }
});

// Static files for images
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "uploads", "images")),
    RequestPath = "/uploads/images"
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
    Console.WriteLine("DB Connection: " + db.Database.CanConnect());

    // For development purposes only: reset database
    //  db.Database.EnsureDeleted();
    // db.Database.EnsureCreated();

    if (app.Environment.IsDevelopment())
        db.Database.Migrate();
}

app.UseRouting();

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();
