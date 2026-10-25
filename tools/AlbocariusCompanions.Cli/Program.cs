namespace AlbocariusCompanions.Cli;

public static class Program
{
    public static int Main(string[] args) => CliProgram.Run(args, Console.Out, Console.Error);
}
