using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aurora.DevAssist.CodeAnalysis
{
    public static class INamedTypeSymbolExtensions
    {
        public static async Task<string> DetermineMethodNameUsedToNotifyThatPropertyWasChangedAsync(this INamedTypeSymbol typeSymbol, Solution solution)
        {
            var result = "OnPropertyChanged";

            var typesInInheritanceHierarchy = new List<INamedTypeSymbol>();
            var currentType = typeSymbol;
            while ((currentType != null) && (!currentType.ContainingNamespace?.ToString().StartsWith("System.") == true))
            {
                typesInInheritanceHierarchy.Add(currentType);
                currentType = currentType.BaseType;
            }

            foreach (var type in typesInInheritanceHierarchy)
            {
                foreach (ISymbol methodSymbol in type.GetMembers().Where(x => !x.IsImplicitlyDeclared).OfType<IMethodSymbol>().Where(x => x.MethodKind == MethodKind.Ordinary))
                {
                    foreach (var syntaxReference in methodSymbol.DeclaringSyntaxReferences)
                    {
                        var methodNode = await syntaxReference.GetSyntaxAsync().ConfigureAwait(false) as MethodDeclarationSyntax;
                        foreach (var invocation in methodNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
                        {
                            if (invocation.Expression is IdentifierNameSyntax idSyntaxt)
                            {
                                if (idSyntaxt.Identifier.ValueText == "PropertyChanged")
                                {
                                    result = methodSymbol.Name;
                                    return result;
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        public static char? DetermineBackingFiledPrefix(this INamedTypeSymbol typeSymbol)
        {
            char? result = null;

            var backingFileds = typeSymbol.GetMembers().Where(x => x.Kind == SymbolKind.Field).Where(x => x.IsImplicitlyDeclared == false);

            if (backingFileds.Any())
            {
                if (backingFileds.First().Name?.HasPrefix() == true)
                {
                    var candidateForPrefix = backingFileds.First().Name[0];
                    if (backingFileds.All(x => x.Name[0] == candidateForPrefix))
                    {
                        result = candidateForPrefix;
                    }
                }
            }

            return result;
        }
    }
}