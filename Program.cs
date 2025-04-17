using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace HideMyEmailGenerator
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("iCloud Hide My Email generator");

            // Generate command
            var generateCommand = new Command("generate", "Generate emails");
            var countOption = new Option<int?>(
                "--count",
                getDefaultValue: () => 5,
                description: "How many emails to generate");
            generateCommand.AddOption(countOption);
            generateCommand.SetHandler(async (count) =>
            {
                try
                {
                    await EmailGenerator.GenerateAsync(count);
                }
                catch (OperationCanceledException)
                {
                    // Gracefully handle cancellation
                    return;
                }
            }, countOption);

            // List command
            var listCommand = new Command("list", "List emails");
            var activeOption = new Option<bool>(
                "--active",
                getDefaultValue: () => true,
                description: "Filter Active / Inactive emails");
            var inactiveOption = new Option<bool>(
                "--inactive",
                getDefaultValue: () => false,
                description: "Filter Inactive emails");
            var searchOption = new Option<string>(
                "--search",
                description: "Search emails");
            listCommand.AddOption(activeOption);
            listCommand.AddOption(inactiveOption);
            listCommand.AddOption(searchOption);
            listCommand.SetHandler(async (active, inactive, search) =>
            {
                // If inactive is specified, override active
                bool isActive = !inactive && active;
                try
                {
                    await EmailGenerator.ListAsync(isActive, search);
                }
                catch (OperationCanceledException)
                {
                    // Gracefully handle cancellation
                    return;
                }
            }, activeOption, inactiveOption, searchOption);

            rootCommand.AddCommand(generateCommand);
            rootCommand.AddCommand(listCommand);

            // Default action when no command is specified
            rootCommand.SetHandler(async () =>
            {
                try
                {
                    await EmailGenerator.GenerateAsync(null);
                }
                catch (OperationCanceledException)
                {
                    // Gracefully handle cancellation
                    return;
                }
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
} 