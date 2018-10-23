
using System;
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
        [InlineData(@"using System;
public class Foo 
{
    public void Do() 
    {
        Console.WriteLine(""`"");
    }
}")]
        public async Task can_retrieve_completion(string code)
        {
            var hostServices = MefHostServices.Create(MefHostServices.DefaultAssemblies.Concat(
                new[]
                {
                    typeof(CompletionService).Assembly,
                    typeof(StringCompletionProvider).Assembly,
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
               .AddDocument("TestDocument.cs", code);

            var service = CompletionService.GetService(document);
            Assert.NotNull(service);

            var caret = code.IndexOf('`');
            Assert.NotEqual(-1, caret);

            var completions = await service.GetCompletionsAsync(document, caret);

            Assert.NotNull(completions);
            Assert.NotEmpty(completions.Items);
        }
    }
}
