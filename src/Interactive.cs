// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Graph.Beta.Models.ExternalConnectors;

namespace GitHubConnector;

/// <summary>
/// Static class providing user prompts and input.
/// </summary>
public static class Interactive
{
    /// <summary>
    /// Display the main menu and get user input.
    /// </summary>
    /// <param name="currentConnection">The currently selected connection.</param>
    /// <returns>The user's choice.</returns>
    public static MenuChoice DoMenuPrompt(ExternalConnection? currentConnection)
    {
        Console.WriteLine($"Current connection: {(currentConnection is null ? "NONE" : currentConnection.Name)}");
        Console.WriteLine("Please choose one of the following options:");
        Console.WriteLine($"{Convert.ToInt32(MenuChoice.CreateConnection)}. Create a connection");
        Console.WriteLine($"{Convert.ToInt32(MenuChoice.SelectConnection)}. Select existing connection");
        Console.WriteLine($"{Convert.ToInt32(MenuChoice.DeleteConnection)}. Delete current connection");
        Console.WriteLine($"{Convert.ToInt32(MenuChoice.RegisterSchema)}. Register schema for current connection");
        Console.WriteLine($"{Convert.ToInt32(MenuChoice.PushAllItems)}. Push items to current connection");
        Console.WriteLine($"{Convert.ToInt32(MenuChoice.Exit)}. Exit");

        try
        {
            var choice = int.Parse(Console.ReadLine() ?? string.Empty);
            return Enum.IsDefined(typeof(MenuChoice), choice) ?
                (MenuChoice)choice : MenuChoice.Invalid;
        }
        catch (FormatException)
        {
            return MenuChoice.Invalid;
        }
    }

    /// <summary>
    /// Prompt the user for input.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <param name="valueRequired">Value indicating whether a non-empty input is required.</param>
    /// <returns>The user's input.</returns>
    public static string? PromptForInput(string prompt, bool valueRequired)
    {
        string? response;

        do
        {
            Console.WriteLine($"{prompt}{(valueRequired ? string.Empty : " (OPTIONAL)")}:");
            response = Console.ReadLine();
            if (valueRequired && string.IsNullOrEmpty(response))
            {
                Console.WriteLine("You must provide a value");
            }
        }
        while (valueRequired && string.IsNullOrEmpty(response));

        return response;
    }

    /// <summary>
    /// Prompt the user to choose an item type.
    /// </summary>
    /// <returns>A string indicating the chosen item type.</returns>
    public static string PromptForItemType()
    {
        do
        {
            Console.WriteLine("What type of data?");
            Console.WriteLine("1. Repositories");
            Console.WriteLine("2. Issues");

            try
            {
                var choice = int.Parse(Console.ReadLine() ?? string.Empty);
                switch (choice)
                {
                    case 1:
                        return "repos";
                    case 2:
                        return "issues";
                    default:
                        Console.WriteLine("Invalid choice!");
                        break;
                }
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid choice!");
            }
        }
        while (true);
    }

    /// <summary>
    /// Prompt the user to choose from a list of connections.
    /// </summary>
    /// <param name="connections">The list of connections to choose from.</param>
    /// <returns>The index into the collection of the user's choice.</returns>
    public static int PromptToSelectConnection(List<ExternalConnection> connections)
    {
        Console.WriteLine("Choose one of the following connections:");
        int menuNumber = 1;

        foreach (var connection in connections)
        {
            Console.WriteLine($"{menuNumber++}. {connection.Name}");
        }

        do
        {
            try
            {
                var choice = int.Parse(Console.ReadLine() ?? string.Empty);
                if (choice > 0 && choice <= connections.Count)
                {
                    // Return the 0-based index
                    return choice - 1;
                }
                else
                {
                    Console.WriteLine("Invalid choice!");
                }
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid choice!");
            }
        }
        while (true);
    }
}
