using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

// Example of how we can deal with internals for now until we get our ExternalAccess PRs merged.
namespace Xamarin.CodeAnalysis
{
    static class DocumentExtensions
    {
        public static Task<SemanticModel> GetSemanticModelForNodeAsync(this Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            var extensions = typeof(Workspace).Assembly.GetType("Microsoft.CodeAnalysis.Shared.Extensions.DocumentExtensions", true);
            var method = extensions.GetMethod(
                nameof(GetSemanticModelForNodeAsync),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Document), typeof(SyntaxNode), typeof(CancellationToken) },
                null);

            return (Task<SemanticModel>)method.Invoke(null, new object[] { document, node, cancellationToken });
        }
    }
}
