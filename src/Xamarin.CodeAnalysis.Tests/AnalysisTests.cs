using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
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
", XAA1001StringLiteralToResource.DiagnosticId)]
        public async Task can_get_diagnostics(string code, string diagnosticId)
        {
            var workspace = new AdhocWorkspace();
            var document = workspace
               .AddProject("TestProject", LanguageNames.CSharp)
               .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
               .WithMetadataReferences(Directory
                    .EnumerateFiles("MonoAndroid", "*.dll")
                    .Select(dll => MetadataReference.CreateFromFile(dll)))
               .AddAdditionalDocument("strings.xml", @"<?xml version='1.0' encoding='utf-8'?>
<resources>
    <string name='app_name'>TestApp</string>
</resources>", new[] { "Resources", "values" }, "Resources\\values\\strings.xml")
               .Project
               .AddAdditionalDocument("styles.xml", @"<?xml version='1.0' encoding='utf-8'?>
<resources>
    <style name='AppTheme' parent='Theme.AppCompat.Light.DarkActionBar' />
</resources>", new[] { "Resources", "values" }, "Resources\\values\\styles.xml")
               .Project
               .AddDocument("TestDocument.cs", code.Replace("`", ""));

            var compilation = await document.Project.GetCompilationAsync(TimeoutToken(5));
            var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new XAA1001StringLiteralToResource()));
            var diagnostic = (await withAnalyzers.GetAnalyzerDiagnosticsAsync())
                .Where(d => d.Id == diagnosticId)
                .OrderBy(d => d.Location.SourceSpan.Start)
                .FirstOrDefault() ?? throw new ArgumentException($"Analyzer did not produce diagnostic {diagnosticId}.");

            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, diagnostic, (a, d) => actions.Add(a), TimeoutToken(5));
            await new XAA1001CodeFixProvider().RegisterCodeFixesAsync(context);

            var changed = actions
                .SelectMany(x => x.GetOperationsAsync(TimeoutToken(2)).Result)
                .OfType<ApplyChangesOperation>()
                .First()
                .ChangedSolution;

            var changes = changed.GetChanges(document.Project.Solution);

        }
    }
}
