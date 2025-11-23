namespace InzDynamicModuleLoader.Core;

internal static class InzConsole
{
    private static string Prefix => "[InzConsole] ";
    public static void Headline(string content)
    {
        Console.WriteLine($"{Prefix}[{DateTimeOffset.Now.TimeOfDay:c}] ======= {content} =======");
    }

    public static void EndHeadline()
    {
        Console.WriteLine($"{Prefix}[{DateTimeOffset.Now.TimeOfDay:c}] ==============");
        Console.WriteLine();
    }

    public static void Log(string message)
    {
        Console.WriteLine($"{Prefix}{message}");
    }

    public static void FirstLevelItem(string message)
    {
        Console.WriteLine($"{Prefix}-> {message}");
    }

    public static void SecondLevelItem(string message)
    {
        Console.WriteLine($"{Prefix}---> {message}");
    }

    public static void ThirdLevelItem(string message)
    {
        Console.WriteLine($"{Prefix}-----> {message}");
    }

    public static void LogWithNewLine(string message)
    {
        Console.WriteLine($"{Prefix}-> {message}");
        Console.WriteLine();
    }

    public static void Success(string message)
    {
        Console.BackgroundColor = ConsoleColor.Green;
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(Prefix + "🥳 " + message);
        Console.ResetColor();
    }

    public static void SuccessWithNewLine(string message)
    {
        Console.BackgroundColor = ConsoleColor.Green;
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(Prefix + "🥳 " + message);
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
    }
    
    public static void Error(string message)
    {
        Console.Error.WriteLine($"{Prefix}-> {message}");
    }

    public static void ErrorWithNewLine(string message)
    {
        Console.Error.WriteLine($"{Prefix}-> {message}");
        Console.WriteLine();
    }

    public static void Warning(string message)
    {
        Console.BackgroundColor = ConsoleColor.Yellow;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.WriteLine($"{Prefix}-> {message}");
        Console.ResetColor();
    }

    public static void WarningWithNewLine(string message)
    {
        Console.BackgroundColor = ConsoleColor.Yellow;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write($"{Prefix}-> {message}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
    }
}