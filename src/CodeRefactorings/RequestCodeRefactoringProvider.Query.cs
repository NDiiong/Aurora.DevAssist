using Aurora.DevAssist.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aurora.DevAssist.CodeRefactorings
{
    public partial class RequestCodeRefactoringProvider
    {
        private const string QUERY_DESCRIPTION = "Aurora: Create Query and Handler";
        private const string QUERY_SUFFIX = "Query";
        private const string INTERFACE_QUERY = "IQuery";
        private const string ABSTRACT_QUERY_HANDLER = "QueryHandler";
        private const string QUERY_HANDLER_SUFFIX = "QueryHandler";

        // PROJECTS
        private const string PROJECT_QUERY = "Aurora.{0}.Domain";

        private const string PROJECT_QUERY_HANDLER = "Aurora.{0}.ApplicationService";

        // NAMSPACES
        private const string NAMESPACE_QUERY = "Aurora.{0}.Domain.Queries";

        private const string NAMESPACE_QUERY_HANDLER = "Aurora.{0}.ApplicationService.Queries";

        // FOLDES
        private readonly string[] QUERY_FOLDERS = new[] { "Queries" };

        private readonly string[] QUERY_HANDLER_FOLDERS = new[] { "Queries" };

        // USINGS
        private readonly string[] USINGS_QUERY = new[] {
            "Aurora.{0}.Domain.Dtos",
            "Travel2Pay.Cqrs.Queries",
        };

        private readonly string[] USINGS_QUERY_HANDLER = new[] {
            "Aurora.{0}.Domain.Queries",
            "Aurora.{0}.Domain.Dtos",
            "Travel2Pay.Cqrs.Queries"
        };

        private async Task<CodeAction[]> SendQueryAddCodeActionAsync(GenericNameSyntax genericName, Document document, CancellationToken cancellationToken)
        {
            if (genericName.TypeArgumentList.Arguments.Count == 2)
            {
                var queryType = genericName.TypeArgumentList.Arguments[0] as IdentifierNameSyntax;
                var dtoType = genericName.TypeArgumentList.Arguments[1] as IdentifierNameSyntax;

                if (queryType == null || dtoType == null || !queryType.Identifier.Text.EndsWith(QUERY_SUFFIX))
                    return Array.Empty<CodeAction>();

                var solution = document?.Project?.Solution;
                if (solution == null)
                    return Array.Empty<CodeAction>();

                var @namespace = await GetNamespaceAsync(document, genericName.Span, cancellationToken);
                var serviceName = GetServiceName(@namespace);

                if (string.IsNullOrEmpty(serviceName))
                    return Array.Empty<CodeAction>();

                var queryName = queryType.Identifier.Text;
                var queryHandlerName = queryName.Replace(QUERY_SUFFIX, QUERY_HANDLER_SUFFIX);
                var dtoName = dtoType.Identifier.Text;

                var existingQuery = await FindExistingClassAsync(solution, queryName, cancellationToken);
                var existingQueryHandler = await FindExistingClassAsync(solution, queryHandlerName, cancellationToken);
                var existingDto = await FindExistingClassAsync(solution, dtoName, cancellationToken);

                if (existingQuery && existingQueryHandler && existingDto)
                    return Array.Empty<CodeAction>();

                // Create the code action
                var codeAction = CodeAction.Create(QUERY_DESCRIPTION,
                    cancellation => GenerateQueryIncludesRelatedClassesAsync(
                        document, serviceName,
                        existingQuery, existingQueryHandler, existingDto,
                        queryName, queryHandlerName, dtoName, cancellation),
                    equivalenceKey: nameof(RequestCodeRefactoringProvider));

                return new[] { codeAction };
            }

            return Array.Empty<CodeAction>();
        }

        private async Task<Solution> CreateObjectCreationQueryAsync(
            Document document, string serviceName,
            bool existingQuery, bool existingQueryHandler,
            string queryName, string queryHandlerName, CancellationToken cancellation)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution = document.Project.Solution;

            if (!existingQuery)
            {
                // Create query
                var queryUsings = USINGS_QUERY.Select(u => u.Format(serviceName)).ToArray();
                var queryNamespace = NAMESPACE_QUERY.Format(serviceName);
                var queryProject = PROJECT_QUERY.Format(serviceName);
                cancellation.ThrowIfCancellationRequested();
                var querySyntax = GenerateICommand_IQueryClassSyntax(INTERFACE_QUERY, queryName, queryNamespace, queryUsings);
                solution = await AddDocumentAsync(solution, queryProject, queryName, QUERY_HANDLER_FOLDERS, querySyntax);
            }

            if (!existingQueryHandler)
            {
                // Create Handler
                var handlerUsings = USINGS_QUERY_HANDLER.Select(u => u.Format(serviceName)).ToArray();
                var handlerNamespace = NAMESPACE_QUERY_HANDLER.Format(serviceName);
                var handlerProject = PROJECT_QUERY_HANDLER.Format(serviceName);
                cancellation.ThrowIfCancellationRequested();
                var handlerSyntax = GenerateQueryHandlerReturnResultClass(queryName, queryHandlerName, "", handlerNamespace, handlerUsings);
                solution = await AddDocumentAsync(solution, handlerProject, queryHandlerName, QUERY_HANDLER_FOLDERS, handlerSyntax);
            }

            return solution;
        }

        private async Task<Solution> GenerateQueryIncludesRelatedClassesAsync(
            Document document, string serviceName,
            bool existingQuery, bool existingQueryHandler, bool existingDto,
            string queryName, string queryHandlerName, string dtoName,
            CancellationToken cancellation)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution = document.Project.Solution;

            if (!existingQuery)
            {
                // Create Query
                var queryUsings = USINGS_QUERY.Select(u => u.Format(serviceName)).ToArray();
                var queryNamespace = NAMESPACE_QUERY.Format(serviceName);
                var queryProject = PROJECT_QUERY.Format(serviceName);
                cancellation.ThrowIfCancellationRequested();
                var querySyntax = GenerateICommand_IQueryClassSyntax(INTERFACE_QUERY, queryName, queryNamespace, queryUsings);
                solution = await AddDocumentAsync(solution, queryProject, queryName, QUERY_HANDLER_FOLDERS, querySyntax);
            }

            if (!existingDto)
            {
                // Generate DTO class
                var dtoUsings = USINGS_DTO.Select(u => u.Format(serviceName)).ToArray();
                var dtoNamespace = NAMESPACE_DTO.Format(serviceName);
                var dtoProject = PROJECT_DTO.Format(serviceName);
                cancellation.ThrowIfCancellationRequested();
                var dtoSyntax = GenerateDtoClass(dtoName, dtoNamespace, dtoUsings);
                solution = await AddDocumentAsync(solution, dtoProject, dtoName, DTO_FOLDERS, dtoSyntax);
            }

            if (!existingQueryHandler)
            {
                // Generate Handler class
                var handlerUsings = USINGS_QUERY_HANDLER.Select(u => u.Format(serviceName)).ToArray();
                var handlerNamespace = NAMESPACE_QUERY_HANDLER.Format(serviceName);
                var handlerProject = PROJECT_QUERY_HANDLER.Format(serviceName);
                cancellation.ThrowIfCancellationRequested();
                var handlerSyntax = GenerateQueryHandlerReturnResultClass(queryName, queryHandlerName, dtoName, handlerNamespace, handlerUsings);
                solution = await AddDocumentAsync(solution, handlerProject, queryHandlerName, QUERY_HANDLER_FOLDERS, handlerSyntax);
            }

            return solution;
        }

        private static CompilationUnitSyntax GenerateQueryHandlerReturnResultClass(string queryName, string queryHandlerName, string dtoName, string @namespace, string[] usings)
        {
            return SyntaxFactory.CompilationUnit()
                .AddUsings(@namespace, usings)
                .AddMembers(
                    SyntaxFactory.NamespaceDeclaration(
                        SyntaxFactory.ParseName(@namespace))
                    .AddMembers(
                        SyntaxFactory.ClassDeclaration(queryHandlerName)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .AddBaseListTypes(
                                SyntaxFactory.SimpleBaseType(
                                    SyntaxFactory.ParseTypeName($"{ABSTRACT_QUERY_HANDLER}<{queryName}, {dtoName}>")))
                            .AddMembers(
                                SyntaxFactory.MethodDeclaration(
                                    SyntaxFactory.ParseTypeName($"Task<{dtoName}>"),
                                    "HandleAsync")
                                    .AddModifiers(
                                        SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                                        SyntaxFactory.Token(SyntaxKind.OverrideKeyword),
                                        SyntaxFactory.Token(SyntaxKind.AsyncKeyword))
                                    .AddParameterListParameters(
                                        SyntaxFactory.Parameter(
                                            SyntaxFactory.Identifier("query"))
                                            .WithType(SyntaxFactory.ParseTypeName(queryName)),
                                        SyntaxFactory.Parameter(
                                            SyntaxFactory.Identifier("token"))
                                            .WithType(SyntaxFactory.ParseTypeName("CancellationToken"))
                                            .WithDefault(
                                                SyntaxFactory.EqualsValueClause(
                                                    SyntaxFactory.LiteralExpression(
                                                        SyntaxKind.DefaultLiteralExpression))))
                                    .WithBody(
                                        SyntaxFactory.Block(
                                            SyntaxFactory.ThrowStatement(
                                                SyntaxFactory.ObjectCreationExpression(
                                                    SyntaxFactory.ParseTypeName("NotImplementedException"))
                                                    .WithArgumentList(
                                                        SyntaxFactory.ArgumentList())))))));
        }
    }
}