using IniParser;
using System.Diagnostics;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

internal class TelegramClient
{
    public static readonly string INI_FILENAME = "config.ini";


    static TelegramBotClient? Client;
    static User? Me { get; set; }
    static string[]? adminArray;
    static string[]? listenArray;
    static Logger? logger;

    public static void Init()
    {
        var iniDataParser = new FileIniDataParser();
        if (!System.IO.File.Exists(INI_FILENAME))
        {
            System.IO.File.WriteAllText(INI_FILENAME, "[TELEGRAM]");
        }
        var iniData = iniDataParser.ReadFile(INI_FILENAME);
        var token = iniData["TELEGRAM"]["TOKEN"];
        var proxy = iniData["TELEGRAM"]["PROXY"];
        if (!string.IsNullOrWhiteSpace(proxy))
        {
            var webProxy = new WebProxy
            {
                Address = new Uri(proxy)
            };
            //proxy.Credentials = new NetworkCredential(); //Used to set Proxy logins. 
            var handler = new HttpClientHandler
            {
                Proxy = webProxy
            };
            var httpClient = new HttpClient(handler);
            Client = new TelegramBotClient(token, httpClient);
        }
        else
        {
            Client = new TelegramBotClient(token);
        }
        using var cts = new CancellationTokenSource();
        // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
        };
        Client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );
        Me = Client.GetMeAsync().Result;
        adminArray = iniData["TELEGRAM"]["ADMIN"]?.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        listenArray = iniData["TELEGRAM"]["LISTEN"]?.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        logger = new Logger($"{DateTime.UtcNow:yyyy-MM-dd}", "telegram_log");
        logger.WriteLine($"Telegram connected: username = {Me.Username}");
        logger.WriteLine($"adminArray = {(adminArray == null ? "Null" : string.Join(",", adminArray))}");
    }

    static bool AddListen(string chatId)
    {
        var iniDataParser = new FileIniDataParser();
        var iniData = iniDataParser.ReadFile(INI_FILENAME);
        listenArray = iniData["TELEGRAM"]["LISTEN"]?.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (listenArray != null && listenArray.Contains(chatId)) return false;
        var listenArrayString = iniData["TELEGRAM"]["LISTEN"];
        if (string.IsNullOrWhiteSpace(listenArrayString))
            listenArrayString = chatId;
        else
            listenArrayString = listenArrayString.Trim() + "," + chatId;
        iniData["TELEGRAM"]["LISTEN"] = listenArrayString;
        iniDataParser.WriteFile(INI_FILENAME, iniData);
        listenArray = listenArrayString.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        return true;
    }

    static bool RemoveListen(string chatId)
    {
        var iniDataParser = new FileIniDataParser();
        var iniData = iniDataParser.ReadFile(INI_FILENAME);
        listenArray = iniData["TELEGRAM"]["LISTEN"]?.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (listenArray == null || !listenArray.Contains(chatId)) return false;
        var listenList = listenArray.ToList();
        listenList.Remove(chatId);
        var listenArrayString = string.Join(",", listenList);
        iniData["TELEGRAM"]["LISTEN"] = listenArrayString;
        iniDataParser.WriteFile(INI_FILENAME, iniData);
        listenArray = listenArrayString.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        return true;
    }

    static readonly Dictionary<string, string> LastCommand = new();

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            long chatId;
            int messageId;
            string chatUsername;
            string senderUsername;
            string receivedMessageText;
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text && update.Message!.Chat.Type == ChatType.Private)
            {
                // Only process text messages
                chatId = update.Message.Chat.Id;
                messageId = update.Message.MessageId;
                chatUsername = update.Message.Chat.Username!;
                senderUsername = update.Message.From!.Username!;
                receivedMessageText = update.Message.Text!;
                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  \"{receivedMessageText}\" from {senderUsername}. chatId = {chatId}, messageId = {messageId}", ConsoleColor.DarkGray);
            }
            else if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text && (update.Message!.Chat.Type == ChatType.Group || update.Message!.Chat.Type == ChatType.Supergroup))
            {
                chatId = update.Message.Chat.Id;
                messageId = update.Message.MessageId;
                chatUsername = update.Message.Chat.Username!;
                senderUsername = update.Message.From!.Username!;
                receivedMessageText = update.Message.Text!;
                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  \"{receivedMessageText}\" from {senderUsername}. chatId = {chatId}, messageId = {messageId}", ConsoleColor.DarkGray);
                if (receivedMessageText[0] == '/' && receivedMessageText.EndsWith($"@{Me!.Username}"))
                {
                    var command = receivedMessageText[..^$"@{Me!.Username}".Length];
                    bool isAdmin = adminArray != null && adminArray.Contains(senderUsername!);
                    switch (command)
                    {
                        case $"/start":
                            if (isAdmin)
                            {
                                AddListen(chatId.ToString());
                                string replyMessageText = "Started.";
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            }
                            break;
                        case $"/stop":
                            if (isAdmin)
                            {
                                RemoveListen(chatId.ToString());
                                string replyMessageText = "Stopped.";
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            }
                            break;
                    }
                }

                return;
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                chatId = update.CallbackQuery!.Message!.Chat.Id;
                senderUsername = update.CallbackQuery.From.Username!;
                receivedMessageText = update.CallbackQuery.Data!;
                await botClient.AnswerCallbackQueryAsync(callbackQueryId: update.CallbackQuery!.Id, cancellationToken: cancellationToken);
            }
            else
                return;
            {
                bool isAdmin = adminArray != null && adminArray.Contains(senderUsername!);
                if (receivedMessageText[0] == '/')
                {
                    var command = receivedMessageText;
                    switch (command)
                    {
                        case "/start":
                            if (isAdmin)
                            {
                                AddListen(chatId.ToString());
                                string replyMessageText = "Started.";
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            }
                            break;
                        case "/stop":
                            if (isAdmin)
                            {
                                RemoveListen(chatId.ToString());
                                string replyMessageText = "Stopped.";
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            }
                            break;
                        default:
                            {
                                string replyMessageText = $"Unknown command: {command}";
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                logger!.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            }
                            LastCommand.Remove(senderUsername);
                            break;
                    }
                }
                else if (LastCommand.ContainsKey(senderUsername!))
                {
                    if (receivedMessageText == "exit" || receivedMessageText == "/exit")
                        LastCommand.Remove(senderUsername!);
                    else
                        switch (LastCommand[senderUsername!])
                        {
                            default:
                                {
                                    logger!.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  Unknown error", ConsoleColor.Red);
                                }
                                break;
                        }
                }
            }
        }
        catch (Exception ex)
        {
            logger!.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red, false);
            logger!.WriteFile($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {ex}");
        }
    }

    static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };
        if (logger != null) logger.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    public static void SendMessageToListenGroup(string text, ParseMode? parseMode = default, IReplyMarkup? replyMarkup = default)
    {
        if (Client == null || listenArray == null) return;
        try
        {
            int count = 0;
            foreach (var chat in listenArray)
            {
                if (string.IsNullOrWhiteSpace(chat)) continue;
                var result = Client.SendTextMessageAsync(chatId: chat, text: text, disableWebPagePreview: true, parseMode: parseMode, replyMarkup: replyMarkup).Result;
                count++;
            }
            logger!.WriteFile($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  Message sent to {count} chats: {text}");
        }
        catch (Exception ex)
        {
            logger!.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red, false);
            logger!.WriteFile($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {ex}");
            logger!.WriteFile($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {text}");
        }
    }

    public static void SendPhotoToListenGroup(Stream fileStream, string? caption = null, ParseMode? parseMode = default, IReplyMarkup? replyMarkup = default)
    {
        if (Client == null || listenArray == null) return;
        try
        {
            int count = 0;
            InputOnlineFile inputOnlineFile = new(fileStream);
            foreach (var chat in listenArray)
            {
                if (string.IsNullOrWhiteSpace(chat)) continue;
                var result = Client.SendPhotoAsync(chatId: chat, photo: inputOnlineFile, caption: caption, parseMode: parseMode, replyMarkup: replyMarkup).Result;
                count++;
            }
            logger!.WriteFile($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  Photo sent to {count} caption: {caption}");
        }
        catch (Exception ex)
        {
            logger!.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red, false);
            logger!.WriteFile($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {ex}");
            logger!.WriteFile($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {caption}");
        }
    }

}
