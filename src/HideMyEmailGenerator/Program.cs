using System.CommandLine;
using HideMyEmailGenerator.Services;

var rootCommand = new RootCommand("iCloud Hide My Email generator");

var generateCommand = new Command("generate", "Generate emails");
var countOption = new Option<int?>("--count", () => 5, "How many emails to generate");
generateCommand.AddOption(countOption);
generateCommand.SetHandler(async (count) =>
{
    try
    {
        await EmailGenerator.GenerateAsync(count);
    }
    catch (OperationCanceledException) { }
}, countOption);

var listCommand = new Command("list", "List emails");
var activeOption = new Option<bool>("--active", () => true, "Filter Active / Inactive emails");
var inactiveOption = new Option<bool>("--inactive", () => false, "Filter Inactive emails");
var searchOption = new Option<string?>("--search", "Search emails");
listCommand.AddOption(activeOption);
listCommand.AddOption(inactiveOption);
listCommand.AddOption(searchOption);
listCommand.SetHandler(async (active, inactive, search) =>
{
    bool isActive = !inactive && active;
    try
    {
        await EmailGenerator.ListAsync(isActive, search);
    }
    catch (OperationCanceledException) { }
}, activeOption, inactiveOption, searchOption);

rootCommand.AddCommand(generateCommand);
rootCommand.AddCommand(listCommand);
rootCommand.SetHandler(async () =>
{
    try
    {
        await EmailGenerator.GenerateAsync(null);
    }
    catch (OperationCanceledException) { }
});

return await rootCommand.InvokeAsync(args); 