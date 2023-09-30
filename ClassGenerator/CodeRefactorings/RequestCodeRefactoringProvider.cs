using ClassGenerator.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace ClassGenerator.CodeRefactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RequestCodeRefactoringProvider)), Shared]
    public class RequestCodeRefactoringProvider : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var methodDeclarations = root.ExtractSelectedNodesOfType<MethodDeclarationSyntax>(context.Span, true).ToArray();
            var classDeclarations = root.ExtractSelectedNodesOfType<ClassDeclarationSyntax>(context.Span, true).ToArray();
            var text = root.ExtractSelectedNodesOfType<IdentifierNameSyntax>(context.Span, true).ToArray();
        }

        public async Task AddDocumentAsync(Solution solution, string projectName, string fileName, string folder, CompilationUnitSyntax syntax)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var project = solution.Projects.FirstOrDefault(p => p.Name == projectName);
            if (project != null)
            {
                var updateProject = project.AddDocument(fileName, syntax, new[] { folder }).Project;
                solution.Workspace.TryApplyChanges(updateProject.Solution);
            }
        }
    }
}