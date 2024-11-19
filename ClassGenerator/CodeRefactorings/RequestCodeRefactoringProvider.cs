using ClassGenerator.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClassGenerator.CodeRefactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RequestCodeRefactoringProvider)), Shared]
    public partial class RequestCodeRefactoringProvider : CodeRefactoringProvider
    {
        private const string NAMESPACE_PATTERN = @"Aurora\.(?'service'\w+)";

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var semanticModel = await document.GetSemanticModelAsync();

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var node = syntaxRoot.FindNode(context.Span);

            if (node is IdentifierNameSyntax identifierSyntax && node.Parent is ObjectCreationExpressionSyntax objectCreationSyntax)
            {
                var classNameTyping = identifierSyntax.Identifier.Text;

                var solution = document?.Project?.Solution;
                if (string.IsNullOrEmpty(classNameTyping) || solution == null)
                    return;

                if (classNameTyping.EndsWith(COMMAND_SUFFIX) || classNameTyping.EndsWith(QUERY_SUFFIX))
                {
                    var existingClass = await FindExistingClassAsync(solution, classNameTyping, cancellationToken);
                    if (existingClass != null)
                        return;

                    var @namespace = await GetNamespaceAsync(document, node.Span, cancellationToken);
                    var serviceName = GetServiceName(@namespace);

                    if (string.IsNullOrEmpty(serviceName))
                        return;

                    var codeActions = new List<CodeAction>();
                    if (classNameTyping.EndsWith(COMMAND_SUFFIX))
                    {
                        var commandAction = CodeAction.Create($"Create Command and Handler",
                            cancellation => CreateCommandWithHandlerAsync(document, serviceName, classNameTyping, cancellation),
                            equivalenceKey: nameof(RequestCodeRefactoringProvider));
                        codeActions.Add(commandAction);
                    }
                    else if (classNameTyping.EndsWith(QUERY_SUFFIX))
                    {
                        var queryAction = CodeAction.Create($"Create Query and Handler",
                            cancellation => CreateCommandWithHandlerAsync(document, serviceName, classNameTyping, cancellation),
                            equivalenceKey: nameof(RequestCodeRefactoringProvider));
                        codeActions.Add(queryAction);
                    }

                    context.AddCodeActions("Aurora", codeActions);
                }
            }
            else if (node is GenericNameSyntax genericName)
            {
                var codeActions = new List<CodeAction>();
                if (genericName.Identifier.Text == "SendCommand" && genericName.TypeArgumentList.Arguments.Count == 2)
                {
                    var commandType = genericName.TypeArgumentList.Arguments[0] as IdentifierNameSyntax;
                    var dtoType = genericName.TypeArgumentList.Arguments[1] as IdentifierNameSyntax;

                    if (commandType == null || !commandType.Identifier.Text.EndsWith(COMMAND_SUFFIX))
                        return;

                    var solution = document?.Project?.Solution;
                    if (solution == null) return;

                    var @namespace = await GetNamespaceAsync(document, node.Span, cancellationToken);
                    var serviceName = GetServiceName(@namespace);

                    if (string.IsNullOrEmpty(serviceName))
                        return;

                    // Create the code action
                    var action = CodeAction.Create(
                        $"Create Command and Handler",
                        cancellation => GenerateSendCommandRelatedClassesAsync(document, serviceName, commandType.Identifier.Text, dtoType?.Identifier.Text, cancellation),
                        equivalenceKey: nameof(RequestCodeRefactoringProvider));
                }

                context.AddCodeActions("Aurora", codeActions);
            }
            else if (node is InvocationExpressionSyntax invocationSyntax)
            {
                System.Console.WriteLine();
            }
        }

        private async Task<Solution> AddDocumentAsync(Solution solution, string projectName, string fileName, string[] folder, CompilationUnitSyntax syntax)
        {
            if (solution != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var project = solution.Projects.FirstOrDefault(p => p.Name == projectName);
                if (project != null)
                {
                    var updateProject = project.AddDocument(fileName, syntax, folder).Project;
                    return updateProject.Solution;
                }
            }

            return solution;
        }

        private async Task<INamedTypeSymbol> FindExistingClassAsync(Solution solution, string className, CancellationToken cancellationToken)
        {
            if (solution == null || string.IsNullOrWhiteSpace(className))
                return null;

            foreach (var projectId in solution.ProjectIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var project = solution.GetProject(projectId);
                var compilation = await project.GetCompilationAsync();

                var matchingType = compilation.GetSymbolsWithName(className).OfType<INamedTypeSymbol>().FirstOrDefault();

                if (matchingType != null)
                    return matchingType;
            }

            return null;
        }

        private static string GetServiceName(string currentNamespace)
        {
            foreach (Match m in Regex.Matches(currentNamespace, NAMESPACE_PATTERN, RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                return m.Groups["service"].Value;
            }

            return string.Empty;
        }

        private async Task<string> GetNamespaceAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var classDeclarationSyntax = syntaxRoot.DescendantNodes(span).OfType<ClassDeclarationSyntax>().FirstOrDefault();

            if (classDeclarationSyntax != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var declaredSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
                return declaredSymbol.ContainingNamespace.ToDisplayString();
            }

            return string.Empty;
        }
    }
}