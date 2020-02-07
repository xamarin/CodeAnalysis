using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Xamarin.CodeAnalysis
{
    public static class Extensions
    {
        public static ISymbol GetSymbol(this IOperation operation)
        {
            switch (operation)
            {
                case IMemberReferenceOperation memberReference:
                    return memberReference.Member;
                case IInvocationOperation invocation:
                    return invocation.TargetMethod;
                case IObjectCreationOperation creation:
                    return creation.Constructor;
                default:
                    return null;
            }
        }
    }
}
