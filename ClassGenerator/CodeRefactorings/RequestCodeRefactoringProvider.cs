using ClassGenerator.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassGenerator.CodeRefactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RequestCodeRefactoringProvider)), Shared]
    public class RequestCodeRefactoringProvider : CodeRefactoringProvider
    {
        private const string INTERFACE_QUERY = "IQuery";
        private const string DEFAULT_PROJECT_QUERY = "Aurora.FIN.Domain";
        private const string DEFAULT_NAMESPACE_QUERY = "Aurora.FIN.Domain.Queries";

        private readonly string[] _defaultFolderQuery = new[] { "Queries" };
        private readonly string[] _defaultUsingsQuery = new[] { "Aurora.FIN.Domain.Dtos", "Travel2Pay.Cqrs.Queries", };

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var node = syntaxRoot.FindNode(context.Span);

            if (node is IdentifierNameSyntax identifierSyntax && node.Parent is ObjectCreationExpressionSyntax)
            {
                var className = identifierSyntax?.Identifier.Text;
                var solution = document?.Project?.Solution;

                var existingClass = await FindExistingClassAsync(solution, className, cancellationToken);
                if (existingClass != null)
                    return;

                var query = CodeAction.Create("Create IQuery", cancellation => CreateQueryClassAsync(document, className, cancellation));
                var queryHandler = CodeAction.Create("Create QueryHandler", cancellation => CreateQueryClassAsync(document, className, cancellation));

                var command = CodeAction.Create("Create ICommand", cancellation => CreateQueryClassAsync(document, className, cancellation));
                var commandHandler = CodeAction.Create("Create CommandHandler", cancellation => CreateQueryClassAsync(document, className, cancellation));

                var group = CodeAction.Create("Aurora", ImmutableArray.Create(new[] { query, queryHandler, command, commandHandler }), false);
                context.RegisterRefactoring(group);
            }
        }

        private async Task<Solution> CreateQueryClassAsync(Document document, string className, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var requestClassDocument = GenerateQueryClassDocument(className, DEFAULT_NAMESPACE_QUERY, _defaultFolderQuery, _defaultUsingsQuery);
            cancellationToken.ThrowIfCancellationRequested();
            return await AddDocumentAsync(document.Project.Solution, DEFAULT_PROJECT_QUERY, requestClassDocument.FileName, requestClassDocument.Folder, requestClassDocument.Syntax);
        }

        private static DocumentContext GenerateQueryClassDocument(string className, string @namespace, string[] folder, params string[] usings)
        {
            return new DocumentContext
            {
                Syntax = GenerateQueryClassSyntax(className, @namespace, usings),
                FileName = className,
                Folder = folder
            };
        }

        private static CompilationUnitSyntax GenerateQueryClassSyntax(string className, string @namespace, params string[] usings)
        {
            var compilationUnit = SyntaxFactory.CompilationUnit();
            compilationUnit = compilationUnit.AddUsings(@namespace, usings);

            var newNamespace = SyntaxFactoryEx.NamespaceDeclaration(@namespace);
            var classDeclaration = SyntaxFactoryEx.PublicClassDeclaration(className);
            classDeclaration = classDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(INTERFACE_QUERY)));
            newNamespace = newNamespace.AddMembers(classDeclaration);
            compilationUnit = compilationUnit.AddMembers(newNamespace);
            return compilationUnit.NormalizeWhitespace();
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
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (solution != null && !string.IsNullOrWhiteSpace(className))
            {
                foreach (var project in solution.Projects)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var document in project.Documents)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var semanticModel = await document.GetSemanticModelAsync();
                        var syntaxRoot = await document.GetSyntaxRootAsync();
                        foreach (var classDeclaration in syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var declaredSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                            if (declaredSymbol?.Name == className)
                            {
                                if (declaredSymbol.ContainingNamespace.ToDisplayString() != document.Project.DefaultNamespace)
                                {
                                    return declaredSymbol;
                                }
                            }
                        }
                    }
                }
            }

            return default;
        }

        private class DocumentContext
        {
            public string FileName { get; set; }
            public string[] Folder { get; set; }
            public CompilationUnitSyntax Syntax { get; set; }
        }
    }
}