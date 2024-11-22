using Aurora.DevAssist.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aurora.DevAssist.CodeRefactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RequestCodeRefactoringProvider)), Shared]
    public partial class RequestCodeRefactoringProvider : CodeRefactoringProvider
    {
        private const string NAMESPACE_PATTERN = @"Aurora\.(?'service'\w+)";
        private const string CSHARP_EXTENSION = ".cs";

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var semanticModel = await document.GetSemanticModelAsync();

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var node = syntaxRoot.FindNode(context.Span);

            // WHEN THE MOUSE POINTER FOCUSES ON THE NEW COMMAND
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
                        var commandAction = CodeAction.Create(COMMAND_DESCRIPTION,
                            cancellation => CreateObjectCreationCommandAsync(document, serviceName, classNameTyping, cancellation),
                            equivalenceKey: nameof(RequestCodeRefactoringProvider));
                        codeActions.Add(commandAction);
                    }
                    else if (classNameTyping.EndsWith(QUERY_SUFFIX))
                    {
                        var queryAction = CodeAction.Create(QUERY_DESCRIPTION,
                            cancellation => CreateObjectCreationQueryAsync(document, serviceName, classNameTyping, cancellation),
                            equivalenceKey: nameof(RequestCodeRefactoringProvider));
                        codeActions.Add(queryAction);
                    }

                    context.AddCodeActions(codeActions);
                }
            }
            // WHEN THE MOUSE POINTER FOCUSES ON THE ARGUMENTS OF METHOD SENDCOMMAND
            else if (node is IdentifierNameSyntax identifierName && node?.Parent?.Parent is GenericNameSyntax parentGenericNameSyntax)
            {
                var codeActions = await DetectSendCommandQueryAddCodeActionAsync(parentGenericNameSyntax, document, cancellationToken);
                context.AddCodeActions(codeActions);
            }
            // WHEN THE MOUSE POINTER FOCUSES ON THE METHOD SENDCOMMAND
            else if (node is GenericNameSyntax nodeGenericNameSyntax)
            {
                var codeActions = await DetectSendCommandQueryAddCodeActionAsync(nodeGenericNameSyntax, document, cancellationToken);
                context.AddCodeActions(codeActions);
            }
            // WHEN THE MOUSE POINTER FOCUSES ON THE VARIABLE DECLARATOR
            else if (node is VariableDeclaratorSyntax variableDeclarator)
            {
                var invocationExpression = variableDeclarator
                    .Ancestors().OfType<LocalDeclarationStatementSyntax>()
                    .FirstOrDefault()?.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>().FirstOrDefault();

                if (invocationExpression == null)
                    return;

                var memberAccessExpression = invocationExpression.Expression as MemberAccessExpressionSyntax;
                if (memberAccessExpression == null)
                    return;

                if (memberAccessExpression.Name is GenericNameSyntax memberAccessGenericName)
                {
                    var codeActions = await DetectSendCommandQueryAddCodeActionAsync(memberAccessGenericName, document, cancellationToken);
                    context.AddCodeActions(codeActions);
                }
            }
            // WHEN THE MOUSE POINTER FOCUSES ON THE SCOPED MEDIATOR
            else if (node is IdentifierNameSyntax identifierName2 && node.Parent is MemberAccessExpressionSyntax parentMemberAccessExpression)
            {
                if (parentMemberAccessExpression.Name is GenericNameSyntax memberAccessGenericName)
                {
                    var codeActions = await DetectSendCommandQueryAddCodeActionAsync(memberAccessGenericName, document, cancellationToken);
                    context.AddCodeActions(codeActions);
                }
                else if (parentMemberAccessExpression.Name is IdentifierNameSyntax identifierName3)
                {
                    var codeActions = await SendCommandAddCodeActionAsync(identifierName3, document, cancellationToken);
                    context.AddCodeActions(codeActions);
                }
            }
            // WHEN THE MOUSE POINTER FOCUSES ON THE AWAIT
            else if (node is AwaitExpressionSyntax awaitExpressionSyntax && awaitExpressionSyntax.Expression is InvocationExpressionSyntax invocationExpressionSyntax)
            {
                var memberAccessExpression = invocationExpressionSyntax.Expression as MemberAccessExpressionSyntax;
                if (memberAccessExpression == null)
                    return;

                if (memberAccessExpression.Name is GenericNameSyntax memberAccessGenericName)
                {
                    var codeActions = await DetectSendCommandQueryAddCodeActionAsync(memberAccessGenericName, document, cancellationToken);
                    context.AddCodeActions(codeActions);
                }
                else if (memberAccessExpression.Name is IdentifierNameSyntax identifierName3)
                {
                    var codeActions = await SendCommandAddCodeActionAsync(identifierName3, document, cancellationToken);
                    context.AddCodeActions(codeActions);
                }
            }
        }

        private async Task<CodeAction[]> DetectSendCommandQueryAddCodeActionAsync(GenericNameSyntax genericName, Document document, CancellationToken cancellationToken)
        {
            if (genericName.Identifier.Text == "SendCommand")
                return await SendCommandAddCodeActionAsync(genericName, document, cancellationToken);
            else if (genericName.Identifier.Text == "SendQuery")
                return await SendQueryAddCodeActionAsync(genericName, document, cancellationToken);

            return Array.Empty<CodeAction>();
        }

        private async Task<Solution> AddDocumentAsync(Solution solution, string projectName, string fileName, string[] folder, CompilationUnitSyntax syntax)
        {
            if (solution != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var project = solution.Projects.FirstOrDefault(p => p.Name == projectName);
                if (project != null)
                {
                    var existingDocument = project.Documents.FirstOrDefault(d => d.Name == fileName + CSHARP_EXTENSION);
                    if (existingDocument != null)
                        return solution;

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

                // Use GetAllDocuments() to include all documents in nested folders
                var documents = project.Documents;

                foreach (var document in documents)
                {
                    var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                    var matchingType = semanticModel.Compilation.GetSymbolsWithName(
                        name => name == className,
                        SymbolFilter.Type)
                        .OfType<INamedTypeSymbol>()
                        .FirstOrDefault();

                    if (matchingType != null)
                        return matchingType;
                }
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

        private CompilationUnitSyntax GenerateICommand_IQueryClassSyntax(string @interface, string className, string @namespace, string[] usings)
        {
            var compilationUnit = SyntaxFactory.CompilationUnit();

            // Add usings
            foreach (var usingNamespace in usings)
            {
                compilationUnit = compilationUnit.AddUsings(
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(usingNamespace))
                );
            }

            // Create namespace
            var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(@namespace)
            );

            // Create class declaration
            var classDeclaration = SyntaxFactory.ClassDeclaration(className)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword)
                    )
                )
                .AddBaseListTypes(
                    SyntaxFactory.SimpleBaseType(
                        SyntaxFactory.IdentifierName(@interface)
                    )
                );

            // Add class to namespace
            namespaceDeclaration = namespaceDeclaration.AddMembers(classDeclaration);

            // Add namespace to compilation unit
            compilationUnit = compilationUnit.AddMembers(namespaceDeclaration);

            return compilationUnit.NormalizeWhitespace();
        }

        private CompilationUnitSyntax GenerateDtoClass(string dtoName, string @namespace, string[] usings)
        {
            return SyntaxFactory.CompilationUnit()
                .AddUsings(@namespace, usings)
                .AddMembers(
                    SyntaxFactory.NamespaceDeclaration(
                        SyntaxFactory.ParseName(@namespace))
                    .AddMembers(
                        SyntaxFactory.ClassDeclaration(dtoName)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))));
        }
    }
}