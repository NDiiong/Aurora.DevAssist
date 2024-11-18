namespace ClassGenerator.CodeRefactorings
{
    public partial class RequestCodeRefactoringProvider
    {
        private const string INTERFACE_QUERY = "IQuery";
        private const string QUERY_SUFFIX = "Query";
        private const string INTERFACE_QUERY_HANDLER = "QueryHandler";
        private const string DEFAULT_NAMESPACE_QUERY = "Aurora.{0}.Domain.Queries";
        private const string DEFAULT_NAMESPACE_QUERY_HANDLER = "Aurora.{0}.ApplicationService.Queries.{1}";
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