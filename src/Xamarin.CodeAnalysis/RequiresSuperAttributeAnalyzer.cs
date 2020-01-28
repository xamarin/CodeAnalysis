// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
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
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RequiresSuperAttribute
{
	/// <summary>
	/// Analyzer that reports diagnostics for all methods with the [RequiresSuper] attribute which are overridden without a base.[methodname]() call in the method body
	/// </summary>
	[DiagnosticAnalyzer (LanguageNames.CSharp)]
	public class RequiresSuperAttributeAnalyzer : DiagnosticAnalyzer
	{
		public static readonly string DiagnosticId = "XI0004";

		static readonly DiagnosticDescriptor Rule =
		new DiagnosticDescriptor (
			DiagnosticId,
			"Add base method call to methods with the [RequiresSuper] attribute",
			"Overriding the {0} method requires a call to the overridden method",
			"Notifications",
			DiagnosticSeverity.Info,
			isEnabledByDefault: true
			);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create (Rule);

		const string RequiresSuperAttributeName = "RequiresSuperAttribute";
		const string RequiresSuperAttributeNamespace = "ObjCRuntime";

		public override void Initialize (AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis (GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution ();
			context.RegisterOperationBlockAction ((codeBlockContext) => {
				// returns if the code block isn't an overridden method
				var method = (IMethodSymbol)codeBlockContext.OwningSymbol;
				if (method == null || !method.IsOverride) { return; }

				// seeing if the [RequiresSuper] attribute is there
				var baseType = method.ContainingType.BaseType;
				ISymbol baseMethod = baseType.GetMembers (method.Name).FirstOrDefault ();
				// returns if the [RequiresSuper] attribute isn't present
				if (!baseMethod.GetAttributes ().Any (attr => (attr.AttributeClass.Name == RequiresSuperAttributeName && attr.AttributeClass.ContainingNamespace.Name == RequiresSuperAttributeNamespace))) {
					return;
				}

				foreach (var block in codeBlockContext.OperationBlocks.Where ((b) => b.Kind == OperationKind.Block)) // loops once per method
				{
					foreach (var exp in block.Children) // loops once per operation in the method body 
					{
						//skips if the operation isn't base.___ or return ___; 
						if (exp.Kind != OperationKind.ExpressionStatement && exp.Kind != OperationKind.Return) { continue; }


						foreach (var child in exp.Children) {
							// skips if it's not an Invocation Operation
							if (child.Kind != OperationKind.Invocation) { continue; }
							var invocationOp = (IInvocationOperation)child;
							var instance = invocationOp.Instance;
							if (instance == null) { return; } // FIXME: this is the void foo () => . . .case; make this register a diagnostic (the code fix currently throws an error if the diagnostic is registered)

							// skips if the operation doesn't contain base
							if (instance.Kind != OperationKind.InstanceReference) { continue; }
							var instanceOp = (IInstanceReferenceOperation)instance;

							// checks if in the base.[method]() call, [method] matches the one in the method's signature
							if (instanceOp.ReferenceKind != InstanceReferenceKind.ContainingTypeInstance) { continue; }

							var invokedMethod = invocationOp.TargetMethod;
							var containingMethod = (IMethodSymbol)codeBlockContext.OwningSymbol;

							var iter = containingMethod;
							while (iter != null) {
								iter = iter.OverriddenMethod;
								if (invokedMethod.Equals (iter)) {
									return;
								}
							}
						}
					}
				}
				//adds the diagnostic if we don't find base.method() or return base.method()
				var location = codeBlockContext.OwningSymbol.Locations.FirstOrDefault ();
				if (location == null) { return; }
				var diagnostic = Diagnostic.Create (Rule, location, method.Name);
				codeBlockContext.ReportDiagnostic (diagnostic);
			});
		}

	}
}
