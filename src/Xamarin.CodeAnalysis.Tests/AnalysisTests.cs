using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using static Xamarin.CodeAnalysis.Tests.TestHelpers;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerClass, DisableTestParallelization = true)]

namespace Xamarin.CodeAnalysis.Tests
{
    public class AnalysisTests
    {
        [Theory]
        [InlineData(@"using Android.App;

[Activity(Label = ""Main"", MainLauncher = true)]
public class MainActivity : Activity
{
}
", typeof(XAA1001StringLiteralToResource))]
        [InlineData(@"using Android.App;

[Activity(Label = ""@string/foo"")]
public class MainActivity : Activity
{
}
", typeof(XAA1002ResourceIdentifierNotFound))]
        public async Task can_get_diagnostics(string code,  Type analyzerType)
        {
            var workspace = new AdhocWorkspace();
            var document = workspace
               .AddProject("TestProject", LanguageNames.CSharp)
               .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
               .WithMetadataReferences(Directory
                    .EnumerateFiles("MonoAndroid", "*.dll")
                    .Select(dll => MetadataReference.CreateFromFile(dll)))
               .AddDocument("Resource.designer.cs", @"[assembly: global::Android.Runtime.ResourceDesignerAttribute(""MyApp.Resource"", IsApplication=true)]
namespace MyApp
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute(""Xamarin.Android.Build.Tasks"", ""1.0.0.0"")]
    public partial class Resource
    {
        public partial class String
        {
            public const int app_name = 2130968578;
            public const int app_title = 2130968579;
        }
        public partial class Style
        {
            public const int AppTheme = 2131034114;
			public const int AppTheme_NoActionBar = 2131230723;
        }
        public partial class Drawable
        {
            public const int design_fab_background = 2131296343;
        }
        public partial class Mipmap
        {
            public const int ic_launcher = 2130837506;
        }
    }
}")
               .Project
               .AddDocument("TestDocument.cs", code.Replace("`", ""));

            var compilation = await document.Project.GetCompilationAsync(TimeoutToken(5));
            var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(analyzerType);
            var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
            var diagnostic = (await withAnalyzers.GetAnalyzerDiagnosticsAsync())
                .Where(d => analyzer.SupportedDiagnostics.Any(x => x.Id == d.Id))
                .OrderBy(d => d.Location.SourceSpan.Start)
                .FirstOrDefault() ?? throw new ArgumentException($"Analyzer did not produce diagnostic(s) {string.Join(", ", analyzer.SupportedDiagnostics.Select(d => d.Id))}.");

            // TODO: test code fix?
            //var actions = new List<CodeAction>();
            //var context = new CodeFixContext(document, diagnostic, (a, d) => actions.Add(a), TimeoutToken(5));
            //await new XAA1001CodeFixProvider().RegisterCodeFixesAsync(context);

            //var changed = actions
            //    .SelectMany(x => x.GetOperationsAsync(TimeoutToken(2)).Result)
            //    .OfType<ApplyChangesOperation>()
            //    .First()
            //    .ChangedSolution;

            //var changes = changed.GetChanges(document.Project.Solution);

        }
    }
}
