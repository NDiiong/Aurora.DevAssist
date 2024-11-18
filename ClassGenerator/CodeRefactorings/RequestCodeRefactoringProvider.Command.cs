using ClassGenerator.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassGenerator.CodeRefactorings
{
    public partial class RequestCodeRefactoringProvider
    {
        private const string COMMAND_SUFFIX = "Command";
        private const string INTERFACE_COMMAND = "ICommand";
        private const string ABSTRACT_COMMAND_HANDLER = "CommandHandler";
        private const string COMMAND_HANDLER_SUFFIX = "CommandHandler";
        private const string PROJECT_COMMAND = "Aurora.{0}.Domain";
        private const string PROJECT_COMMAND_HANDLER = "Aurora.{0}.ApplicationService";
        private const string NAMESPACE_COMMAND = "Aurora.{0}.Domain.Commands";
        private const string NAMESPACE_COMMAND_HANDLER = "Aurora.{0}.ApplicationService.Commands";
        private readonly string[] COMMAND_FOLDERS = new[] { "Commands" };

        private readonly string[] USINGS_COMMAND = new[] {
            "Aurora.{0}.Domain.Dtos",
            "Travel2Pay.Cqrs.Commands",
        };

        private readonly string[] USINGS_COMMAND_HANDLER = new[] {
            "Aurora.{0}.Domain.Commands",
            "Travel2Pay.Cqrs.Commands"
        };

        private async Task<Solution> CreateCommandWithHandlerAsync(Document document, string serviceName, string classNameTyping, CancellationToken cancellation)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution = document.Project.Solution;

            // Create Command
            var commandClassName = classNameTyping;
            var commandUsings = USINGS_COMMAND.Select(u => u.Format(serviceName)).ToArray();
            var commandNamespace = NAMESPACE_COMMAND.Format(serviceName);
            var commandProject = PROJECT_COMMAND.Format(serviceName);
            var commandSyntax = GenerateCommandClassSyntax(commandClassName, commandNamespace, commandUsings);

            // Create Handler
            var commandHandlerClassName = commandClassName.Replace(COMMAND_SUFFIX, COMMAND_HANDLER_SUFFIX);
            var commandHandlerUsings = USINGS_COMMAND_HANDLER.Select(u => u.Format(serviceName)).ToArray();
            var commandHandlerNamespace = NAMESPACE_COMMAND_HANDLER.Format(serviceName);
            var commandHandlerProject = PROJECT_COMMAND_HANDLER.Format(serviceName);
            var commandHandlerSyntax = GenerateCommandHandlerClassSyntax(commandClassName, commandHandlerClassName, commandHandlerNamespace, commandHandlerUsings);

            solution = await AddDocumentAsync(solution, commandProject, commandClassName, COMMAND_FOLDERS, commandSyntax);
            solution = await AddDocumentAsync(solution, commandHandlerProject, commandHandlerClassName, COMMAND_FOLDERS, commandHandlerSyntax);

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

        private static CompilationUnitSyntax GenerateCommandClassSyntax(string className, string @namespace, string[] usings)
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
                        SyntaxFactory.IdentifierName(INTERFACE_COMMAND)
                    )
                );

            // Add class to namespace
            namespaceDeclaration = namespaceDeclaration.AddMembers(classDeclaration);

            // Add namespace to compilation unit
            compilationUnit = compilationUnit.AddMembers(namespaceDeclaration);

            return compilationUnit.NormalizeWhitespace();
        }
    }
}