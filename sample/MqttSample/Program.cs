using KestrelSocket.Mqtt;
using MqttSample;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Host.AddKestrelMqtt<MyMqttHandler>(9999);

var app = builder.Build();

// Configure the HTTP request pipeline.

await app.RunAsync();
