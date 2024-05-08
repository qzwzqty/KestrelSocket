using KestrelSocket.Tcp;
using TcpSample;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Host.AddKestrelTcp<MyPackage, MyPackageDecoder, MyPackageHandler>(12301);
// 可以添加多个
//builder.Host.AddIotAdapterTcp<MyBeginAndEndPackage, MyBeginAndEndDecoder, MyBeginAndEndHandler>(12302);
builder.Services.AddHostedService<TestConnectionHandlerJob>();

var app = builder.Build();

// Configure the HTTP request pipeline.

await app.RunAsync();

