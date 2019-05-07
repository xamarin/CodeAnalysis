using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RefactoringEssentials;

namespace Xamarin.CodeAnalysis {
    
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class XAA1002ResourceIdentifierNotFound : DiagnosticAnalyzer
    {
        /// <summary>
        /// The ID for diagnostics produced by the <see cref="XAA1001StringLiteralToResource"/> analyzer.
        /// </summary>
        public const string DiagnosticId = "XAA1002";

        const string HelpLink = "https://github.com/xamarin/CodeAnalysis/blob/master/docs/XAA1002.md";

        static readonly DiagnosticDescriptor Descriptor =
            new DiagnosticDescriptor(DiagnosticId,
				"Resource id must exist in a resource file", //"Resources.XAA1002_Title",
				"No resource found with identifier '{0}'.", //"Resources.XAA1002_MessageFormat",
				DiagnosticAnalyzerCategories.Notifications,
				DiagnosticSeverity.Error, 
                true,
				"Resource identifier must exist in an Android resource file.", //"Resources.XAA1002_Description", 
                HelpLink);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(AnalyzeLiteral, Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression);
        }

        void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is LiteralExpressionSyntax literal &&
                context.Node.Parent is AttributeArgumentSyntax argument &&
                // TODO: we support only property assignment in attributes we know 
                // about, we want to be conservative in the errors we report for now.
                argument.NameEquals != null &&
                context.Node.Parent?.Parent?.Parent is AttributeSyntax attribute &&
                literal.GetText().ToString().Trim('"') is string value && 
                value.StartsWith("@") &&
                value.IndexOf('/') is int slash && 
                slash != -1)
            {
                var category = value.Substring(1, slash - 1);
                var identifier = value.Substring(slash + 1).Replace('.', '_');

                var compilation = context.Compilation;
                var resourceDesignerAttribute = compilation.Assembly.GetAttributes().FirstOrDefault(attr => 
                    attr.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Android.Runtime.ResourceDesignerAttribute");

                if (resourceDesignerAttribute != null && resourceDesignerAttribute.ConstructorArguments.Any())
                {
                    var resourceDesigner = compilation.GetTypeByMetadataName((string)resourceDesignerAttribute.ConstructorArguments.First().Value);
                    if (resourceDesigner != null)
                    {
                        var resourceSymbol = resourceDesigner.GetTypeMembers().FirstOrDefault(x => x.Name.Equals(category, StringComparison.OrdinalIgnoreCase));
                        if (resourceSymbol != null)
                        {
                            var member = resourceSymbol.GetMembers(identifier).FirstOrDefault();
                            if (member == null)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(Descriptor, context.Node.GetLocation(), value));
                            }
                        }
                        else
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Descriptor, context.Node.GetLocation(), value));
                        }
                    }
                }
            }
        }
    }
}
