using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;

namespace Xamarin.CodeAnalysis
{
    [ExportCompletionProvider(nameof(StringCompletionProvider), LanguageNames.CSharp)]
    public class StringCompletionProvider : CompletionProvider
    {
        private static readonly CompletionItemRules StandardCompletionRules = CompletionItemRules.Default.WithSelectionBehavior(CompletionItemSelectionBehavior.SoftSelection);

        public override bool ShouldTriggerCompletion(SourceText text, int caretPosition, CompletionTrigger trigger, OptionSet options)
        {
            // TODO: should trigger if we're inside a string
            return base.ShouldTriggerCompletion(text, caretPosition, trigger, options);
        }

        public override Task<CompletionDescription> GetDescriptionAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
        {
            // TODO: get the actual xml file location.
            return Task.FromResult(CompletionDescription.FromText("strings.xml(120,34)"));
        }

        public override Task<CompletionChange> GetChangeAsync(Document document, CompletionItem item, char? commitKey, CancellationToken cancellationToken)
        {
            // Determine change to insert
            return base.GetChangeAsync(document, item, commitKey, cancellationToken);
        }

        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            context.AddItem(CompletionItem.Create("@style/MainTheme",
                tags: ImmutableArray.Create(WellKnownTags.Constant),
                // TODO: props should contain the file location it was retrieved from to use in description
                // properties: ImmutableDictionary.CreateBuilder<string, string>().Add(,
                rules: StandardCompletionRules));

            await Task.CompletedTask;
        }
    }
}
