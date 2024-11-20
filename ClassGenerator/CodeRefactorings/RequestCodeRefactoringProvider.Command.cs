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
        private const string COMMAND_DESCRIPTION = "Aurora: Create Command and Handler";
        private const string COMMAND_SUFFIX = "Command";
        private const string INTERFACE_COMMAND = "ICommand";
        private const string ABSTRACT_COMMAND_HANDLER = "CommandHandler";
        private const string COMMAND_HANDLER_SUFFIX = "CommandHandler";

        // PROJECTS
        private const string PROJECT_DTO = "Aurora.{0}.Domain";

        private const string PROJECT_COMMAND = "Aurora.{0}.Domain";
        private const string PROJECT_COMMAND_HANDLER = "Aurora.{0}.ApplicationService";
        private const string PROJECT_COMMAND_HANDLER_WITH_RESULT = "Aurora.{0}.ApplicationService";

        // NAMSPACES
        private const string NAMESPACE_DTO = "Aurora.{0}.Domain.Dtos";

        private const string NAMESPACE_COMMAND = "Aurora.{0}.Domain.Commands";
        private const string NAMESPACE_COMMAND_HANDLER = "Aurora.{0}.ApplicationService.Commands";
        private const string NAMESPACE_COMMAND_HANDLER_WITH_RESULT = "Aurora.{0}.ApplicationService.Commands";

        // FOLDES
        private readonly string[] COMMAND_HANDLER_FOLDERS = new[] { "Commands" };

        private readonly string[] COMMAND_HANDLER_WITH_RESULT_FOLDERS = new[] { "Commands" };
        private readonly string[] DTO_FOLDERS = new[] { "Dtos" };

        // USINGS
        private readonly string[] USINGS_COMMAND = new[] {
            "Aurora.{0}.Domain.Dtos",
            "Travel2Pay.Cqrs.Commands",
        };

        private readonly string[] USINGS_COMMAND_HANDLER = new[] {
            "Aurora.{0}.Domain.Commands",
            "Travel2Pay.Cqrs.Commands"
        };

        private readonly string[] USINGS_COMMAND_HANDLER_WITH_RESULT = new[] {
            "Aurora.{0}.Domain.Commands",
            "Aurora.Invoice.Domain.Dtos",
            "Travel2Pay.Cqrs.Commands",
        };

        private readonly string[] USINGS_DTO = Array.Empty<string>();

        private async Task<Solution> CreateCommandWithHandlerWithoutResultAsync(Document document, string serviceName, string classNameTyping, CancellationToken cancellation)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution = document.Project.Solution;

            // Create Command
            var commandClassName = classNameTyping;
            var commandUsings = USINGS_COMMAND.Select(u => u.Format(serviceName)).ToArray();
            var commandNamespace = NAMESPACE_COMMAND.Format(serviceName);
            var commandProject = PROJECT_COMMAND.Format(serviceName);
            var commandSyntax = GenerateICommand_IQueryClassSyntax(INTERFACE_COMMAND, commandClassName, commandNamespace, commandUsings);
            solution = await AddDocumentAsync(solution, commandProject, commandClassName, COMMAND_HANDLER_FOLDERS, commandSyntax);

            // Create Handler
            var commandHandlerClassName = commandClassName.Replace(COMMAND_SUFFIX, COMMAND_HANDLER_SUFFIX);
            var commandHandlerUsings = USINGS_COMMAND_HANDLER.Select(u => u.Format(serviceName)).ToArray();
            var commandHandlerNamespace = NAMESPACE_COMMAND_HANDLER.Format(serviceName);
            var commandHandlerProject = PROJECT_COMMAND_HANDLER.Format(serviceName);
            var commandHandlerSyntax = GenerateCommandHandlerClassSyntax(commandClassName, commandHandlerClassName, commandHandlerNamespace, commandHandlerUsings);
            solution = await AddDocumentAsync(solution, commandHandlerProject, commandHandlerClassName, COMMAND_HANDLER_FOLDERS, commandHandlerSyntax);

            return solution;
        }

        private static CompilationUnitSyntax GenerateCommandHandlerClassSyntax(string commandClassName, string handlerClassName, string @namespace, string[] usings)
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
            var classDeclaration = SyntaxFactory.ClassDeclaration(handlerClassName)
             .WithModifiers(
                 SyntaxFactory.TokenList(
                     SyntaxFactory.Token(SyntaxKind.PublicKeyword)
                 )
             )
             .AddBaseListTypes(
                 SyntaxFactory.SimpleBaseType(
                     SyntaxFactory.GenericName(
                         SyntaxFactory.Identifier(ABSTRACT_COMMAND_HANDLER))
                         .WithTypeArgumentList(
                             SyntaxFactory.TypeArgumentList(
                                 SyntaxFactory.SeparatedList<TypeSyntax>(
                                     new[] { SyntaxFactory.IdentifierName(commandClassName) }
                                 )
                             )
                         )
                 )
             );

            // Add HandleAsync method
            var methodDeclaration = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.IdentifierName("Task"),
                    SyntaxFactory.Identifier("HandleAsync")
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                        SyntaxFactory.Token(SyntaxKind.OverrideKeyword)
                    )
                )
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Parameter(
                                SyntaxFactory.Identifier("command"))
                                .WithType(SyntaxFactory.IdentifierName(commandClassName)),
                            SyntaxFactory.Parameter(
                                SyntaxFactory.Identifier("token"))
                                .WithType(SyntaxFactory.IdentifierName("CancellationToken"))
                                .WithDefault(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.DefaultLiteralExpression
                                        )
                                    )
                                )
                        })
                    )
                )
                .WithBody(
                    SyntaxFactory.Block(
                        SyntaxFactory.ThrowStatement(
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.IdentifierName("NotImplementedException")
                            ).WithArgumentList(
                                SyntaxFactory.ArgumentList()
                            )
                        )
                    )
                );

            // Add method to class
            classDeclaration = classDeclaration.AddMembers(methodDeclaration);

            // Add class to namespace
            namespaceDeclaration = namespaceDeclaration.AddMembers(classDeclaration);

            // Add namespace to compilation unit
            compilationUnit = compilationUnit.AddMembers(namespaceDeclaration);

            return compilationUnit.NormalizeWhitespace();
        }

        private async Task<Solution> GenerateCommandIncludesRelatedClassesAsync(Document document, string serviceName, string classNameTyping, string dtoTyping, CancellationToken c)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution = document.Project.Solution;

            // Create Command
            var commandClassName = classNameTyping;
            var commandUsings = USINGS_COMMAND.Select(u => u.Format(serviceName)).ToArray();
            var commandNamespace = NAMESPACE_COMMAND.Format(serviceName);
            var commandProject = PROJECT_COMMAND.Format(serviceName);
            var commandSyntax = GenerateICommand_IQueryClassSyntax(INTERFACE_COMMAND, commandClassName, commandNamespace, commandUsings);
            solution = await AddDocumentAsync(solution, commandProject, commandClassName, COMMAND_HANDLER_FOLDERS, commandSyntax);

            // Generate DTO class
            if (!string.IsNullOrEmpty(dtoTyping))
            {
                var dtoName = dtoTyping;
                var dtoUsings = USINGS_DTO.Select(u => u.Format(serviceName)).ToArray();
                var dtoNamespace = NAMESPACE_DTO.Format(serviceName);
                var dtoProject = PROJECT_DTO.Format(serviceName);
                var dtoSyntax = GenerateDtoClass(dtoName, dtoNamespace, dtoUsings);
                solution = await AddDocumentAsync(solution, dtoProject, dtoName, DTO_FOLDERS, dtoSyntax);
            }

            // Generate Handler class
            var commandHandlerClassName = commandClassName.Replace(COMMAND_SUFFIX, COMMAND_HANDLER_SUFFIX);
            var handlerUsings = USINGS_COMMAND_HANDLER_WITH_RESULT.Select(u => u.Format(serviceName)).ToArray();
            var handlerNamespace = NAMESPACE_COMMAND_HANDLER_WITH_RESULT.Format(serviceName);
            var handlerProject = PROJECT_COMMAND_HANDLER_WITH_RESULT.Format(serviceName);
            var handlerSyntax = GenerateCommandHandlerReturnResultClass(commandClassName, commandHandlerClassName, dtoTyping, handlerNamespace, handlerUsings);
            solution = await AddDocumentAsync(solution, handlerProject, commandHandlerClassName, COMMAND_HANDLER_WITH_RESULT_FOLDERS, handlerSyntax);

            return solution;
        }

        private CompilationUnitSyntax GenerateCommandHandlerReturnResultClass(string commandName, string commandHandlerClassName, string dtoName, string @namespace, string[] usings)
        {
            if (!string.IsNullOrEmpty(dtoName))
            {
                return SyntaxFactory.CompilationUnit()
                .AddUsings(@namespace, usings)
                .AddMembers(
                    SyntaxFactory.NamespaceDeclaration(
                        SyntaxFactory.ParseName(@namespace))
                    .AddMembers(
                        SyntaxFactory.ClassDeclaration(commandHandlerClassName)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .AddBaseListTypes(
                                SyntaxFactory.SimpleBaseType(
                                    SyntaxFactory.ParseTypeName($"{ABSTRACT_COMMAND_HANDLER}<{commandName}, {dtoName}>")))
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
                                            SyntaxFactory.Identifier("command"))
                                            .WithType(SyntaxFactory.ParseTypeName(commandName)),
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
            return SyntaxFactory.CompilationUnit()
                .AddUsings(@namespace, usings)
                .AddMembers(
                    SyntaxFactory.NamespaceDeclaration(
                        SyntaxFactory.ParseName(@namespace))
                    .AddMembers(
                        SyntaxFactory.ClassDeclaration(commandHandlerClassName)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .AddBaseListTypes(
                                SyntaxFactory.SimpleBaseType(
                                    SyntaxFactory.ParseTypeName($"{ABSTRACT_COMMAND_HANDLER}<{commandName}>")))
                            .AddMembers(
                                SyntaxFactory.MethodDeclaration(
                                    SyntaxFactory.ParseTypeName($"Task"),
                                    "HandleAsync")
                                    .AddModifiers(
                                        SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                                        SyntaxFactory.Token(SyntaxKind.OverrideKeyword),
                                        SyntaxFactory.Token(SyntaxKind.AsyncKeyword))
                                    .AddParameterListParameters(
                                        SyntaxFactory.Parameter(
                                            SyntaxFactory.Identifier("command"))
                                            .WithType(SyntaxFactory.ParseTypeName(commandName)),
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

        private async Task<CodeAction[]> SendCommandAddCodeActionAsync(GenericNameSyntax genericName, Document document, CancellationToken cancellationToken)
        {
            var commandType = genericName.TypeArgumentList.Arguments[0] as IdentifierNameSyntax;
            var dtoType = genericName.TypeArgumentList.Arguments[1] as IdentifierNameSyntax;

            if (commandType == null || !commandType.Identifier.Text.EndsWith(COMMAND_SUFFIX))
                return Array.Empty<CodeAction>();

            var solution = document?.Project?.Solution;
            if (solution == null)
                return Array.Empty<CodeAction>();

            var @namespace = await GetNamespaceAsync(document, genericName.Span, cancellationToken);
            var serviceName = GetServiceName(@namespace);

            if (string.IsNullOrEmpty(serviceName))
                return Array.Empty<CodeAction>();

            // Create the code action
            var codeAction = CodeAction.Create(COMMAND_DESCRIPTION,
                cancellation => GenerateCommandIncludesRelatedClassesAsync(document, serviceName, commandType.Identifier.Text, dtoType?.Identifier.Text, cancellation),
                equivalenceKey: nameof(RequestCodeRefactoringProvider));

            return new[] { codeAction };
        }
    }
}