using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Simulation;
using AlbocariusCompanions.Core.Triggers;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Cli;

/// <summary>
/// Authoring CLI (SPEC_BEHAVIOR_FOUNDATION I6): validate companion folders and export their conversations
/// as Mermaid flowcharts; "serve" runs the simulator for an editor (M9 I3). Only reads; nothing here writes or throws.
/// </summary>
public static class CliProgram
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr) => Run(args, Console.In, stdout, stderr);

    public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
            return Usage(stderr);

        switch (args[0])
        {
            case "serve":
                return Serve(args, stdin, stdout, stderr);
            case "validate":
                return Validate(args, stdout, stderr);
            case "mermaid":
                return Mermaid(args, stdout, stderr);
            case "docs":
                return Docs(args, stdout, stderr);
            default:
                stderr.WriteLine($"Unknown command \"{args[0]}\".");
                return Usage(stderr);
        }
    }

    /// <summary>
    /// <c>validate &lt;path&gt;</c>: a folder with a companion.json is one companion (only its issues are reported, the
    /// catalog is still loaded from its parent so cross-companion checks run); any other folder is a Companions root.
    /// </summary>
    private static int Validate(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length != 2)
            return Usage(stderr);

        var path = args[1];
        if (!Directory.Exists(path))
            return NotFound(stderr, path);

        var (root, folder) = ResolveValidatePath(path, stderr);
        if (root == null)
            return 2;

        var issues = CompanionCatalog.Load(root).Issues;
        if (folder != null)
            issues = issues.Where(i => i.Folder == folder).ToList();

        WriteIssues(stdout, issues);
        var errors = issues.Count(i => i.IsError);
        stdout.WriteLine($"{errors} error(s), {issues.Count - errors} warning(s).");
        return errors > 0 ? 1 : 0;
    }

    /// <summary>
    /// <c>mermaid &lt;companionFolder&gt; [conversationId]</c>: the chart of one companion (or of one of its
    /// conversations) on stdout. A companion that did not load reports its issues on stderr instead.
    /// </summary>
    private static int Mermaid(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length is < 2 or > 3)
            return Usage(stderr);

        var path = args[1];
        var conversationId = args.Length == 3 ? args[2] : null;
        if (!Directory.Exists(path))
            return NotFound(stderr, path);

        // The path names a companion folder: its own name and the parent as the Companions root, whether or not
        // it is loadable (a missing companion.json then surfaces as that folder's issue).
        var folder = new DirectoryInfo(path);
        if (folder.Parent == null)
        {
            stderr.WriteLine($"Cannot load the parent folder of \"{path}\".");
            return 2;
        }

        var catalog = CompanionCatalog.Load(folder.Parent.FullName);
        if (!catalog.Registry.TryGet(folder.Name, out var package) || package == null)
        {
            WriteIssues(stderr, catalog.Issues.Where(i => i.Folder == folder.Name).ToList());
            return 1;
        }

        try
        {
            stdout.Write(MermaidExport.Render(package, conversationId));
        }
        catch (ArgumentException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }
        return 0;
    }

    /// <summary>
    /// The Companions root to load and the companion folder name (null for a whole root). A folder holding a
    /// companion.json is a companion, so its parent is the root; that parent must exist.
    /// </summary>
    private static (string? Root, string? Folder) ResolveValidatePath(string path, TextWriter stderr)
    {
        var folder = new DirectoryInfo(path);
        if (!File.Exists(Path.Combine(path, CompanionLoader.CompanionFileName)))
            return (folder.FullName, null);

        if (folder.Parent == null)
        {
            stderr.WriteLine($"Cannot load the parent folder of \"{path}\".");
            return (null, null);
        }
        return (folder.Parent.FullName, folder.Name);
    }

    /// <summary>One line per issue, in catalog order: <c>ERROR code [folder] message</c>.</summary>
    private static void WriteIssues(TextWriter output, IEnumerable<ValidationIssue> issues)
    {
        foreach (var issue in issues)
            output.WriteLine($"{(issue.IsError ? "ERROR" : "WARNING")} {issue.Code} [{issue.Folder}] {issue.Message}");
    }

    private static int NotFound(TextWriter stderr, string path)
    {
        stderr.WriteLine($"Path not found: {path}");
        return 2;
    }

    /// <summary>
    /// <c>serve [companionsRoot]</c>: the simulator (docs/SIMULATOR_PROTOCOL.md). One JSON request per stdin line, one
    /// JSON response per stdout line, until end of input or a "quit" request. A root given here is loaded first (as a
    /// "load" request would); the server still answers requests when it fails.
    /// </summary>
    private static int Serve(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length > 2)
            return Usage(stderr);

        var protocol = new SimulatorProtocol();
        if (args.Length == 2)
            stderr.WriteLine(protocol.Handle(new JObject { ["id"] = "preload", ["op"] = "load", ["root"] = args[1] }.ToString(Formatting.None)));

        string? line;
        while (!protocol.Quit && (line = stdin.ReadLine()) != null)
        {
            if (line.Trim().Length == 0)
                continue;
            stdout.WriteLine(protocol.Handle(line));
            stdout.Flush();
        }
        return 0;
    }

    /// <summary><c>docs triggers [outFile]</c>: docs/TRIGGERS.md from the trigger registry (M11 T2), to stdout or a file.</summary>
    private static int Docs(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length is < 2 or > 3 || args[1] != "triggers")
            return Usage(stderr);

        var text = TriggerDocs.Render();
        if (args.Length == 2)
        {
            stdout.Write(text);
            return 0;
        }
        try
        {
            File.WriteAllText(args[2], text);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            stderr.WriteLine($"Cannot write \"{args[2]}\": {ex.Message}");
            return 2;
        }
        stdout.WriteLine($"Wrote {args[2]} ({TriggerRegistry.All.Count} triggers).");
        return 0;
    }

    private static int Usage(TextWriter stderr)
    {
        stderr.WriteLine("Usage:");
        stderr.WriteLine("  validate <path>                            a companion folder, or a Companions directory");
        stderr.WriteLine("  mermaid <companionFolder> [conversationId] print the companion's Mermaid flowchart(s)");
        stderr.WriteLine("  docs triggers [outFile]                    trigger reference (docs/TRIGGERS.md) from the registry");
        stderr.WriteLine("  serve [companionsRoot]                     simulator: JSON lines on stdin/stdout (docs/SIMULATOR_PROTOCOL.md)");
        stderr.WriteLine("Exit codes: 0 ok, 1 validation errors, 2 usage, missing path or unknown conversation.");
        return 2;
    }
}
