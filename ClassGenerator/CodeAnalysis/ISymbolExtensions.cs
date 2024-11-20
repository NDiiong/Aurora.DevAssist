using Microsoft.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Aurora.DevAssist.CodeAnalysis
{
    public static class ISymbolExtensions
    {
        public static bool IsCompilerGenerated(this ISymbol symbol)
        {
            var attributes = symbol.GetAttributes();

            if (attributes.Any(x => x.AttributeClass.Name == nameof(CompilerGeneratedAttribute)))
            {
                return true;
            }

            return symbol.IsImplicitlyDeclared;
        }

        public static bool IsInaccessibleLocal(this ISymbol symbol, int position)
        {
            if (symbol.Kind != SymbolKind.Local)
            {
                return false;
            }

            if (symbol.IsImplicitlyDeclared)
            {
                return false;
            }

            var declarationSyntax = symbol.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).FirstOrDefault();
            return declarationSyntax != null && position < declarationSyntax.SpanStart;
        }
    }
}