using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;

namespace Xamarin.CodeAnalysis
{
    [ExportCompletionProvider(nameof(ResourceCompletionProvider), LanguageNames.CSharp)]
    public class ResourceCompletionProvider : CompletionProvider
    {
        private static readonly CompletionItemRules StandardCompletionRules = CompletionItemRules.Default.WithSelectionBehavior(CompletionItemSelectionBehavior.SoftSelection);

        public override Task<CompletionDescription> GetDescriptionAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
        {
            // TODO: get the actual xml file location.
            return Task.FromResult(CompletionDescription.FromText(item.Properties["Summary"]));
        }

        public override Task<CompletionChange> GetChangeAsync(Document document, CompletionItem item, char? commitKey, CancellationToken cancellationToken)
        {
            if (item.Properties.TryGetValue("Start", out var start) && 
                int.TryParse(start, out var s) && 
                item.Properties.TryGetValue("Length", out var length) && 
                int.TryParse(length, out var l))
            {
                return Task.FromResult(CompletionChange.Create(new TextChange(new TextSpan(s, l), item.DisplayText)));
            }

            return base.GetChangeAsync(document, item, commitKey, cancellationToken);
        }

        static readonly Regex isResourceValue = new Regex(@"Resources\\values\\.*.xml", RegexOptions.Compiled);

        public override async Task ProvideCompletionsAsync(CompletionContext completionContext)
        {
            if (!completionContext.Document.SupportsSemanticModel)
                return;

            var position = completionContext.Position;
            var document = completionContext.Document;
            var cancellationToken = completionContext.CancellationToken;

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var span = completionContext.CompletionListSpan;
            var token = root.FindToken(span.Start);
            var node = token.Parent?.AncestorsAndSelf().FirstOrDefault(a => a.FullSpan.Contains(span));
            if (node is LiteralExpressionSyntax literal &&
                node?.Parent is AttributeArgumentSyntax argument && 
                node?.Parent?.Parent?.Parent is AttributeSyntax attribute)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var projectPath = string.IsNullOrEmpty(document.Project.FilePath) ? null : Path.GetDirectoryName(document.Project.FilePath);
                var symbol = semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol;
                if (symbol?.ContainingType.ToDisplayString() == "Android.App.ActivityAttribute" || 
                    (symbol == null && attribute.Name.ToString() == "Activity"))
                {
                    var name = argument.NameEquals.Name.ToString();
                    var kind = "string";
                    if (name == "Theme")
                        kind = "style";

                    var compilation = await document.Project.GetCompilationAsync(completionContext.CancellationToken);
                    var resourceDesignerAttribute = compilation.Assembly.GetAttributes().FirstOrDefault(attr 
                        => attr.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Android.Runtime.ResourceDesignerAttribute");

                    if (resourceDesignerAttribute != null && resourceDesignerAttribute.ConstructorArguments.Any())
                    {
                        var resourceDesigner = compilation.GetTypeByMetadataName((string)resourceDesignerAttribute.ConstructorArguments.First().Value);
                        if (resourceDesigner != null)
                        {
                            var resourceSymbol = resourceDesigner.GetTypeMembers().FirstOrDefault(x => x.Name.Equals(kind, StringComparison.OrdinalIgnoreCase));
                            if (resourceSymbol != null)
                            {
                                foreach (var member in resourceSymbol.GetMembers().Where(x => x.Kind == SymbolKind.Field))
                                {
                                    completionContext.AddItem(CompletionItem.Create(
                                        $"@{kind}/{member.Name}",
                                        literal.GetText().ToString(),
                                        $"@{kind}/{member.Name}",
                                        properties: ImmutableDictionary.Create<string, string>()
                                            .Add("Summary", member.GetDocumentationCommentXml())
                                            // Add the starting quote char to start position
                                            .Add("Start", (node.Span.Start + 1).ToString())
                                            // Remove the two quote characters
                                            .Add("Length", (node.Span.Length - 2).ToString()),
                                        tags: ImmutableArray.Create(WellKnownTags.Constant, "Xamarin"),
                                        rules: StandardCompletionRules));
                                }
                            }
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }
    }
}
