namespace ClassGenerator.CodeRefactorings
{
    public partial class RequestCodeRefactoringProvider
    {
        private const string QUERY_SUFFIX = "Query";
        private const string INTERFACE_QUERY = "IQuery";
        private const string ABSTRACT_QUERY_HANDLER = "QueryHandler";
        private const string COMMAND_QUERY_SUFFIX = "QueryHandler";
        private const string PROJECT_QUERY = "Aurora.{0}.Domain";
        private const string PROJECT_QUERY_HANDLER = "Aurora.{0}.ApplicationService";
        private const string NAMESPACE_QUERY = "Aurora.{0}.Domain.Queries";
        private const string NAMESPACE_QUERY_HANDLER = "Aurora.{0}.ApplicationService.Queries";
        private readonly string[] QUERY_FOLDERS = new[] { "Queries" };

        private readonly string[] USINGS_QUERY = new[] {
            "Aurora.{0}.Domain.Dtos",
            "Travel2Pay.Cqrs.Queries",
        };

        private readonly string[] USINGS_QUERY_HANDLER = new[] {
            "Aurora.{0}.Domain.Queries",
            "Aurora.{0}.Domain.Dtos",
            "Travel2Pay.Cqrs.Queries"
        };
    }
}