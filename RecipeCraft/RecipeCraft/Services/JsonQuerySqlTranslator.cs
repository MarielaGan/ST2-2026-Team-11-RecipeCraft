using RecipeCraft.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            var conditions = query.Conditions ?? new List<QueryCondition>();
            int paramIndex = 0;

            foreach (var cond in conditions)
            {
                if (cond is null || string.IsNullOrWhiteSpace(cond.Field) || string.IsNullOrWhiteSpace(cond.Value))
                    continue;

                var column = ResolveColumn(cond.Field);
                string paramName = $"$p{paramIndex++}";
                string rawValue = cond.Value.Trim();
                string op = (cond.Operator ?? "contains").Trim().ToLowerInvariant();

                if (column == "r.PrepTimeMinutes" && int.TryParse(rawValue, out int numericVal))
                {
                    
                    string sqlOp = op switch
                    {
                        "equals" or "eq" or "=" or "contains" => "=",
                        "notequals" or "neq" or "!=" or "<>" => "<>",
                        "gt" or ">" => ">",
                        "lt" or "<" => "<",
                        "gte" or ">=" => ">=",
                        "lte" or "<=" => "<=",
                        _ => throw new ArgumentException($"Неподдържан оператор'{cond.Operator}' за числовата колона PrepTimeMinutes.")
                    };

                    whereParts.Add($"{column} {sqlOp} {paramName}");
                    readableWhereParts.Add($"{column} {sqlOp} {numericVal}");
                    parameters[paramName] = numericVal;
                }
                else if (column == "r.Ingredients" || column == "Ingredients")
                {
                    string paramNameTitle = $"$p{paramIndex++}";

                    whereParts.Add($"(LOWER({column}) LIKE LOWER({paramName}) OR LOWER(r.Title) LIKE LOWER({paramNameTitle}))");
                    readableWhereParts.Add($"(LOWER({column}) LIKE LOWER('%{rawValue}%') OR LOWER(r.Title) LIKE LOWER('%{rawValue}%'))");

                    parameters[paramName] = $"%{rawValue}%";
                    parameters[paramNameTitle] = $"%{rawValue}%";
                }
                else
                {
                    whereParts.Add($"LOWER({column}) LIKE LOWER({paramName})");
                    readableWhereParts.Add($"LOWER({column}) LIKE LOWER('%{rawValue}%')");
                    parameters[paramName] = $"%{rawValue}%";
                }
            }

            var logicalOp = NormalizeLogicalOperator(query.LogicalOperator);
            string whereSql = whereParts.Count > 0 ? " WHERE " + string.Join($" {logicalOp} ", whereParts) : "";
            string readableWhereSql = readableWhereParts.Count > 0 ? " WHERE " + string.Join($" {logicalOp} ", readableWhereParts) : "";

            string orderByCol = AllowedColumns.TryGetValue(query.OrderBy ?? "", out var col) ? col : "r.Title";
            string sortDir = string.Equals(query.SortDirection, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            string orderSql = $" ORDER BY {orderByCol} {sortDir}";

            return new SqlSelectCommand(
                Sql: BaseSelect + whereSql + orderSql,
                Parameters: parameters,
                ReadableSql: BaseSelect + readableWhereSql + orderSql
            );
        }
        private static string ResolveColumn(string field)
        {
            var trimmed = field.Trim();
            if (AllowedColumns.TryGetValue(trimmed, out var column))
            {
                return column;
            }

            throw new ArgumentException($"Невалидно или непозволено име на колона: '{field}'.");
        }

        private static string NormalizeLogicalOperator(string? logicalOperator)
        {
            if (string.IsNullOrWhiteSpace(logicalOperator))
            {
                return "AND";
            }

            var normalized = logicalOperator.Trim().ToUpperInvariant();
            return normalized switch
            {
                "AND" or "И" => "AND",
                "OR" or "ИЛИ" => "OR",
                _ => throw new ArgumentException($"Невалиден логически оператор: '{logicalOperator}'. Позволени са само AND и OR.")
            };
        }
    }
}