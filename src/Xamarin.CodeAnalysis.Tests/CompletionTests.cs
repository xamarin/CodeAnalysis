using System;
using System.ComponentModel;
using System.IO;
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

namespace MyApp
{
    [Activity(Label = ""`"", MainLauncher = true)]
    public class MainActivity : Activity
    {
    }
}
", "@string/app_name", "@string/app_title")]
        [InlineData(@"using Android.App;

namespace MyApp
{
    [Activity(Theme = ""`"", MainLauncher = true)]
    public class MainActivity : Activity
    {
    }
}
", "@style/AppTheme", "@style/OtherTheme")]
        [InlineData(@"using System;
using System.ComponentModel;
using Android.App;

namespace Android.App;
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MyActivityAttribute : Attribute
    {
        public MyActivityAttribute(string id) { }
    }
}

namespace MyApp
{
    [MyActivity(""`"")]
    public class MainActivity : Activity
    {
    }
}")]
        [InlineData(@"using System;
using System.ComponentModel;
using Android.App;

namespace Android.App;
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MyActivityAttribute : Attribute
    {
        public string Label { get; set; }
        [Category(""@drawable;@mipmap"")]
        public string Drawable { get; set; }
    }
}

namespace MyApp
{
    [MyActivity(Label = ""`"")]
    public class MainActivity : Activity
    {
    }
}
", "@string/app_name", "@string/app_title")]
        [InlineData(@"using System;
using System.ComponentModel;
using Android.App;

namespace Android.App;
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MyActivityAttribute : Attribute
    {
        public string Label { get; set; }
        [Category(""@drawable;@mipmap"")]
        public string Drawable { get; set; }
    }
}

namespace MyApp
{
    [MyActivity(Drawable = ""`"")]
    public class MainActivity : Activity
    {
    }
}
", "@drawable/design_fab_background", "@mipmap/ic_launcher")]
        public async Task can_retrieve_completion(string code, params string[] completions)
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
            public const int OtherTheme = 2131034115;
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

            var service = CompletionService.GetService(document);
            Assert.NotNull(service);

            var caret = code.IndexOf('`');
            Assert.NotEqual(-1, caret);

            var actual = await service.GetCompletionsAsync(document, caret);

            Assert.False(actual == null && completions != null && completions.Length > 0, "No completions were found.");

            if (actual != null)
            {
                Assert.All(actual.Items, x => x.Tags.Contains("Xamarin"));
                Assert.Equal(actual.Items.Select(x => x.DisplayText), completions);
            }
        }

        [Theory]
        [InlineData(@"using Android.App;
namespace MyApp
{
    [Activity(Label = ""@string/app`"", MainLauncher = true)]
    public class MainActivity : Activity
    {
    }
}", "@string/app_name", @"using Android.App;
namespace MyApp
{
    [Activity(Label = ""@string/app_name"", MainLauncher = true)]
    public class MainActivity : Activity
    {
    }
}")]
        [InlineData(@"using Android.App;
namespace MyApp
{
    [Activity(Label = ""Hello` World"", MainLauncher = true)]
    public class MainActivity : Activity
    {
    }
}", "@string/app_name", @"using Android.App;
namespace MyApp
{
    [Activity(Label = ""@string/app_name"", MainLauncher = true)]
    public class MainActivity : Activity
    {
    }
}")]
        [InlineData(@"using Android.App;
namespace MyApp
{
    [Activity(Label = ""@string/app_settings`"", MainLauncher = true)]
    public class MainActivity : Activity
    {
    }
}", "@string/app_name", @"using Android.App;
namespace MyApp
{
    [Activity(Label = ""@string/app_name"", MainLauncher = true)]
    public class MainActivity : Activity
    {
    }
}")]
        public async Task can_apply_change(string code, string completion, string expected)
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
        }
    }
}")
               .Project
               .AddDocument("TestDocument.cs", code.Replace("`", ""));

            var service = CompletionService.GetService(document);
            Assert.NotNull(service);

            var caret = code.IndexOf('`');
            Assert.NotEqual(-1, caret);

            var completions = await service.GetCompletionsAsync(document, caret);
            Assert.NotNull(completions);
            Assert.Contains(completions.Items, x => x.DisplayText == completion);

            var item = completions.Items.First(x => x.DisplayText == completion);
            var change = await service.GetChangeAsync(document, item);
            var text = await document.GetTextAsync();

            var changed = text.WithChanges(change.TextChange).ToString();

            Assert.Equal(expected, changed);
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
        [InlineData(@"using System;
public class Foo 
{
    public void Do() 
    {
        Console.WriteLine(""`"");
    }
}")]
        [InlineData(@"using System.ComponentModel;
[Description(""`"")]
public class Foo 
{
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
        }
    }
}")
               .Project
               .AddDocument("TestDocument.cs", code.Replace("`", ""));

            var service = CompletionService.GetService(document);
            Assert.NotNull(service);

            var caret = code.IndexOf('`');
            Assert.NotEqual(-1, caret);

            var completions = await service.GetCompletionsAsync(document, caret);

            if (completions != null)
            {
                Assert.DoesNotContain(completions.Items, x => x.Tags.Contains("Xamarin"));
            }
        }
    }
}
