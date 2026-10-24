using RecipeCraft.Models;

namespace RecipeCraft.Services
{
    public sealed record SqlSelectCommand(string Sql, IReadOnlyDictionary<string, object> Parameters, string ReadableSql);

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
                ["време за приготвяне"] = "r.PrepTimeMinutes"
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

                if (column == "c.Name")
                {
                    string paramNameDesc = $"$p{paramIndex++}";

                    if (op is "notcontains" or "notlike")
                    {
                        whereParts.Add($"(c.Name IS NULL OR (LOWER(c.Name) NOT LIKE LOWER({paramName}) AND LOWER(r.Ingredients) NOT LIKE LOWER({paramNameDesc})))");
                        readableWhereParts.Add($"(c.Name IS NULL OR (LOWER(c.Name) NOT LIKE LOWER('%{EscapeSqlLiteral(rawValue)}%') AND LOWER(r.Ingredients) NOT LIKE LOWER('%{EscapeSqlLiteral(rawValue)}%')))");
                    }
                    else
                    {
                        whereParts.Add($"(LOWER(c.Name) LIKE LOWER({paramName}) OR LOWER(r.Ingredients) LIKE LOWER({paramNameDesc}))");
                        readableWhereParts.Add($"(LOWER(c.Name) LIKE LOWER('%{EscapeSqlLiteral(rawValue)}%') OR LOWER(r.Ingredients) LIKE LOWER('%{EscapeSqlLiteral(rawValue)}%'))");
                    }

                    parameters[paramName] = $"%{rawValue}%";
                    parameters[paramNameDesc] = $"%{rawValue}%";
                }
                else if (column == "r.PrepTimeMinutes" && int.TryParse(rawValue, out int numericVal))
                {

                    string sqlOp = op switch
                    {
                        "equals" or "eq" or "=" or "contains" => "=",
                        "notequals" or "neq" or "!=" or "<>" => "<>",
                        "gt" or "greaterthan" or ">" => ">",
                        "lt" or "lessthan" or "<" => "<",
                        "gte" or "greaterthanorequal" or ">=" => ">=",
                        "lte" or "lessthanorequal" or "<=" => "<=",
                        _ => throw new ArgumentException($"Неподдържан оператор'{cond.Operator}' за числовата колона PrepTimeMinutes.")
                    };

                    whereParts.Add($"{column} {sqlOp} {paramName}");
                    readableWhereParts.Add($"{column} {sqlOp} {numericVal}");
                    parameters[paramName] = numericVal;
                }
                else if (column == "r.Ingredients" || column == "Ingredients")
                {
                    string paramNameTitle = $"$p{paramIndex++}";

                    if (op is "notcontains" or "notlike")
                    {
                        // Отрицание: ИЗПОЛЗВА СЕ AND, ЗА ДА ГО НЯМА НИТО В ПРОДУКТИТЕ, НИТО В ЗАГЛАВИЕТО
                        whereParts.Add($"(r.Ingredients IS NULL OR (LOWER({column}) NOT LIKE LOWER({paramName}) AND LOWER(r.Title) NOT LIKE LOWER({paramNameTitle})))");
                        readableWhereParts.Add($"(r.Ingredients IS NULL OR (LOWER({column}) NOT LIKE LOWER('%{EscapeSqlLiteral(rawValue)}%') AND LOWER(r.Title) NOT LIKE LOWER('%{EscapeSqlLiteral(rawValue)}%')))");
                    }
                    else
                    {
                        // Позитивно търсене: Използва се OR
                        whereParts.Add($"(LOWER({column}) LIKE LOWER({paramName}) OR LOWER(r.Title) LIKE LOWER({paramNameTitle}))");
                        readableWhereParts.Add($"(LOWER({column}) LIKE LOWER('%{EscapeSqlLiteral(rawValue)}%') OR LOWER(r.Title) LIKE LOWER('%{EscapeSqlLiteral(rawValue)}%')))");
                    }

                    parameters[paramName] = $"%{rawValue}%";
                    parameters[paramNameTitle] = $"%{rawValue}%";
                }
                else
                {
                    var (clause, readableClause, paramValue) = BuildTextCondition(column, op, rawValue, paramName);
                    whereParts.Add(clause);
                    readableWhereParts.Add(readableClause);
                    parameters[paramName] = paramValue;
                }
            }

            var logicalOp = NormalizeLogicalOperator(query.LogicalOperator);
            string whereSql = whereParts.Count > 0 ? " WHERE " + string.Join($" {logicalOp} ", whereParts) : "";
            string readableWhereSql = readableWhereParts.Count > 0 ? " WHERE " + string.Join($" {logicalOp} ", readableWhereParts) : "";

            string orderByCol = AllowedColumns.TryGetValue(query.OrderBy ?? "", out var col) ? col : "r.Title";
            var sortDir = NormalizeSortDirection(query.SortDirection);
            string orderSql = $" ORDER BY {orderByCol} {sortDir}";

            return new SqlSelectCommand(
                Sql: BaseSelect + whereSql + orderSql,
                Parameters: parameters,
                ReadableSql: BaseSelect + readableWhereSql + orderSql
            );
        }
        private static (string Clause, string ReadableClause, string ParamValue) BuildTextCondition(
        string column,
        string op,
        string rawValue,
        string paramName)
        {
            return op switch
            {
                "contains" or "like" => (
                    $"LOWER({column}) LIKE LOWER({paramName})",
                    $"LOWER({column}) LIKE LOWER('{EscapeSqlLiteral($"%{rawValue}%")}')",
                    $"%{rawValue}%"),

                "notcontains" or "notlike" => (
                $"({column} IS NULL OR LOWER({column}) NOT LIKE LOWER({paramName}))",
                $"({column} IS NULL OR LOWER({column}) NOT LIKE LOWER('{EscapeSqlLiteral($"%{rawValue}%")}'))",
                $"%{rawValue}%"),

                "equals" or "eq" or "=" => (
                    $"LOWER({column}) = LOWER({paramName})",
                    $"LOWER({column}) = LOWER('{EscapeSqlLiteral(rawValue)}')",
                    rawValue),

                "startswith" => (
                    $"LOWER({column}) LIKE LOWER({paramName})",
                    $"LOWER({column}) LIKE LOWER('{EscapeSqlLiteral($"{rawValue}%")}')",
                    $"{rawValue}%"),

                "endswith" => (
                    $"LOWER({column}) LIKE LOWER({paramName})",
                    $"LOWER({column}) LIKE LOWER('{EscapeSqlLiteral($"%{rawValue}")}')",
                    $"%{rawValue}"),

                "notequals" or "neq" or "!=" or "<>" => (
                    $"({column} IS NULL OR LOWER({column}) <> LOWER({paramName}))",
                    $"({column} IS NULL OR LOWER({column}) <> LOWER('{EscapeSqlLiteral(rawValue)}'))",
                rawValue),

                _ => throw new ArgumentException($"Неподдържан оператор за текстово поле: '{op}'.")
            };
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
        private static string NormalizeSortDirection(string? sortDirection)
        {
            if (string.IsNullOrWhiteSpace(sortDirection))
            {
                return "ASC";
            }

            var normalized = sortDirection.Trim().ToUpperInvariant();
            return normalized switch
            {
                "ASC" or "ASCENDING" => "ASC",
                "DESC" or "DESCENDING" => "DESC",
                _ => throw new ArgumentException($"Невалидна посока на сортиране: '{sortDirection}'. Позволени са само ASC и DESC.")
            };
        }
        private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");
    }
}