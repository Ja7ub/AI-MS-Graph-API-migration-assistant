using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.SemanticKernel;

public class GraphMigrationBot
{
    private List<string> deprecatedApiPatterns = new List<string>()
    {
        "https://graph.windows.net/",  // AAD Graph
        "microsoft.graph.beta",        // Beta API
        "microsoft.graph.v1.0"         // Current stable API (for future migration)
    };

    private string apiKey;
    private string deploymentName = "gpt-4o";
    // There are two different types! (Azure AI services and Azure OpenAI)
    //private string azureEndpoint = "https://h-m7xkz0pd-eastus2.cognitiveservices.azure.com";
    private string azureEndpoint = "https://ai-ms-graph-api-migration-assistant.openai.azure.com";

    public GraphMigrationBot()
    {
        var apiKeyEnvName = "GAMA_API_KEY";
        apiKey = Environment.GetEnvironmentVariable(apiKeyEnvName);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException($"OpenAI API key is not set in the environment variable {apiKeyEnvName}.");
        }
    }

    public void SetDeprecatedApiPatterns(List<string> patterns)
    {
        Console.WriteLine($"Loading API patterns");
        deprecatedApiPatterns = patterns;
    }

    public Dictionary<string,List<string>> ScanRepository(string repoPath)
    {
        Dictionary<string, List<string>> results = new();
        var csFiles = Directory.GetFiles(repoPath, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            List<string> fileResults = new();
            Console.WriteLine($"Scanning file: {file.Replace(repoPath, "")}");
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                string invocationText = invocation.ToString();

                if (deprecatedApiPatterns.Any(pattern => invocationText.Contains(pattern)))
                {
                    var result = $"File: {file}, Line: {invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}, Code: {invocationText}";
                    fileResults.Add(result);
                    Console.WriteLine(result);
                }
            }
            if (fileResults.Count > 0)
            {
                results[file] = fileResults;
            }
        }

        return results;
    }

    public async Task GenerateMigrationSuggestions(Dictionary<string, List<string>> findings, string documentationUrl) {
        foreach (var finding in findings)
        {
            var filename = finding.Key;
            var fileFindings = finding.Value;
            var sourceCode = File.ReadAllText(filename);
            await GenerateMigrationSuggestionsForSingleFile(sourceCode, filename, fileFindings, documentationUrl);
            Console.WriteLine("Press any key to continue to the next file...");
            Console.ReadKey();
        }
    }

    public async Task GenerateMigrationSuggestionsForSingleFile(string sourceCode, string filename, List<string> findings, string documentationUrl)
    {
        var kernel = buildSemanticKernel();
        var prompt = @"
You are an expert in API migrations. Given these identified possible deprecated API usage places, the migration documentation reference guide and source code for this file, confirm these are really relevant places for a migration and if yes, advise what specifically should change.

Findings:
{{$findings}}

Documentation reference: {{$documentationUrl}}

Filename: {{$sourceCode}} and source code:
{{$sourceCode}}
";

        var function = kernel.CreateFunctionFromPrompt(prompt);
        var arguments = new KernelArguments
        {
            { "findings", string.Join("\n", findings) },
            { "documentationUrl", documentationUrl },
            { "filename", filename },
            { "sourceCode", sourceCode }
        };
        var result = await kernel.InvokeAsync(function, arguments);

        Console.WriteLine($"\nMigration Recommendations ({filename}):");
        Console.WriteLine(result);
    }

    public async Task<List<string>> InfereDeprecatedApiPatternsAsync(string documentationUrl)
    {
        var kernel = buildSemanticKernel();
        var prompt = @"
Go through this documentation reference thoroughly {{$documentationUrl}} and find deprecated API usage patterns, which will be later used to identify deprecated API usage in the code by searching for the patterns (with string.Contains() C# method).

Output each usage pattern on a new line. No additional text.
";

        var function = kernel.CreateFunctionFromPrompt(prompt);
        var arguments = new KernelArguments
        {
            { "documentationUrl", documentationUrl }
        };
        var result = await kernel.InvokeAsync(function, arguments);

        Console.WriteLine("\nInfered deprecated API patterns respose:");
        Console.WriteLine(result);

        // Parse the output into a list of deprecated API patterns
        List<string> deprecatedPatterns = result?.ToString()
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList() ?? new List<string>();

        Console.WriteLine("\nInferred Deprecated API Patterns:");
        deprecatedPatterns.ForEach(Console.WriteLine);

        return deprecatedPatterns;
    }

    private Kernel buildSemanticKernel()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder = kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureEndpoint, apiKey);
        var kernel = kernelBuilder.Build();
        return kernel;
    }
}
