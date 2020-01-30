// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
//
// RequiresSuperAttributeCodeFixProvider.cs:
//
// Authors:
//   Madeline Zhang  <v-madzh@microsoft.com>
//
// Copyright 2020 Xamarin Inc.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;


namespace RequiresSuperAttribute
{
	[ExportCodeFixProvider (LanguageNames.CSharp, Name = nameof (RequiresSuperAnalyzerCodeFixProvider)), Shared]
	public class RequiresSuperAnalyzerCodeFixProvider : CodeFixProvider
	{
		const string Title = "Add base method call";

		public sealed override ImmutableArray<string> FixableDiagnosticIds {
			get { return ImmutableArray.Create (RequiresSuperAttributeAnalyzer.DiagnosticId); }
		}

		public sealed override FixAllProvider GetFixAllProvider () => WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync (CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync (context.CancellationToken).ConfigureAwait (false);
			var diagnostic = context.Diagnostics.First ();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// Find the method declaration identified by the diagnostic.
			var methodDeclaration =
				root.FindToken (diagnosticSpan.Start).Parent.AncestorsAndSelf ()
				.OfType<MethodDeclarationSyntax> ().First ();

			// Register a code action that will invoke the fix.
			context.RegisterCodeFix (CodeAction.Create (Title, c => FixSuperAsync (context.Document, methodDeclaration, c), equivalenceKey: Title), diagnostic);
		}

		async Task<Document> FixSuperAsync (Document document,
		  MethodDeclarationSyntax methodDeclaration,
		  CancellationToken cancellationToken)
		{
			//Find the node to fix.
			var semanticModel = await document.GetSemanticModelAsync (cancellationToken).ConfigureAwait (false);
			var memberSymbol = semanticModel.GetDeclaredSymbol (methodDeclaration) as IMethodSymbol;
            if (memberSymbol == null)
                return document;

			//Generate the new base.[methodName]([arguments]) block. If the method needs to return the base call,
			//it generates return base.[methodName]([arguments])
			// TODO: this might be hacky - (.Add(SyntaxFactory.Tab) adds an extra tab so that it's a tab (or SyntaxFactory.Tab) further than the leading
			//      trivia of the method body block, but we should consider investigating if this behaves as expected when the IDE is set to spaces vs
			//      tabs / different numbers of spaces per tab
			var returnString = memberSymbol.ReturnsVoid ? "" : "return ";
<<<<<<< HEAD
			var parametersString = string.Join (",", memberSymbol.Parameters.Select ((p) => p.Name)); //creates a string of the method's passed-in arguments separated by commas
			var newLiteral = SyntaxFactory.ParseStatement (returnString + "base." + memberSymbol.Name + "(" + parametersString + ");")
=======
			var parametersString = string.Join (",", memberSymbol.Parameters.Select ((p) => p.Name).ToArray ()); //creates a string of the method's passed-in arguments separated by commas
			var newLiteral = SyntaxFactory.ParseStatement ($"{returnString} base.{memberSymbol.Name} ({parametersString});")
>>>>>>> c0eec4abacee0fcb234d420a88f6ecb0a1dbe909
			  .WithLeadingTrivia (methodDeclaration.Body.GetLeadingTrivia ().Add (SyntaxFactory.Tab))
			  .WithTrailingTrivia (methodDeclaration.Body.GetTrailingTrivia ())
			  .WithAdditionalAnnotations (Formatter.Annotation);
			BlockSyntax blockLiteralWithBraces = SyntaxFactory.Block (newLiteral);

			//Get rid of the extra { } braces created by the block creation.
			//TODO: this is hacky ({ } braces are automatically added when you make a new BlockSyntax, and this gets of them but still keeps them in the syntax
			//      tree. We should consider investigating how to do use something else rather than blocks, or how to create a block without enclosing braces.
			BlockSyntax blockLiteral = blockLiteralWithBraces.WithOpenBraceToken (SyntaxFactory.MissingToken (SyntaxKind.OpenBraceToken))
				.WithCloseBraceToken (SyntaxFactory.MissingToken (SyntaxKind.CloseBraceToken));

			//Add new block to the pre-existing block.
			BlockSyntax methodDeclarationBlock = methodDeclaration.Body.AddStatements (blockLiteral);

			//Swap new node into syntax tree.
			var root = await document.GetSyntaxRootAsync ().ConfigureAwait (false);
			var newRoot = root.ReplaceNode (methodDeclaration.Body, methodDeclarationBlock);
			var newDocument = document.WithSyntaxRoot (newRoot);

			return newDocument;
		}
	}
}
