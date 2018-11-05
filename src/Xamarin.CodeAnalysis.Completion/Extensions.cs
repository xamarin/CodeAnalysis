using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Xamarin.CodeAnalysis
{
    internal static class Extensions
    {
        //public static async Task<SemanticModel> GetSemanticModelForSpanAsync(this Document document, TextSpan span, CancellationToken cancellationToken)
        //{
        //    var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        //    var token = root.FindToken(span.Start);
        //    if (token.Parent == null)
        //    {
        //        return await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        //    }
        //}
    }
}
