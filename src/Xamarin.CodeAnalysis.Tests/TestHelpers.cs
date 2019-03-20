using System;
using System.Diagnostics;
using System.Threading;

namespace Xamarin.CodeAnalysis.Tests
{
    static class TestHelpers
    {
        public static CancellationToken TimeoutToken(int seconds)
            => Debugger.IsAttached ?
                CancellationToken.None :
                new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;
    }
}
