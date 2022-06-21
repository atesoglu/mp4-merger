// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using mp4_merger;
using Serilog;

Log.Logger = new LoggerConfiguration().Enrich.FromLogContext().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

Host.CreateDefaultBuilder(args).ConfigureServices((hostContext, services) =>
{
    services
        .AddLogging(configure => configure.ClearProviders().AddSerilog())
        // .AddTransient<BraintreeWorker>()
        //
        .AddHostedService<HostedService>();
}).Build().Run();