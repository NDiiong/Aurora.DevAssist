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

        private Task<Solution> CreateQueryWithHandlerWithoutResultAsync(Document document, string serviceName, string classNameTyping, CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }

        private async Task<Solution> GenerateQueryIncludesRelatedClassesAsync(Document document, string serviceName, string classNameTyping, string dtoTyping, CancellationToken c)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution = document.Project.Solution;

            // Create Query
            var queryClassName = classNameTyping;
            var queryUsings = USINGS_QUERY.Select(u => u.Format(serviceName)).ToArray();
            var queryNamespace = NAMESPACE_QUERY.Format(serviceName);
            var queryProject = PROJECT_QUERY.Format(serviceName);
            var querySyntax = GenerateICommand_IQueryClassSyntax(INTERFACE_QUERY, queryClassName, queryNamespace, queryUsings);
            solution = await AddDocumentAsync(solution, queryProject, queryClassName, QUERY_HANDLER_FOLDERS, querySyntax);

            // Generate DTO class
            var dtoName = dtoTyping;
            var dtoUsings = USINGS_DTO.Select(u => u.Format(serviceName)).ToArray();
            var dtoNamespace = NAMESPACE_DTO.Format(serviceName);
            var dtoProject = PROJECT_DTO.Format(serviceName);
            var dtoSyntax = GenerateDtoClass(dtoName, dtoNamespace, dtoUsings);
            solution = await AddDocumentAsync(solution, dtoProject, dtoName, DTO_FOLDERS, dtoSyntax);

            // Generate Handler class
            var queryHandlerClassName = queryClassName.Replace(QUERY_SUFFIX, QUERY_HANDLER_SUFFIX);
            var handlerUsings = USINGS_QUERY_HANDLER.Select(u => u.Format(serviceName)).ToArray();
            var handlerNamespace = NAMESPACE_QUERY_HANDLER.Format(serviceName);
            var handlerProject = PROJECT_QUERY_HANDLER.Format(serviceName);
            var handlerSyntax = GenerateQueryHandlerReturnResultClass(queryClassName, queryHandlerClassName, dtoName, handlerNamespace, handlerUsings);
            solution = await AddDocumentAsync(solution, handlerProject, queryHandlerClassName, QUERY_HANDLER_FOLDERS, handlerSyntax);

            return solution;
        }

        private CompilationUnitSyntax GenerateQueryHandlerReturnResultClass(string queryName, string queryHandlerClassName, string dtoName, string @namespace, string[] usings)
        {
            return SyntaxFactory.CompilationUnit()
                .AddUsings(@namespace, usings)
                .AddMembers(
                    SyntaxFactory.NamespaceDeclaration(
                        SyntaxFactory.ParseName(@namespace))
                    .AddMembers(
                        SyntaxFactory.ClassDeclaration(queryHandlerClassName)
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

        private async Task<CodeAction[]> SendQueryAddCodeActionAsync(GenericNameSyntax genericName, Document document, CancellationToken cancellationToken)
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

            // Create the code action
            var codeAction = CodeAction.Create(QUERY_DESCRIPTION,
                cancellation => GenerateQueryIncludesRelatedClassesAsync(document, serviceName, queryType.Identifier.Text, dtoType.Identifier.Text, cancellation),
                equivalenceKey: nameof(RequestCodeRefactoringProvider));

            return new[] { codeAction };
        }
    }
}