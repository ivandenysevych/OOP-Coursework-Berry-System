using System.Diagnostics;

namespace warehouse_management_system;

public static class Program
{
    public static int Main(string[] args)
    {
        var rootDirectory = Directory.GetCurrentDirectory();
        var webProjectPath = Path.Combine(rootDirectory, "WarehouseWeb", "WarehouseWeb.csproj");

        if (!File.Exists(webProjectPath))
        {
            Console.Error.WriteLine($"Не знайдено веб-проєкт: {webProjectPath}");
            return 1;
        }

        var forwardedArgs = BuildForwardedArgs(args);
        var processInfo = new ProcessStartInfo(
            "dotnet",
            $"run --project \"{webProjectPath}\"{forwardedArgs}")
        {
            WorkingDirectory = rootDirectory,
            UseShellExecute = false
        };

        using var process = Process.Start(processInfo);
        if (process == null)
        {
            Console.Error.WriteLine("Не вдалося запустити дочірній процес dotnet.");
            return 1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static string BuildForwardedArgs(string[] args)
    {
        if (args.Length == 0)
        {
            return string.Empty;
        }

        var escaped = args.Select(EscapeArg);
        return " -- " + string.Join(' ', escaped);
    }

    private static string EscapeArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        if (!arg.Contains(' ') && !arg.Contains('"'))
        {
            return arg;
        }

        return "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
}
