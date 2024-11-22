using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using System.Collections.Generic;

namespace Aurora.DevAssist.CodeAnalysis
{
    public static class CodeRefactoringContextExtensions
    {
        public static void AddCodeAction(this CodeRefactoringContext context, CodeAction codeAction)
        {
            context.RegisterRefactoring(codeAction);
        }

        public static void AddCodeActions(this CodeRefactoringContext context, List<CodeAction> codeActions)
        {
            if (codeActions != null && codeActions.Count > 0)
            {
                for (var i = 0; i < codeActions.Count; i++)
                {
                    context.RegisterRefactoring(codeActions[i]);
                }
            }
        }

        public static void AddCodeActions(this CodeRefactoringContext context, CodeAction[] codeActions)
        {
            if (codeActions != null && codeActions.Length >= 0)
            {
                for (var i = 0; i < codeActions.Length; i++)
                {
                    context.RegisterRefactoring(codeActions[i]);
                }
            }

            //if (codeActions.Length > 0)
            //{
            //    var group = CodeAction.Create(groupName, ImmutableArray.Create(codeActions), isInlinable: true);
            //    context.RegisterRefactoring(group);
            //}
        }
    }
}