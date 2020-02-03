using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Xamarin.CodeAnalysis.Apple
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AttributeAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string AdviceDiagnosticId = "XIA1001";
        public const string IntroducedDiagnosticId = "XIA1002";
        public const string DeprecatedDiagnosticId = "XIA1003";

        const string AdviceHelpLink = "https://github.com/xamarin/CodeAnalysis/blob/master/docs/XIA1001.md";
        const string IntroducedHelpLink = "https://github.com/xamarin/CodeAnalysis/blob/master/docs/XIA1002.md";
        const string DeprecatedHelpLink = "https://github.com/xamarin/CodeAnalysis/blob/master/docs/XIA1003.md";

        const string ConfigFileName = "Info.plist";
        const string Category = "Notifications";

        static readonly DiagnosticDescriptor adviceRule = new DiagnosticDescriptor(
            AdviceDiagnosticId,
            "Notifies you with advice on how to use Apple APIs",
            "{0}",
            Category,
            DiagnosticSeverity.Info,
            true,
            AdviceHelpLink
        );

        static readonly DiagnosticDescriptor introducedRule = new DiagnosticDescriptor(
            IntroducedDiagnosticId,
            "Notifies you if you are using newer Apple APIs when targeting an older OS version",
            "{0}",
            Category,
            DiagnosticSeverity.Info,
            true,
            "This rule is comparing the versions of the API's introduced attribute and the minimum deployment target defined in the Info.plist.",
            IntroducedHelpLink
        );

        static readonly DiagnosticDescriptor deprecatedRule = new DiagnosticDescriptor(
            DeprecatedDiagnosticId,
            "Notifies you when using a deprecated, obsolete or unavailable Apple API",
            "{0}",
            Category,
            DiagnosticSeverity.Info,
            true,
            DeprecatedHelpLink
        );

        static readonly ImmutableArray<OperationKind> operationKindsOfInterest = ImmutableArray.Create(
            OperationKind.EventReference,
            OperationKind.FieldReference,
            OperationKind.Invocation,
            OperationKind.MethodReference,
            OperationKind.PropertyReference,
            OperationKind.ObjectCreation
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(adviceRule, introducedRule, deprecatedRule);

        const string AdviceAttribute = "Foundation.AdviceAttribute";

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        PlatformName GetPlatform(IEnumerable<AssemblyIdentity> referencedAssemblyNames)
        {
            foreach (var element in referencedAssemblyNames)
            {
                switch (element.Name)
                {
                    case "Xamarin.iOS":
                        return PlatformName.iOS;
                    case "Xamarin.WatchOS":
                        return PlatformName.WatchOS;
                    case "Xamarin.TVOS":
                        return PlatformName.TvOS;
                    case "Xamarin.Mac":
                        return PlatformName.MacOSX;
                }
            }
            return PlatformName.None;
        }

        void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            AdditionalText infoPlistFilePath = compilationContext.Options.AdditionalFiles.FirstOrDefault(f => Path.GetFileName(f.Path) == ConfigFileName);

            if (infoPlistFilePath == null)
                return;

            var adviceAttribute = compilationContext.Compilation.GetTypeByMetadataName(AdviceAttribute);
            if (adviceAttribute == null)
                return;

            var referencedAssemblyNames = compilationContext.Compilation.ReferencedAssemblyNames;

            PlatformName platformType = GetPlatform(referencedAssemblyNames);

            compilationContext.RegisterOperationAction(
                (nodeContext) =>
                {

                    var symbol = nodeContext.Operation.GetSymbol();
                    if (symbol == null)
                        return;

                    if (!TryGetSymbolMergedAttributes(nodeContext, symbol, adviceAttribute, out PlatformAvailability availability) &&
                !TryGetSymbolMergedAttributes(nodeContext, symbol.ContainingType, adviceAttribute, out availability))
                        return;

                    var message = availability.GetDeprecatedMessage(platformType);

                    Version minimumOSVersion = GetMinimumDeploymentTargetNumber(infoPlistFilePath);

                    bool introduced = false;
                    if (message == null)
                    {
                        message = availability.GetMinimumRequiredPlatformMessage(minimumOSVersion, platformType);
                        introduced = true;
                    }

                    if (message == null)
                        return;

                    if (availability.Message != null)
                        message = string.Format("{0}.\n {1}", message, availability.Message);

                    AddIssue(nodeContext, introduced ? introducedRule : deprecatedRule, string.Format("'{0}' {1}", symbol.Name, message));
                },
                operationKindsOfInterest
            );
        }

        bool TryGetSymbolMergedAttributes(OperationAnalysisContext context, ISymbol symbol, INamedTypeSymbol adviceAttribute, out PlatformAvailability availability)
        {
            availability = null;

            MergeSymbolAttributes(context, symbol, adviceAttribute, ref availability);

            if (symbol is IPropertySymbol property)
            {
                var syntax = context.Operation.Syntax;
                var parent = syntax?.Parent;
                IMethodSymbol method = null;

                switch (parent)
                {
                    case AssignmentExpressionSyntax assignment:
                        method = (assignment.Right == syntax) ? property.GetMethod : property.SetMethod;
                        break;
                    case PostfixUnaryExpressionSyntax _:
                    case PrefixUnaryExpressionSyntax _:
                        if ((method = property.GetMethod) != null)
                            MergeSymbolAttributes(context, method, adviceAttribute, ref availability);

                        method = property.SetMethod;
                        break;
                    case ElementAccessExpressionSyntax _:
                    case ArgumentSyntax _:
                        method = property.GetMethod;
                        break;
                }

                if (method != null)
                    MergeSymbolAttributes(context, method, adviceAttribute, ref availability);
            }

            return availability != null && availability.IsSpecified;
        }

        void MergeSymbolAttributes(OperationAnalysisContext context, ISymbol symbol, INamedTypeSymbol adviceAttribute, ref PlatformAvailability availability)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass == adviceAttribute && attr.ConstructorArguments.Length > 0)
                {
                    AddIssue(context, adviceRule, attr.ConstructorArguments[0].Value?.ToString());
                }
                else
                {
                    availability = availability.Merge(attr);
                }
            }
        }

        void AddIssue(OperationAnalysisContext context, DiagnosticDescriptor rule, string message) => context.ReportDiagnostic(Diagnostic.Create(rule, context.Operation.Syntax.GetLocation(), message));

        static string GetMinimumOSVersionLine(AdditionalText infoPlistFilePath)
        {
            PDictionary dict = PDictionary.FromFile(infoPlistFilePath.Path.ToString());

            if (dict.TryGetValue("MinimumOSVersion", out PString value) || dict.TryGetValue("LSMinimumSystemVersion", out value))
                return value.Value;

            return String.Empty; // should never reach here; placeholder value just in case
        }

        static Version GetMinimumDeploymentTargetNumber(AdditionalText infoPlistFilePath)
        {
            var minimumOSNumber = GetMinimumOSVersionLine(infoPlistFilePath);

            Version.TryParse(minimumOSNumber, out Version vers);
            return vers;
        }
    }
}
