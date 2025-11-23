namespace InzDynamicLoader.Core;

internal static class InzConsole
{
    public static void Headline(string content)
    {
        Console.WriteLine($"[{DateTimeOffset.Now:F}] ======= {content} =======");
    }

    public static void EndHeadline()
    {
        Console.WriteLine($"[{DateTimeOffset.Now:u}] ==============");
        Console.WriteLine();
    }

    public static void Log(string message)
    {
        Console.WriteLine(message);
    }

    public static void FirstLevelItem(string message)
    {
        Console.WriteLine($"|-👉 {message}");
    }

    public static void SecondLevelItem(string message)
    {
        Console.WriteLine($"|---👉 {message}");
    }

    public static void ThirdLevelItem(string message)
    {
        Console.WriteLine($"|-----👉 {message}");
    }

    public static void LogWithNewLine(string message)
    {
        Console.WriteLine(message);
        Console.WriteLine();
    }

    public static void Success(string message)
    {
        Console.BackgroundColor = ConsoleColor.Green;
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("🥳 " + message);
        Console.ResetColor();
    }

    public static void SuccessWithNewLine(string message)
    {
        Console.BackgroundColor = ConsoleColor.Green;
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("🥳 " + message);
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
    }
    
    public static void Error(string message)
    {
        Console.Error.WriteLine("🤬 " + message);
    }

    public static void ErrorWithNewLine(string message)
    {
        Console.Error.WriteLine(message);
        Console.WriteLine();
    }

    public static void Warning(string message)
    {
        Console.BackgroundColor = ConsoleColor.Yellow;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.WriteLine("😑 " + message);
        Console.ResetColor();
    }

    public static void WarningWithNewLine(string message)
    {
        Console.BackgroundColor = ConsoleColor.Yellow;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Write("😑 " + message);
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
    }
}