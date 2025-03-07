if (args.Length < 2)
{
    Console.WriteLine("Usage: GraphMigrationBot <repository-path> <command> [documentation-url]");
    Console.WriteLine("\nCommands:");
    Console.WriteLine("  patterns       - Extracts and saves deprecated API usage patterns from the documentation.");
    Console.WriteLine("  identify       - Scans the specified repository for deprecated API usage using saved patterns.");
    Console.WriteLine("\nExample Usage:");
    Console.WriteLine("  GraphMigrationBot C:\\Projects\\MyRepo identify");
    return;
}

string repoPath = args[0];
string command = args[1];

string documentationUrl = args.Length > 2 ? args[2] : "https://learn.microsoft.com/en-us/graph/migrate-azure-ad-graph-planning-checklist";
string patternsFilePath = "deprecated_patterns.txt";

var migrationBot = new GraphMigrationBot();

if (command == "patterns")
{
    Console.WriteLine("Scanning for deprecated MS Graph usage...");

    var deprecatedApiPatterns = await migrationBot.InfereDeprecatedApiPatternsAsync(documentationUrl);

    File.WriteAllLines(patternsFilePath, deprecatedApiPatterns);
    Console.WriteLine($"Saved {deprecatedApiPatterns.Count} deprecated API patterns to {patternsFilePath}.");
}
else if (command == "identify")
{
    if (!File.Exists(patternsFilePath))
    {
        Console.WriteLine("Deprecated API patterns file not found. Run 'patterns' command first.");
        return;
    }

    var deprecatedApiPatterns = new List<string>(File.ReadAllLines(patternsFilePath));
    migrationBot.SetDeprecatedApiPatterns(deprecatedApiPatterns);


    Console.WriteLine($"Scanning repository: {repoPath}");
    var findings = migrationBot.ScanRepository(repoPath);

    if (findings.Count == 0)
    {
        Console.WriteLine("No deprecated MS Graph usage found.");
        return;
    }

    Console.WriteLine($"\nIdentified potential migration points ({findings.Count}):");
    foreach (var finding in findings)
    {
        Console.WriteLine($"# {finding.Key}");
        foreach (var line in finding.Value)
        {
            Console.WriteLine($"  - {line}");
        }
    }

    Console.WriteLine("\nGenerating migration suggestions...");
    await migrationBot.GenerateMigrationSuggestions(findings, documentationUrl);
}
else
{
    Console.WriteLine($"Unknown command: {command}");
    return;
}
