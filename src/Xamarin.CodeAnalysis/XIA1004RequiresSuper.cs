// Copyright (c) Microsoft.  All Rights Reserved.
//
// RequiresSuperAttributeAnalyzer.cs:
//
// Authors:
//   Madeline Zhang  <v-madzh@microsoft.com>
//
// Copyright 2020 Xamarin Inc.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Formatting;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.CodeAnalysis

{
    /// <summary>
    /// Analyzer that reports diagnostics for all methods with the [RequiresSuper] attribute which are overridden without a base.[methodname]() call in the method body
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class XIA1004RequiresSuper : DiagnosticAnalyzer
    {
        public static readonly string DiagnosticId = "XI0004RequiresSuperAttribute";

        static readonly DiagnosticDescriptor Rule =
        new DiagnosticDescriptor(
            DiagnosticId,
            "Add base method call to methods with the [RequiresSuper] attribute",
            "Overriding the {0} method requires a call to the overridden method",
            "Notifications",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true
            );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        const string RequiresSuperAttributeName = "RequiresSuperAttribute";
        const string RequiresSuperAttributeNamespace = "ObjCRuntime";

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationBlockAction((codeBlockContext) =>
            {
                // returns if the code block isn't an overridden method
                var method = codeBlockContext.OwningSymbol as IMethodSymbol;
                if (method == null || !method.IsOverride)
                    return;

                // seeing if the [RequiresSuper] attribute is there
                var baseType = method.ContainingType.BaseType;
                ISymbol baseMethod = method.OverriddenMethod;
                // returns if the [RequiresSuper] attribute isn't present
                if (!baseMethod.GetAttributes().Any(attr => (attr.AttributeClass.Name == RequiresSuperAttributeName && attr.AttributeClass.ContainingNamespace.Name == RequiresSuperAttributeNamespace)))
                    return;

                foreach (var block in codeBlockContext.OperationBlocks.Where((b) => b.Kind == OperationKind.Block)) // loops once per method
                {
                    foreach (var exp in block.Children) // loops once per operation in the method body
                    {
                        //skips if the operation isn't base.___ or return ___;
                        if (exp.Kind != OperationKind.ExpressionStatement && exp.Kind != OperationKind.Return)
                            continue;

                        foreach (var child in exp.Children)
                        {
                            // skips if it's not an Invocation Operation
                            if (child.Kind != OperationKind.Invocation)
                                continue;
                            var invocationOp = (IInvocationOperation)child;
                            var instance = invocationOp.Instance;
                            if (instance == null) // FIXME: this is the void foo () => . . .case; make this register a diagnostic (the code fix currently throws an error if the diagnostic is registered)
                                return;
                            // skips if the operation doesn't contain base
                            if (instance.Kind != OperationKind.InstanceReference)
                                continue;
                            var instanceOp = (IInstanceReferenceOperation)instance;

                            // checks if in the base.[method]() call, [method] matches the one in the method's signature
                            if (instanceOp.ReferenceKind != InstanceReferenceKind.ContainingTypeInstance)
                                continue;
                            var invokedMethod = invocationOp.TargetMethod;
                            if (invokedMethod.Equals(method.OverriddenMethod))
                            {
                                return;
                            }
                        }
                    }
                }
                //adds the diagnostic if we don't find base.method() or return base.method()
                var location = codeBlockContext.OwningSymbol.Locations.FirstOrDefault();
                if (location == null)
                    return;
                var diagnostic = Diagnostic.Create(Rule, location, method.Name);
                codeBlockContext.ReportDiagnostic(diagnostic);
            });
        }

    }
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(XIA1004CodeFixProvider))]
    [Shared]
    internal class XIA1004CodeFixProvider : CodeFixProvider
    {
        const string Title = "Add base method call";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(XIA1004RequiresSuper.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
                return;
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the method declaration identified by the diagnostic.
            var methodDeclaration =
                root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (methodDeclaration == null)
                return;

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(CodeAction.Create(Title, c => FixSuperAsync(context.Document, methodDeclaration, c), equivalenceKey: Title), diagnostic);
        }

        async Task<Document> FixSuperAsync(Document document,
          MethodDeclarationSyntax methodDeclaration,
          CancellationToken cancellationToken)
        {
            //Find the node to fix.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var memberSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;
            if (memberSymbol == null)
                return document;

            //Generate the new base.[methodName]([arguments]) block. If the method needs to return the base call,
            //it generates return base.[methodName]([arguments])
            // TODO: this might be hacky - (.Add(SyntaxFactory.Tab) adds an extra tab so that it's a tab (or SyntaxFactory.Tab) further than the leading
            //      trivia of the method body block, but we should consider investigating if this behaves as expected when the IDE is set to spaces vs
            //      tabs / different numbers of spaces per tab
            var returnString = memberSymbol.ReturnsVoid ? "" : "return ";
            if (memberSymbol == null)
                return document;
            var parametersString = string.Join(",", memberSymbol.Parameters.Select((p) => p.Name)); //creates a string of the method's passed-in arguments separated by commas
            var newLiteral = SyntaxFactory.ParseStatement($"{returnString} base.{memberSymbol.Name} ({parametersString});")
              .WithLeadingTrivia(methodDeclaration.Body.GetLeadingTrivia().Add(SyntaxFactory.Tab))
              .WithTrailingTrivia(methodDeclaration.Body.GetTrailingTrivia())
              .WithAdditionalAnnotations(Formatter.Annotation);
            BlockSyntax blockLiteralWithBraces = SyntaxFactory.Block(newLiteral);

            //Get rid of the extra { } braces created by the block creation.
            //TODO: this is hacky ({ } braces are automatically added when you make a new BlockSyntax, and this gets of them but still keeps them in the syntax
            //      tree. We should consider investigating how to do use something else rather than blocks, or how to create a block without enclosing braces.
            BlockSyntax blockLiteral = blockLiteralWithBraces.WithOpenBraceToken(SyntaxFactory.MissingToken(SyntaxKind.OpenBraceToken))
                .WithCloseBraceToken(SyntaxFactory.MissingToken(SyntaxKind.CloseBraceToken));

            //Add new block to the pre-existing block.
            BlockSyntax methodDeclarationBlock = methodDeclaration.Body.AddStatements(blockLiteral);

            //Swap new node into syntax tree.
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(methodDeclaration.Body, methodDeclarationBlock);
            var newDocument = document.WithSyntaxRoot(newRoot);

            return newDocument;
        }
    }
}
