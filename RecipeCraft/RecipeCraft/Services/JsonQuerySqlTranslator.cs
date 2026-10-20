using RecipeCraft.Models;

namespace RecipeCraft.Services
{
    public sealed record SqlSelectCommand(
        string Sql,
        IReadOnlyDictionary<string, object> Parameters,
        string ReadableSql);

    public static class JsonQuerySqlTranslator
    {
        private const string BaseSelect = @"
            SELECT r.Id, r.Title, r.CategoryId, r.PrepTimeMinutes, r.Ingredients, r.Instructions, r.CreatedBy, r.CreatedOn, r.ImageUrl 
            FROM Recipes AS r
            LEFT JOIN Categories AS c ON r.CategoryId = c.Id";

        private static readonly IReadOnlyDictionary<string, string> AllowedColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = "r.Title",
                ["заглавие"] = "r.Title",
                ["име"] = "r.Title",
                ["category"] = "c.Name",
                ["категория"] = "c.Name",
                ["ingredients"] = "r.Ingredients",
                ["съставки"] = "r.Ingredients",
                ["продукти"] = "r.Ingredients",
                ["instructions"] = "r.Instructions",
                ["инструкции"] = "r.Instructions",
                ["начин на приготвяне"] = "r.Instructions",
                ["preptimeminutes"] = "r.PrepTimeMinutes",
                ["време"] = "r.PrepTimeMinutes",
                ["време за приготвяне"] = "r.PrepTimeMinutes",
                ["createdby"] = "r.CreatedBy",
                ["автор"] = "r.CreatedBy",
                ["createdon"] = "r.CreatedOn"
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
                string op = (cond.Operator ?? "contains").ToLowerInvariant();

                if (column == "PrepTimeMinutes" && int.TryParse(rawValue, out int numericVal))
                {
                    string sqlOp = op switch
                    {
                        "lessthan" => "<",
                        "greaterthan" => ">",
                        "equals" => "=",
                        _ => "<="
                    };

                    whereParts.Add($"{column} {sqlOp} {paramName}");
                    readableWhereParts.Add($"{column} {sqlOp} {numericVal}");
                    parameters[paramName] = numericVal;
                }
                else
                {
                    whereParts.Add($"LOWER({column}) LIKE LOWER({paramName})");
                    readableWhereParts.Add($"LOWER({column}) LIKE LOWER('%{rawValue}%')");
                    parameters[paramName] = $"%{rawValue}%";
                }
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