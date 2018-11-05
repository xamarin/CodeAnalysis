using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Xunit;

namespace Xamarin.CodeAnalysis.Tests
{
    public class CompletionTests
    {
        [Theory]
        [InlineData(@"using Android.App;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;

[Activity(Label = ""`"", MainLauncher = true)]
public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
{
    public bool OnNavigationItemSelected(IMenuItem menuItem) => true;
}
", "@string/app_name")]
        public async Task can_retrieve_completion(string code, string completion)
        {
            var hostServices = MefHostServices.Create(MefHostServices.DefaultAssemblies.Concat(
                new[]
                {
                    typeof(CompletionService).Assembly,
                    typeof(ResourceCompletionProvider).Assembly,
                }));

            var workspace = new AdhocWorkspace(hostServices);
            var document = workspace
               .AddProject("TestProject", LanguageNames.CSharp)
               .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
               .WithMetadataReferences(new MetadataReference[]
               {
                   MetadataReference.CreateFromFile(ThisAssembly.Metadata.NETStandardReference),
                   MetadataReference.CreateFromFile("Xamarin.CodeAnalysis.dll"),
                   MetadataReference.CreateFromFile("Xamarin.CodeAnalysis.Completion.dll"),
               })
               .AddAdditionalDocument("strings.xml", @"<?xml version='1.0' encoding='utf-8'?>
<resources>
    <string name='app_name'>TestApp</string>
</resources>", new[] { "Resources\\values" }, "Resources\\values\\strings.xml")
               .Project
               .AddAdditionalDocument("styles.xml", @"<?xml version='1.0' encoding='utf-8'?>
<resources>
    <style name='AppTheme' parent='Theme.AppCompat.Light.DarkActionBar' />
</resources>", new[] { "Resources\\values" }, "Resources\\values\\styles.xml")
               .Project
               .AddDocument("TestDocument.cs", code.Replace("`", ""));

            var service = CompletionService.GetService(document);
            Assert.NotNull(service);

            var caret = code.IndexOf('`');
            Assert.NotEqual(-1, caret);

            var completions = await service.GetCompletionsAsync(document, caret);

            Assert.NotNull(completions);
            Assert.Contains(completions.Items, x => x.Tags.Contains("Xamarin"));
            Assert.Contains(completions.Items, x => x.DisplayText == completion);
        }

        [Theory]
        [InlineData(@"using System;
public class Foo 
{
    public void Do() 
    {
        Console.`WriteLine("""");
    }
}")]
        public async Task does_not_trigger_completion(string code)
        {
            var hostServices = MefHostServices.Create(MefHostServices.DefaultAssemblies.Concat(
                new[]
                {
                    typeof(CompletionService).Assembly,
                    typeof(ResourceCompletionProvider).Assembly,
                }));

            var workspace = new AdhocWorkspace(hostServices);
            var document = workspace
               .AddProject("TestProject", LanguageNames.CSharp)
               .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
               .WithMetadataReferences(new MetadataReference[]
               {
                   MetadataReference.CreateFromFile(ThisAssembly.Metadata.NETStandardReference),
                   MetadataReference.CreateFromFile("Xamarin.CodeAnalysis.dll"),
                   MetadataReference.CreateFromFile("Xamarin.CodeAnalysis.Completion.dll"),
               })
               .AddDocument("TestDocument.cs", code.Replace("`", ""));

            var service = CompletionService.GetService(document);
            Assert.NotNull(service);

            var caret = code.IndexOf('`');
            Assert.NotEqual(-1, caret);

            var completions = await service.GetCompletionsAsync(document, caret);

            Assert.NotNull(completions);
            Assert.DoesNotContain(completions.Items, x => x.Tags.Contains("Xamarin"));
        }
    }
}
