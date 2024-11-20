using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

namespace Aurora.DevAssist.CodeAnalysis
{
    public static class SyntaxNodeExtensions
    {
        public static IEnumerable<T> ExtractSelectedNodesOfType<T>(this SyntaxNode rootNode, TextSpan selection, bool endOnBlockNode = false) where T : SyntaxNode
        {
            var currentNode = rootNode.FindNode(selection);
            var result = currentNode.DescendantNodes(selection).OfType<T>();

            if (!result.Any())
            {
                do
                {
                    if (endOnBlockNode && currentNode is BlockSyntax)
                        break;

                    if (currentNode is T singleResult)
                    {
                        result = new[] { singleResult };
                        break;
                    }
                    currentNode = currentNode.Parent;
                } while (currentNode != null);
            }

            return result;
        }

        public static T FirstParentOrSelfOfType<T>(this SyntaxNode rootNode) where T : SyntaxNode
        {
            while (rootNode != null)
            {
                if (rootNode is T result)
                {
                    return result;
                }
                rootNode = rootNode.Parent;
            }
            return null;
        }
    }
}