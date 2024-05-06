using EasyUploader;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

//use first command-line argument as app url
if (args.Length > 0)
    builder.WebHost.UseUrls(args[0]);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IFileHandler, FileHandler>();
builder.Services.AddSingleton<IServerPlayerManager, ServerPlayerManager>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 30 * 1024 * 1024; //set request max size to ~30mb for posting server build in chunks
});

var app = builder.Build();
app.UseAuthorization();
app.MapControllers();
app.Run();
