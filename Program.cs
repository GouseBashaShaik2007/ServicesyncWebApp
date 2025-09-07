// Program.cs — SPA + API (isolated /api), robust email send, debug endpoints

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;          // StatusCodes, SendFileAsync
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ServicesyncWebApp.Services;         // IEmailSender, SmtpEmailSender
using ServicesyncWebApp.Options;          // EmailOptions
using ServicesyncWebApp.Models;           // EmailRequest

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers(); // attribute-routed controllers (if any)
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// CORS (dev): allow cross-origin calls if your front-end runs elsewhere
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// No HTTPS redirect in this setup (avoid port mismatch warnings)
// app.UseHttpsRedirection();

app.UseRouting();
app.UseCors();

// Serve static files ONLY for non-/api paths
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), spa =>
{
    spa.UseDefaultFiles();   // serves wwwroot/index.html at /
    spa.UseStaticFiles();    // serves other assets in wwwroot
});

// -------- API ENDPOINTS --------

// Health checks
app.MapGet("/api/ping", () => Results.Text("ok"));
app.MapGet("/api/email/ping", () => Results.Text("email-ok"));

// Inspect loaded (non-secret) email config
app.MapGet("/api/email/config", (IOptions<EmailOptions> opts) =>
{
    var e = opts.Value;
    return Results.Ok(new { e.Host, e.Port, e.UseStartTls, e.UserName, From = e.FromEmail });
});

// Send email with debug-friendly error handling (?debug=1 to echo exception)
app.MapPost("/api/email/send", async (
    HttpContext http,
    EmailRequest req,
    IEmailSender sender,
    ILogger<Program> log) =>
{
    try
    {
        if (req is null ||
            string.IsNullOrWhiteSpace(req.To) ||
            string.IsNullOrWhiteSpace(req.Subject) ||
            string.IsNullOrWhiteSpace(req.Html))
            return Results.BadRequest("To, Subject, and Html are required.");

        await sender.SendAsync(req.To, req.Subject, req.Html, req.Text);
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Email send failed");
        var debug = http.Request.Query.ContainsKey("debug");
        var message = debug ? ex.ToString() : "Email failed";
        return Results.Problem(message, statusCode: 500);
    }
});

// Route inventory (visit /__routes to see everything mapped)
app.MapGet("/__routes", (IEnumerable<EndpointDataSource> sources) =>
{
    var lines = new List<string>();
    foreach (var s in sources)
    foreach (var ep in s.Endpoints)
    {
        var route = (ep as RouteEndpoint)?.RoutePattern?.RawText ?? ep.DisplayName ?? "(unknown)";
        var methods = ep.Metadata.OfType<HttpMethodMetadata>().FirstOrDefault()?.HttpMethods ?? new[] { "ANY" };
        lines.Add($"{route} [{string.Join(",", methods)}]");
    }
    return Results.Text(string.Join("\n", lines));
});

// If you have other attribute-routed controllers, keep this:
app.MapControllers();

// SPA fallback outside UseWhen; DO NOT hijack /api/*
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Not found");
        return;
    }

    var indexPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
    if (File.Exists(indexPath))
    {
        await context.Response.SendFileAsync(indexPath);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("index.html not found");
    }
});

app.Run();
