using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace HideMyEmailGenerator.Services;

public static class EmailGenerator
{
    private const string CookieFile = "cookie.txt";
    private const int MaxConcurrentTasks = 10;

    public static async Task GenerateAsync(int? count = null)
    {
        await using var service = CreateService();
        await GenerateEmailsAsync(service, count);
    }

    public static async Task ListAsync(bool active, string? search = null)
    {
        await using var service = CreateService();
        await ListEmailsAsync(service, active, search);
    }

    private static HideMyEmailService CreateService()
    {
        var cookies = LoadCookies();
        return new HideMyEmailService(cookies);
    }

    private static string LoadCookies()
    {
        if (!File.Exists(CookieFile))
        {
            AnsiConsole.MarkupLine("[bold red]ERROR:[/] No \"cookie.txt\" file found!");
            AnsiConsole.MarkupLine("\nPlease create a \"cookie.txt\" file in the project root directory and paste your iCloud cookies.");
            AnsiConsole.MarkupLine("See README.md for detailed instructions on how to obtain the cookies.");
            Environment.Exit(1);
        }

        var cookies = File.ReadAllLines(CookieFile)
            .FirstOrDefault(line => !line.Trim().StartsWith("//")) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cookies))
        {
            AnsiConsole.MarkupLine("[bold red]ERROR:[/] The \"cookie.txt\" file is empty!");
            AnsiConsole.MarkupLine("\nPlease paste your iCloud cookies into the \"cookie.txt\" file.");
            AnsiConsole.MarkupLine("See README.md for detailed instructions on how to obtain the cookies.");
            Environment.Exit(1);
        }

        return cookies;
    }

    private static async Task GenerateEmailsAsync(HideMyEmailService service, int? count)
    {
        var emails = new List<string>();
        AnsiConsole.Write(new Rule());

        count ??= AnsiConsole.Prompt(new TextPrompt<int>("How many iCloud emails you want to generate?"));

        AnsiConsole.MarkupLine($"Generating {count} email(s)...");
        AnsiConsole.Write(new Rule());

        await AnsiConsole.Status()
            .StartAsync("Generating iCloud email(s)...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                ctx.SpinnerStyle(Style.Parse("green"));

                int remaining = count.Value;
                while (remaining > 0)
                {
                    int batchSize = Math.Min(remaining, MaxConcurrentTasks);
                    var batch = await GenerateBatchAsync(service, batchSize);
                    remaining -= MaxConcurrentTasks;
                    emails.AddRange(batch);
                }
            });

        if (emails.Count > 0)
        {
            await File.AppendAllLinesAsync("emails.txt", emails);
            AnsiConsole.Write(new Rule());
            AnsiConsole.MarkupLine(":star: Emails saved to \"emails.txt\"");
            AnsiConsole.MarkupLine($"[bold green]Done![/] Generated [bold green]{emails.Count}[/] email(s)");
        }
    }

    private static async Task<List<string>> GenerateBatchAsync(HideMyEmailService service, int count)
    {
        var tasks = Enumerable.Range(0, count).Select(_ => GenerateOneAsync(service));
        var results = await Task.WhenAll(tasks);
        return results.Where(e => e != null).ToList()!;
    }

    private static async Task<string?> GenerateOneAsync(HideMyEmailService service)
    {
        var genRes = await service.GenerateEmailAsync();
        if (genRes == null || !GetBoolValue(genRes, "success"))
        {
            var errMsg = ExtractErrorMessage(genRes);
            AnsiConsole.MarkupLine($"[bold red][[ERR]][/] Failed to generate. Reason: {errMsg}");
            return null;
        }

        var email = ExtractEmail(genRes);
        if (string.IsNullOrEmpty(email)) return null;

        AnsiConsole.MarkupLine($"[green][[50%]][/] \"{email}\" - Generated");

        var reserveRes = await service.ReserveEmailAsync(email);
        if (reserveRes == null || !GetBoolValue(reserveRes, "success"))
        {
            var errMsg = ExtractErrorMessage(reserveRes);
            AnsiConsole.MarkupLine($"[bold red][[ERR]][/] \"{email}\" - Failed to reserve. Reason: {errMsg}");
            return null;
        }

        AnsiConsole.MarkupLine($"[green][[100%]][/] \"{email}\" - Reserved");
        return email;
    }

    private static async Task ListEmailsAsync(HideMyEmailService service, bool active, string? search)
    {
        var genRes = await service.ListEmailAsync();
        if (genRes == null || !GetBoolValue(genRes, "success"))
        {
            var errMsg = ExtractErrorMessage(genRes);
            AnsiConsole.MarkupLine($"[bold red][[ERR]][/] Failed to list. Reason: {errMsg}");
            return;
        }

        var table = new Table();
        table.AddColumn("Label");
        table.AddColumn("Hide my email");
        table.AddColumn("Created Date Time");
        table.AddColumn("IsActive");

        if (genRes["result"] is JsonElement resultElement)
        {
            var resultDict = JsonSerializer.Deserialize<Dictionary<string, object>>(resultElement.GetRawText());
            if (resultDict?["hmeEmails"] is JsonElement emailsElement)
            {
                var emails = JsonSerializer.Deserialize<List<JsonElement>>(emailsElement.GetRawText());
                foreach (var emailElement in emails ?? [])
                {
                    var email = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(emailElement.GetRawText());
                    if (email == null) continue;

                    bool isActive = email["isActive"].GetBoolean();
                    if (isActive != active) continue;

                    string label = email["label"].GetString() ?? string.Empty;
                    if (search != null && !Regex.IsMatch(label, search)) continue;

                    string hme = email["hme"].GetString() ?? string.Empty;
                    long createTimestamp = email["createTimestamp"].GetInt64();
                    var createDateTime = DateTimeOffset.FromUnixTimeMilliseconds(createTimestamp).DateTime;

                    table.AddRow(label, hme, createDateTime.ToString(), isActive.ToString());
                }
            }
        }

        AnsiConsole.Write(table);
    }

    private static bool GetBoolValue(Dictionary<string, object>? dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key)) return false;

        return dict[key] switch
        {
            bool b => b,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            _ => false
        };
    }

    private static string ExtractErrorMessage(Dictionary<string, object>? dict)
    {
        if (dict == null) return "Unknown";

        if (dict.TryGetValue("error", out var error))
        {
            if (error is int && dict.TryGetValue("reason", out var reason))
                return reason.ToString() ?? "Unknown";

            if (error is JsonElement errorElement)
            {
                try
                {
                    var errorDict = JsonSerializer.Deserialize<Dictionary<string, object>>(errorElement.GetRawText());
                    if (errorDict?.TryGetValue("errorMessage", out var msg) == true)
                        return msg.ToString() ?? "Unknown";
                }
                catch { }
            }
        }

        return "Unknown";
    }

    private static string? ExtractEmail(Dictionary<string, object> dict)
    {
        if (dict["result"] is not JsonElement resultElement) return null;

        try
        {
            var resultDict = JsonSerializer.Deserialize<Dictionary<string, object>>(resultElement.GetRawText());
            return resultDict?.TryGetValue("hme", out var hme) == true ? hme.ToString() : null;
        }
        catch
        {
            return null;
        }
    }
}
