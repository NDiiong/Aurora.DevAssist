using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ClassGenerator.CodeAnalysis
{
    public static class CodeRefactoringContextExtensions
    {
        public static void AddCodeAction(this CodeRefactoringContext context, CodeAction codeAction)
        {
            context.RegisterRefactoring(codeAction);
        }

        public static void AddCodeActions(this CodeRefactoringContext context, string groupName, List<CodeAction> codeActions)
        {
            if (codeActions.Count > 0)
            {
                var group = CodeAction.Create(groupName, ImmutableArray.Create(codeActions.ToArray()), isInlinable: false);
                context.RegisterRefactoring(group);
            }
        }
    }
}