using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using DotNetEnv;

class Program
{
    private static ITelegramBotClient _botClient;
    // private static List<string> _members_throw = new List<string> { "@Kefirolab", "@xelanko", "@Forterat", "@Денис"};
    // private static List<string> _members_clean = new List<string> { "@Денис", "@Kefirolab", "@menakata", "@xelanko", "@Forterat"};
    private static List<string> _members_throw = new List<string> { "@xelanko", "@Lonex17KO" };
    private static List<string> _members_clean = new List<string> { "@xelanko", "@Lonex17KO" };
    private static int _currentIndexThrow = 0;
    private static int _currentIndexClean = 0;

    static async Task Main()
    {
        Env.Load();
        _botClient = new TelegramBotClient(Environment.GetEnvironmentVariable("BOT_TOKEN"));
        using var cts = new CancellationTokenSource();

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            cancellationToken: cts.Token
        );

        var me = await _botClient.GetMeAsync();

        // Запускаємо нагадування
        Task.Run(() => StartReminders(cts.Token));

        await Task.Delay(-1);
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message.Text != null)
        {
            var messageText = update.Message.Text.ToLower();
            var chatId = update.Message.Chat.Id;
            string taskType = string.Empty;

            // Вчитання повідомлень зі слешом
            if (messageText == "/start")
            {
                await botClient.SendTextMessageAsync(chatId,
                    "Привіт! Я бот-нагадувач чергування. Я буду повідомляти, хто черговий, і чекати підтвердження.",
                    cancellationToken: cancellationToken);
                var nextUser = _members_throw[_currentIndexThrow];
                await _botClient.SendTextMessageAsync(chatId, $"{nextUser}, привіт, зараз твоя черга викидати сміття!",
                    parseMode: ParseMode.Markdown);
            }
            else if (messageText == "/help")
                await botClient.SendTextMessageAsync(chatId,
                    "Якщо ти викинув сміття, то не забудь написати в чат \"/викинув\"(без лапок), а якщо поприбирав - \"/поприбирав\", бо інакше черга не перейде до іншого.\nДля зміни порядку чергування використай \"/change throw(або clean) <перелік учасників> <індекс чергового>\"",
                    cancellationToken: cancellationToken);
            else if (messageText == "/викинув" &&
                     update.Message.From.Username == _members_throw[_currentIndexThrow].TrimStart('@'))
                taskType = "throw";
            else if (messageText == "/поприбирав" &&
                     update.Message.From.Username == _members_throw[_currentIndexThrow].TrimStart('@'))
                taskType = "clean";
            else if (messageText.StartsWith("/change"))
            {
                string[] splittedText = messageText.Split(" ");
                string lineType = splittedText[1];
                string[] newMembers = splittedText[1..(splittedText.Length - 1)];

                // дві перевірки коректності індексу 
                if (!int.TryParse(splittedText[^1], out int newIndex))
                {
                    await botClient.SendTextMessageAsync(chatId,
                        "Не вдалося прочитати індекс чергового. Перевір шаблон вводу команди.",
                        cancellationToken: cancellationToken);
                    return;
                }
                if (newIndex > newMembers.Length - 1 || newIndex < 0)
                { 
                    await botClient.SendTextMessageAsync(chatId,
                        "Індекс виходить за рамки списку чергових.",
                        cancellationToken: cancellationToken);
                    return; 
                }
                if (lineType == "throw")
                {
                    _members_throw = new List<string>(newMembers);
                    _currentIndexThrow = newIndex;
                }
                else if (lineType == "clean")
                {
                    _members_clean = new List<string>(newMembers);
                    _currentIndexClean = newIndex;
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId,
                        "Некоректний тип списку чергових. Наявні throw, clean",
                        cancellationToken: cancellationToken);
                    return;
                }
            }


            // Зміна чергового
            if (taskType != string.Empty)
            {
                await botClient.SendTextMessageAsync(chatId, "Дякую! Змінюю чергового.",
                    cancellationToken: cancellationToken);
                ChangeTurn(taskType);
                await NotifyNext(chatId, taskType);
            }
        }
    }

    private static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
    }

    private static void ChangeTurn(string taskType)
    {
        if (taskType == "throw")
            _currentIndexThrow = (_currentIndexThrow + 1) % _members_throw.Count;
        else if (taskType == "clean")
        {
            _currentIndexClean = (_currentIndexClean + 1) % _members_clean.Count;
        }
    }

    private static async Task NotifyNext(long chatId, string taskType)
    {
        if (taskType == "throw")
        {
            var nextUser = _members_throw[_currentIndexThrow];
            await _botClient.SendTextMessageAsync(chatId, $"{nextUser}, привіт, зараз твоя черга викидати сміття!",
                parseMode: ParseMode.Markdown);
        }
        else if (taskType == "clean")
        {
            var nextUser = _members_clean[_currentIndexClean];
        }
    }

    private static async Task StartReminders(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var chatId = Environment.GetEnvironmentVariable("CHAT_ID");// ID чату 
            var currentUser = _members_throw[_currentIndexThrow];
            DateTime now = DateTime.Now;
            string currentTime = now.ToString("HH:mm");
            DayOfWeek dayOfWeek = now.DayOfWeek;
            if (currentTime == "16:00" && dayOfWeek == DayOfWeek.Friday)
                await _botClient.SendTextMessageAsync(chatId,
                    $"{_currentIndexClean}, привіт, ці вихідні твоя черга прибирати у квартирі!",
                    parseMode: ParseMode.Markdown);
            if (currentTime == "09:00")
                await _botClient.SendTextMessageAsync(chatId, $"{currentUser}, привіт, не забудь викинути сміття!",
                    cancellationToken: cancellationToken);
            Thread.Sleep(60000); // 1 minute delay
        }
    }
}