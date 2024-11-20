using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace Aurora.DevAssist.CodeAnalysis
{
    public static class CompilationUnitSyntaxExtensions
    {
        public static CompilationUnitSyntax AddUsings(this CompilationUnitSyntax compilationUnitSyntax, string currentNamespace, params string[] usings)
        {
            if (usings != null && usings.Length > 0)
            {
                var distinctUsings = usings
                    .Where(x => !currentNamespace.StartsWith(x))
                    .OrderBy(x => x).Distinct();

                var items = distinctUsings.Select(x => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(x))).ToArray();
                return compilationUnitSyntax.AddUsings(items);
            }

            return compilationUnitSyntax;
        }
    }
}