using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.SemanticKernel;

public class GraphMigrationBot
{
    private readonly string[] DeprecatedApiPatterns =
    {
        "https://graph.windows.net/",  // AAD Graph
        "microsoft.graph.beta",        // Beta API
        "microsoft.graph.v1.0"         // Current stable API (for future migration)
    };

    private string apiKey;
    private string deploymentName = "gpt-4";
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

    public List<string> ScanRepository(string repoPath)
    {
        List<string> results = new();
        var csFiles = Directory.GetFiles(repoPath, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            Console.WriteLine($"Scanning file: {file.Replace(repoPath, "")}");
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                string invocationText = invocation.ToString();

                if (DeprecatedApiPatterns.Any(pattern => invocationText.Contains(pattern)))
                {
                    var result = $"File: {file}, Line: {invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}, Code: {invocationText}";
                    results.Add(result);
                    Console.WriteLine(result);
                }
            }
        }
        return results;
    }

    public async Task GenerateMigrationSuggestions(List<string> findings, string documentationUrl)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder = kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureEndpoint, apiKey);
        var kernel = kernelBuilder.Build();
        var prompt = @"
You are an expert in Microsoft Graph API migrations. Given the following deprecated API usage examples, suggest the appropriate migration steps.

Findings:
{{$findings}}

Documentation reference: {{$documentationUrl}}

Provide step-by-step migration recommendations.
";

        var function = kernel.CreateFunctionFromPrompt(prompt);
        var arguments = new KernelArguments
        {
            { "findings", string.Join("\n", findings) },
            { "documentationUrl", documentationUrl }
        };
        var result = await kernel.InvokeAsync(function, arguments);

        Console.WriteLine("\nMigration Recommendations:");
        Console.WriteLine(result);
    }
}
