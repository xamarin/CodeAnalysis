using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Language.Xml;
using Xamarin.CodeAnalysis.Properties;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Xamarin.CodeAnalysis.LocalizableString;

namespace Xamarin.CodeAnalysis
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class XAA1001StringLiteralToResource : DiagnosticAnalyzer
    {
        /// <summary>
        /// The ID for diagnostics produced by the <see cref="XAA1001StringLiteralToResource"/> analyzer.
        /// </summary>
        public const string DiagnosticId = "XAA1001";

        const string HelpLink = "https://github.com/xamarin/CodeAnalysis/blob/master/docs/XAA1001.md";

        static readonly DiagnosticDescriptor Descriptor =
            new DiagnosticDescriptor(DiagnosticId, 
                Localizable(nameof(Resources.XAA1001_Title)),
                Localizable(nameof(Resources.XAA1001_MessageFormat)), 
                Constants.AnalyzerCategory, 
                Microsoft.CodeAnalysis.DiagnosticSeverity.Info, 
                true, 
                Localizable(nameof(Resources.XAA1001_Description)), 
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
                context.Node.Parent is AttributeArgumentSyntax &&
                context.Node.Parent?.Parent?.Parent is AttributeSyntax &&
                !literal.GetText().ToString().StartsWith("@"))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, context.Node.GetLocation()));
            }
        }
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(XAA1001CodeFixProvider))]
    [Shared]
    internal class XAA1001CodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(XAA1001StringLiteralToResource.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => null;

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Resources.XAA1001_Title,
                        cancellation => CreateChangedSolutionAsync(context, diagnostic, cancellation),
                        nameof(XAA1001CodeFixProvider)),
                    diagnostic);
            }

            return Task.CompletedTask;
        }


        private async Task<Solution> CreateChangedSolutionAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken cancellation)
        {
            var root = await context.Document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);

            var literal = (LiteralExpressionSyntax)token.Parent;
            var argument = literal.FirstAncestorOrSelf<AttributeArgumentSyntax>();

            var resourceDoc = default(TextDocument);
            if (argument.NameEquals.Name.ToString() == "Label")
                resourceDoc = context.Document.Project.AdditionalDocuments.FirstOrDefault(doc => doc.FilePath.EndsWith(@"Resources\values\strings.xml"));

            // Potentially support moving resources to other files?

            if (resourceDoc == null)
                return null;

            var declaration = literal.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            var identifier = new StringBuilder();
            foreach (var c in declaration.Identifier.ValueText)
            {
                if (char.IsUpper(c))
                {
                    if (identifier.Length > 0)
                        identifier = identifier.Append("_");

                    identifier.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    identifier.Append(c);
                }
            }

            var key = identifier.Append("_").Append(argument.NameEquals.Name.ToString().ToLowerInvariant()).ToString();

            var documentSyntax = Parser.ParseText((await resourceDoc.GetTextAsync()).ToString());
            // the XML mutation model is a bit cumbersome, and the factory API is very hard to follow, it's easy to 
            // loose track of what you're building. So we parse a new doc instead and append that to the previous one.
            var elementSyntax = Parser.ParseText("\r\n\t" + new XElement("string", new XAttribute("name", key), literal.ToString().TrimStart('"').TrimEnd('"')).ToString());

            var newXml = documentSyntax.RootSyntax.AddChild(elementSyntax.RootSyntax);
            var docNode = SyntaxFactory.XmlDocument(
                documentSyntax.Prologue, documentSyntax.PrecedingMisc, 
                newXml.AsNode, documentSyntax.FollowingMisc,
                documentSyntax.SkippedTokens, documentSyntax.Eof);
            
            var text = await resourceDoc.GetTextAsync(cancellation);
            var newDoc = context.Document.Project.Solution
                .WithAdditionalDocumentText(resourceDoc.Id, SourceText.From(
                    docNode.ToFullString(), text.Encoding))
                .GetProject(context.Document.Project.Id)
                .GetDocument(context.Document.Id);

            var generator = SyntaxGenerator.GetGenerator(newDoc);
            newDoc = newDoc.WithSyntaxRoot(generator.ReplaceNode(
                await newDoc.GetSyntaxRootAsync(), 
                literal, 
                literal.WithToken(Literal($"@string/{key}"))));

            return newDoc.Project.Solution;
        }
    }
}
