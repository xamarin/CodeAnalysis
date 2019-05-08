using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;

namespace Xamarin.CodeAnalysis
{
    [ExportCompletionProvider(nameof(ResourceCompletionProvider), LanguageNames.CSharp)]
	public class ResourceCompletionProvider : CompletionProvider
	{
		private static readonly CompletionItemRules StandardCompletionRules = CompletionItemRules.Default;
		//.WithSelectionBehavior(CompletionItemSelectionBehavior.SoftSelection);

		public override bool ShouldTriggerCompletion(SourceText text, int caretPosition, CompletionTrigger trigger, OptionSet options)
		{
			if (trigger.Kind == CompletionTriggerKind.Invoke ||
				trigger.Kind == CompletionTriggerKind.InvokeAndCommitIfUnique)
			{
				return true;
			}

			if (trigger.Kind == CompletionTriggerKind.Insertion)
			{
				return trigger.Character == '"';
			}
			else if (trigger.Kind == CompletionTriggerKind.Deletion &&
				caretPosition > 0 &&
				text.GetSubText(TextSpan.FromBounds(caretPosition - 1, caretPosition)).ToString() == "\"")
			{
				return true;
			}

			return base.ShouldTriggerCompletion(text, caretPosition, trigger, options);
		}

		public override Task<CompletionDescription> GetDescriptionAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
			=> Task.FromResult(CompletionDescription.FromText(item.Properties["Summary"]));

		public override Task<CompletionChange> GetChangeAsync(Document document, CompletionItem item, char? commitKey, CancellationToken cancellationToken)
		{
			if (item.Properties.TryGetValue("Start", out var start) &&
				int.TryParse(start, out var s) &&
				item.Properties.TryGetValue("Length", out var length) &&
				int.TryParse(length, out var l))
			{
				return Task.FromResult(CompletionChange.Create(new TextChange(new TextSpan(s, l), item.SortText)));
			}

			return base.GetChangeAsync(document, item, commitKey, cancellationToken);
		}

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
				// TODO: we support only property syntax for completion in attributes, 
				// since that's what XA has right now anyway, and we need to lookup the 
				// [Category] attribute on the property.
				argument.NameEquals != null &&
				node?.Parent?.Parent?.Parent is AttributeSyntax attribute)
			{
				var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
				var projectPath = string.IsNullOrEmpty(document.Project.FilePath) ? null : Path.GetDirectoryName(document.Project.FilePath);
				// From the attribute syntax, we'll get the constructor for the attribute
				var symbol = (semanticModel.GetSymbolInfo(attribute, cancellationToken).Symbol as IMethodSymbol)?.ContainingType;
				if (symbol?.ContainingNamespace.ToDisplayString() == "Android.App")
				{
					var name = argument.NameEquals.Name.ToString();
					// First try explicit completion hint via Category
					var propertyInfo = symbol.GetMembers(name).FirstOrDefault() as IPropertySymbol;
					if (propertyInfo == null)
						return;

					string[] categories;
					var categoryAttr = propertyInfo.GetAttributes().FirstOrDefault(attr => attr.AttributeClass.ToDisplayString() == typeof(CategoryAttribute).FullName);
					if (categoryAttr == null)
					{
						// Apply default heuristics based on member name
						// Label/Description: @string
						// Icon/RoundIcon: @drawable; @mipmap
						// Theme: @style
						if (name == "Label" || name == "Description")
							categories = new[] { "string" };
						else if (name == "Icon" || name == "RoundIcon")
							categories = new[] { "drawable", "mipmap" };
						else if (name == "Theme")
							categories = new[] { "style" };
						else
							return;
					}
					else if (!categoryAttr.ConstructorArguments.IsDefaultOrEmpty)
					{
						categories = ((string)categoryAttr.ConstructorArguments[0].Value)
							.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
							.Select(s => s.Trim().TrimStart('@'))
							.ToArray();
					}
					else
					{
						return;
					}

					var compilation = await document.Project.GetCompilationAsync(completionContext.CancellationToken);
					var resourceDesignerAttribute = compilation.Assembly.GetAttributes().FirstOrDefault(attr
						=> attr.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Android.Runtime.ResourceDesignerAttribute");

					if (resourceDesignerAttribute != null && resourceDesignerAttribute.ConstructorArguments.Any())
					{
						var resourceDesigner = compilation.GetTypeByMetadataName((string)resourceDesignerAttribute.ConstructorArguments.First().Value);
						if (resourceDesigner != null)
						{
							foreach (var category in categories)
							{
								var resourceSymbol = resourceDesigner.GetTypeMembers().FirstOrDefault(x => x.Name.Equals(category, StringComparison.OrdinalIgnoreCase));
								if (resourceSymbol != null)
								{
									foreach (var member in resourceSymbol.GetMembers().Where(x => x.Kind == SymbolKind.Field))
									{
										completionContext.AddItem(CompletionItem.Create(
											displayText: member.Name,
											displayTextPrefix: $"@{category}/",
											sortText: $"@{category}/{member.Name}",
											properties: ImmutableDictionary.Create<string, string>()
												 .Add("Summary", member.GetDocumentationCommentXml())
												// Add the starting quote char to start position
												.Add("Start", (node.Span.Start + 1).ToString())
												// Remove the two quote characters
												.Add("Length", (node.Span.Length - 2).ToString()),
											tags: ImmutableArray.Create(WellKnownTags.Constant, "Xamarin"),
											rules: CompletionItemRules.Default));
									}
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
