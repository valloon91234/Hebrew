// See https://aka.ms/new-console-template for more information

using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.Files;
using EmbedIO.WebApi;
using Swan.Logging;
using System.Diagnostics;
using System.Text;

AppHelper.QuickEditMode(false);
//Console.BufferHeight = Int16.MaxValue - 1;
//AppHelper.MoveWindow(AppHelper.GetConsoleWindow(), 24, 0, 1080, 280, true);
AppHelper.FixCulture();

TelegramClient.Init();

if (!Debugger.IsAttached)
{
    //ConsoleLogger.Instance.LogLevel = LogLevel.Fatal;
    Swan.Logging.Logger.UnregisterLogger<ConsoleLogger>();
}
string url = "http://*:80/";
using var server = CreateWebServer(url);
server.RunAsync();
Console.ReadKey(true);

/**
 * https://github.com/unosquare/embedio
 */
static WebServer CreateWebServer(string url)
{
    var server = new WebServer(o => o
            .WithUrlPrefix(url)
            .WithMode(HttpListenerMode.EmbedIO))
        .WithLocalSessionManager()
        .WithWebApi("/checkout", m => m.WithController<CheckoutController>())
        .WithStaticFolder("/assets", "www/assets", true, m => m
            .WithContentCaching(true)) // Add static files after other modules to avoid conflicts
        .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));
    server.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();
    server.HandleHttpException(async (context, exception) =>
    {
        context.Response.StatusCode = exception.StatusCode;
        switch (exception.StatusCode)
        {
            case 404:
                await context.SendStringAsync("404", "text/html", Encoding.UTF8);
                break;
            default:
                await HttpExceptionHandler.Default(context, exception);
                break;
        }
    });
    return server!;
}

