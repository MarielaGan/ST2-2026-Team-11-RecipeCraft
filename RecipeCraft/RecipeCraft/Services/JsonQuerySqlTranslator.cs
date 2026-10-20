using RecipeCraft.Models;

namespace RecipeCraft.Services
{
    public sealed record SqlSelectCommand(
        string Sql,
        IReadOnlyDictionary<string, object> Parameters,
        string ReadableSql);

    public static class JsonQuerySqlTranslator
    {
        private const string BaseSelect = "SELECT Id, Title, CategoryId, PrepTimeMinutes, Ingredients, Instructions FROM Recipes";

        private static readonly IReadOnlyDictionary<string, string> AllowedColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = "Title",
                ["заглавие"] = "Title",
                ["име"] = "Title",
                ["category"] = "CategoryId",
                ["категория"] = "CategoryId",
                ["ingredients"] = "Ingredients",
                ["съставки"] = "Ingredients",
                ["продукти"] = "Ingredients"
            };

        public static SqlSelectCommand Translate(JSONQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var whereParts = new List<string>();
            var readableWhereParts = new List<string>();
            var parameters = new Dictionary<string, object>();
            int paramIndex = 0;

            foreach (var cond in query.Conditions ?? new())
            {
                if (string.IsNullOrWhiteSpace(cond.Field) || string.IsNullOrWhiteSpace(cond.Value))
                    continue;

                if (!AllowedColumns.TryGetValue(cond.Field.Trim(), out var column))
                    throw new ArgumentException($"Непозволена колона за търсене: '{cond.Field}'");

                string paramName = $"$p{paramIndex++}";
                string rawValue = cond.Value.Trim();

                whereParts.Add($"LOWER({column}) LIKE LOWER({paramName})");
                readableWhereParts.Add($"LOWER({column}) LIKE LOWER('%{rawValue}%')");
                parameters[paramName] = $"%{rawValue}%";
            }

            string logicalOp = string.Equals(query.LogicalOperator, "OR", StringComparison.OrdinalIgnoreCase) ? "OR" : "AND";
            string whereSql = whereParts.Count > 0 ? " WHERE " + string.Join($" {logicalOp} ", whereParts) : "";
            string readableWhereSql = readableWhereParts.Count > 0 ? " WHERE " + string.Join($" {logicalOp} ", readableWhereParts) : "";

            string orderByCol = AllowedColumns.TryGetValue(query.OrderBy ?? "", out var col) ? col : "Title";
            string sortDir = string.Equals(query.SortDirection, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            string orderSql = $" ORDER BY {orderByCol} {sortDir}";

            return new SqlSelectCommand(
                Sql: BaseSelect + whereSql + orderSql,
                Parameters: parameters,
                ReadableSql: BaseSelect + readableWhereSql + orderSql
            );
        }
    }
}

