using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Spectre.Console;

namespace HideMyEmailGenerator
{
    public class RichHideMyEmail : HideMyEmail
    {
        private const string CookieFile = "cookie.txt";
        private const int MaxConcurrentTasks = 10;

        public RichHideMyEmail() : base()
        {
            if (File.Exists(CookieFile))
            {
                // Load cookie string from file
                string[] lines = File.ReadAllLines(CookieFile);
                Cookies = lines.FirstOrDefault(line => !line.Trim().StartsWith("//"));
            }
            else
            {
                AnsiConsole.MarkupLine("[bold yellow][[WARN]][/] No \"cookie.txt\" file found! Generation might not work due to unauthorized access.");
            }
        }

        private bool GetBooleanValue(Dictionary<string, object> dict, string key, bool defaultValue = false)
        {
            if (!dict.ContainsKey(key))
                return defaultValue;

            var value = dict[key];
            if (value is bool boolValue)
                return boolValue;
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.True)
                    return true;
                if (jsonElement.ValueKind == JsonValueKind.False)
                    return false;
                if (jsonElement.ValueKind == JsonValueKind.Number)
                    return jsonElement.GetInt32() != 0;
                if (jsonElement.ValueKind == JsonValueKind.String)
                    return !string.IsNullOrEmpty(jsonElement.GetString());
            }
            return defaultValue;
        }

        private async Task<string> GenerateOneAsync()
        {
            // First, generate an email
            var genRes = await GenerateEmailAsync();

            if (genRes == null)
            {
                return null;
            }
            else if (!genRes.ContainsKey("success") || !GetBooleanValue(genRes, "success"))
            {
                string errMsg = "Unknown";
                if (genRes.ContainsKey("error"))
                {
                    var error = genRes["error"];
                    if (error is int && genRes.ContainsKey("reason"))
                    {
                        errMsg = genRes["reason"].ToString();
                    }
                    else if (genRes["error"] is JsonElement errorElement)
                    {
                        try
                        {
                            var errorDict = JsonSerializer.Deserialize<Dictionary<string, object>>(errorElement.GetRawText());
                            if (errorDict.ContainsKey("errorMessage"))
                            {
                                errMsg = errorDict["errorMessage"].ToString();
                            }
                        }
                        catch { }
                    }
                }
                AnsiConsole.MarkupLine($"[bold red][[ERR]][/] - Failed to generate email. Reason: {errMsg}");
                return null;
            }

            // Extract the email from the result
            string email = null;
            if (genRes["result"] is JsonElement resultElement)
            {
                try
                {
                    var resultDict = JsonSerializer.Deserialize<Dictionary<string, object>>(resultElement.GetRawText());
                    if (resultDict.ContainsKey("hme"))
                    {
                        email = resultDict["hme"].ToString();
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(email))
            {
                return null;
            }

            AnsiConsole.MarkupLine($"[green][[50%]][/] \"{email}\" - Successfully generated");

            // Then, reserve it
            var reserveRes = await ReserveEmailAsync(email);

            if (reserveRes == null)
            {
                return null;
            }
            else if (!reserveRes.ContainsKey("success") || !GetBooleanValue(reserveRes, "success"))
            {
                string errMsg = "Unknown";
                if (reserveRes.ContainsKey("error"))
                {
                    var error = reserveRes["error"];
                    if (error is int && reserveRes.ContainsKey("reason"))
                    {
                        errMsg = reserveRes["reason"].ToString();
                    }
                    else if (reserveRes["error"] is JsonElement errorElement)
                    {
                        try
                        {
                            var errorDict = JsonSerializer.Deserialize<Dictionary<string, object>>(errorElement.GetRawText());
                            if (errorDict.ContainsKey("errorMessage"))
                            {
                                errMsg = errorDict["errorMessage"].ToString();
                            }
                        }
                        catch { }
                    }
                }
                AnsiConsole.MarkupLine($"[bold red][[ERR]][/] \"{email}\" - Failed to reserve email. Reason: {errMsg}");
                return null;
            }

            AnsiConsole.MarkupLine($"[green][[100%]][/] \"{email}\" - Successfully reserved");
            return email;
        }

        private async Task<List<string>> GenerateBatchAsync(int num)
        {
            var tasks = new List<Task<string>>();
            for (int i = 0; i < num; i++)
            {
                tasks.Add(GenerateOneAsync());
            }

            var results = await Task.WhenAll(tasks);
            return results.Where(e => e != null).ToList();
        }

        public async Task<List<string>> GenerateAsync(int? count = null)
        {
            try
            {
                var emails = new List<string>();
                AnsiConsole.Write(new Rule());

                if (count == null)
                {
                    count = AnsiConsole.Prompt(
                        new TextPrompt<int>("How many iCloud emails you want to generate?")
                    );
                }

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
                            var batch = await GenerateBatchAsync(batchSize);
                            remaining -= MaxConcurrentTasks;
                            emails.AddRange(batch);
                        }
                    });

                if (emails.Count > 0)
                {
                    File.AppendAllLines("emails.txt", emails);

                    AnsiConsole.Write(new Rule());
                    AnsiConsole.MarkupLine(":star: Emails have been saved into the \"emails.txt\" file");
                    AnsiConsole.MarkupLine($"[bold green]All done![/] Successfully generated [bold green]{emails.Count}[/] email(s)");
                }

                return emails;
            }
            catch (OperationCanceledException)
            {
                return new List<string>();
            }
        }

        public async Task ListAsync(bool active, string search = null)
        {
            var genRes = await ListEmailAsync();
            if (genRes == null)
            {
                return;
            }

            if (!genRes.ContainsKey("success") || !GetBooleanValue(genRes, "success"))
            {
                string errMsg = "Unknown";
                if (genRes.ContainsKey("error"))
                {
                    var error = genRes["error"];
                    if (error is int && genRes.ContainsKey("reason"))
                    {
                        errMsg = genRes["reason"].ToString();
                    }
                    else if (genRes["error"] is JsonElement errorElement)
                    {
                        try
                        {
                            var errorDict = JsonSerializer.Deserialize<Dictionary<string, object>>(errorElement.GetRawText());
                            if (errorDict.ContainsKey("errorMessage"))
                            {
                                errMsg = errorDict["errorMessage"].ToString();
                            }
                        }
                        catch { }
                    }
                }
                AnsiConsole.MarkupLine($"[bold red][[ERR]][/] - Failed to list emails. Reason: {errMsg}");
                return;
            }

            var table = new Table();
            table.AddColumn("Label");
            table.AddColumn("Hide my email");
            table.AddColumn("Created Date Time");
            table.AddColumn("IsActive");

            if (genRes["result"] is JsonElement resultElement)
            {
                try
                {
                    var resultDict = JsonSerializer.Deserialize<Dictionary<string, object>>(resultElement.GetRawText());
                    if (resultDict.ContainsKey("hmeEmails") && resultDict["hmeEmails"] is JsonElement emailsElement)
                    {
                        var emails = JsonSerializer.Deserialize<List<JsonElement>>(emailsElement.GetRawText());
                        
                        foreach (var emailElement in emails)
                        {
                            var email = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(emailElement.GetRawText());
                            
                            bool isActive = email["isActive"].GetBoolean();
                            if (isActive == active)
                            {
                                string label = email["label"].GetString();
                                if (search == null || Regex.IsMatch(label, search))
                                {
                                    string hme = email["hme"].GetString();
                                    long createTimestamp = email["createTimestamp"].GetInt64();
                                    var createDateTime = DateTimeOffset.FromUnixTimeMilliseconds(createTimestamp).DateTime;
                                    
                                    table.AddRow(
                                        label,
                                        hme,
                                        createDateTime.ToString(),
                                        isActive.ToString()
                                    );
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteException(ex);
                }
            }

            AnsiConsole.Write(table);
        }
    }

    // Static methods for easy access
    public static class EmailGenerator
    {
        public static async Task GenerateAsync(int? count = null)
        {
            await using var hme = await new RichHideMyEmail().InitializeAsync();
            await ((RichHideMyEmail)hme).GenerateAsync(count);
        }

        public static async Task ListAsync(bool active, string search = null)
        {
            await using var hme = await new RichHideMyEmail().InitializeAsync();
            await ((RichHideMyEmail)hme).ListAsync(active, search);
        }
    }
} 