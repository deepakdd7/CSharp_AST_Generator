using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string rootPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

        var csFiles = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
            .ToList();

        var syntaxTrees = csFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)).ToList();

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create("ReturnAnalyzer")
            .AddSyntaxTrees(syntaxTrees)
            .AddReferences(references);

        var results = new List<object>();

        foreach (var tree in syntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methods)
            {
                var methodInfo = new Dictionary<string, object>
                {
                    ["file"] = tree.FilePath,
                    ["method"] = method.Identifier.Text,
                    ["returns"] = new List<object>()
                };

                foreach (var ret in method.DescendantNodes().OfType<ReturnStatementSyntax>())
                {
                    var dependencies = new List<object>();
                    AnalyzeExpression(ret.Expression, model, dependencies, 0, 5);
                    ((List<object>)methodInfo["returns"]).Add(dependencies);
                }

                if (((List<object>)methodInfo["returns"]).Count > 0)
                {
                    results.Add(methodInfo);
                }
            }
        }

        var outputPath = Path.Combine(rootPath, "metadata.json");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        Console.WriteLine($"Metadata written to {outputPath}");
    }

    static void AnalyzeExpression(ExpressionSyntax? expr, SemanticModel model, List<object> deps, int level, int maxDepth)
    {
        if (expr == null || level > maxDepth) return;

        var symbol = model.GetSymbolInfo(expr).Symbol;
        var typeInfo = model.GetTypeInfo(expr).Type;

        if (symbol != null)
        {
            deps.Add(new
            {
                level,
                kind = symbol.Kind.ToString(),
                name = symbol.Name,
                type = symbol switch
                {
                    ILocalSymbol local => local.Type.ToDisplayString(),
                    IPropertySymbol prop => prop.Type.ToDisplayString(),
                    IFieldSymbol field => field.Type.ToDisplayString(),
                    IMethodSymbol method => method.ReturnType.ToDisplayString(),
                    _ => typeInfo?.ToDisplayString() ?? "unknown"
                }
            });
        }
        else if (typeInfo != null)
        {
            deps.Add(new
            {
                level,
                kind = "TypeOnly",
                name = expr.ToString(),
                type = typeInfo.ToDisplayString()
            });
        }

        // Drill down
        switch (expr)
        {
            case ObjectCreationExpressionSyntax oce when oce.Initializer != null:
                foreach (var init in oce.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                {
                    deps.Add(new
                    {
                        level = level + 1,
                        property = init.Left.ToString()
                    });
                    AnalyzeExpression(init.Right, model, deps, level + 1, maxDepth);
                }
                break;

            case InvocationExpressionSyntax invocation:
                foreach (var arg in invocation.ArgumentList.Arguments)
                {
                    AnalyzeExpression(arg.Expression, model, deps, level + 1, maxDepth);
                }
                break;

            case MemberAccessExpressionSyntax memberAccess:
                AnalyzeExpression(memberAccess.Expression, model, deps, level + 1, maxDepth);
                break;

            case BinaryExpressionSyntax binary:
                AnalyzeExpression(binary.Left, model, deps, level + 1, maxDepth);
                AnalyzeExpression(binary.Right, model, deps, level + 1, maxDepth);
                break;

            case ConditionalExpressionSyntax cond:
                AnalyzeExpression(cond.Condition, model, deps, level + 1, maxDepth);
                AnalyzeExpression(cond.WhenTrue, model, deps, level + 1, maxDepth);
                AnalyzeExpression(cond.WhenFalse, model, deps, level + 1, maxDepth);
                break;
        }
    }
}
