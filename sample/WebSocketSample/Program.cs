using KestrelSocket.WebSocket;
using WebSocketSample;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Host.AddKestrelWebSocket<MyPackage, MyPackageDecoder, MyPackageHandler>();
// 可以添加多个
//builder.Host.AddKestrelWebSocket<MyPackage2, MyPackageDecoder2, MyPackageHandler2>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseWebSockets();
app.UseKestrelWebSocket<MyPackage>(patterns: new string[] { "/ws" });
// 可以添加多个
//app.UseKestrelWebSocket<MyPackage2>(patterns: new string[] { "/ws2" });

await app.RunAsync();
