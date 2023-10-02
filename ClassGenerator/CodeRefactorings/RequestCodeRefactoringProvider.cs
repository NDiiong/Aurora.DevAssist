using ClassGenerator.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClassGenerator.CodeRefactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RequestCodeRefactoringProvider)), Shared]
    public class RequestCodeRefactoringProvider : CodeRefactoringProvider
    {
        private const string NAMESPACE_PATTERN = @"Aurora\.(?'service'\w+)";

        private const string INTERFACE_QUERY = "IQuery";
        private const string INTERFACE_COMMANDS = "ICommand";

        private const string DEFAULT_PROJECT_DOMAIN = "Aurora.{0}.Domain";

        private const string DEFAULT_NAMESPACE_QUERY = "Aurora.{0}.Domain.Queries";
        private readonly string[] FOLDER_QUERY = new[] { "Queries" };
        private readonly string[] USINGS_QUERY = new[] { "Aurora.{0}.Domain.Dtos", "Travel2Pay.Cqrs.Queries", };

        private const string DEFAULT_NAMESPACE_COMMAND = "Aurora.{0}.Domain.Commands";
        private readonly string[] FOLDER_COMMAND = new[] { "Commands" };
        private readonly string[] USINGS_COMMAND = new[] { "Aurora.{0}.Domain.Dtos", "Travel2Pay.Cqrs.Commands", };

        private const string DEFAULT_NAMESPACE_QUERY_HANDLER = "Aurora.{0}.{1}.Queries";
        private readonly string[] FOLDER_QUERY_HANDLER = new[] { "Queries" };
        private readonly string[] USINGS_QUERY_HANDLER = new[] { "Aurora.{0}.Domain.Queries", "Aurora.{0}.Domain.Dtos", "Travel2Pay.Cqrs.Queries", };

        private const string DEFAULT_NAMESPACE_COMMAND_HANDLER = "Aurora.{0}.{1}.Commands";
        private readonly string[] FOLDER_COMMAND_HANDLER = new[] { "Commands" };
        private readonly string[] USINGS_COMMAND_HANDLER = new[] { "Aurora.{0}.Domain.Commands", "Aurora.{0}.Domain.Dtos", "Travel2Pay.Cqrs.Commands", };

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var semanticModel = await document.GetSemanticModelAsync();

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var node = syntaxRoot.FindNode(context.Span);

            if (node is IdentifierNameSyntax identifierSyntax && node.Parent is ObjectCreationExpressionSyntax objectCreationSyntax)
            {
                var className = identifierSyntax?.Identifier.Text;
                var solution = document?.Project?.Solution;

                var existingClass = await FindExistingClassAsync(solution, className, cancellationToken);
                if (existingClass != null)
                    return;

                var @namespace = await GetFirstOrDefaultNamespaceAsync(document, node.Span, cancellationToken);
                if (string.IsNullOrEmpty(@namespace))
                    return;

                var serviceName = GetServiceName(@namespace);

                if (string.IsNullOrEmpty(serviceName))
                    return;

                var query = CodeAction.Create("Create IQuery", cancellation => CreateQueryClassAsync(document, serviceName, className, cancellation));
                //var queryHandler = CodeAction.Create("Create QueryHandler", cancellation => CreateQueryHandlerClassAsync(document, serviceName, className, cancellation));

                var command = CodeAction.Create("Create ICommand", cancellation => CreateCommandClassAsync(document, serviceName, className, cancellation));
                //var commandHandler = CodeAction.Create("Create CommandHandler", cancellation => CreateCommandHandlerClassAsync(document, serviceName, className, cancellation));

                var group = CodeAction.Create("Aurora", ImmutableArray.Create(new[] { query, command, /*queryHandler , commandHandler*/ }), false);
                context.RegisterRefactoring(group);
            }
        }

        //private async Task<Solution> CreateCommandHandlerClassAsync(Document document, string serviceName, string className, CancellationToken cancellation)
        //{
        //    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        //}

        //private static async Task<Solution> CreateQueryHandlerClassAsync(Document document, string serviceName, string className, CancellationToken cancellation)
        //{
        //    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        //}

        private async Task<Solution> CreateCommandClassAsync(Document document, string serviceName, string @class, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var @using = USINGS_COMMAND.Format(serviceName);
            var @namespace = DEFAULT_NAMESPACE_COMMAND.Format(serviceName);
            var @project = DEFAULT_PROJECT_DOMAIN.Format(serviceName);

            var syntax = GenerateQueryClassSyntax(@class, @namespace, INTERFACE_COMMANDS, @using);

            cancellationToken.ThrowIfCancellationRequested();
            return await AddDocumentAsync(document.Project.Solution, @project, @class, FOLDER_COMMAND, syntax);
        }

        private async Task<Solution> CreateQueryClassAsync(Document document, string serviceName, string @class, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var @using = USINGS_QUERY.Format(serviceName);
            var @namespace = DEFAULT_NAMESPACE_QUERY.Format(serviceName);
            var @project = DEFAULT_PROJECT_DOMAIN.Format(serviceName);

            var syntax = GenerateQueryClassSyntax(@class, @namespace, INTERFACE_QUERY, @using);

            cancellationToken.ThrowIfCancellationRequested();
            return await AddDocumentAsync(document.Project.Solution, @project, @class, FOLDER_QUERY, syntax);
        }

        private static CompilationUnitSyntax GenerateQueryClassSyntax(string @class, string @namespace, string @interface, params string[] usings)
        {
            var compilationUnit = SyntaxFactory.CompilationUnit();
            compilationUnit = compilationUnit.AddUsings(@namespace, usings);

            var newNamespace = SyntaxFactoryEx.NamespaceDeclaration(@namespace);
            var classDeclaration = SyntaxFactoryEx.PublicClassDeclaration(@class);
            classDeclaration = classDeclaration.AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(@interface)));
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

        private async Task<string> GetFirstOrDefaultNamespaceAsync(Document document, TextSpan span, CancellationToken cancellationToken)
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