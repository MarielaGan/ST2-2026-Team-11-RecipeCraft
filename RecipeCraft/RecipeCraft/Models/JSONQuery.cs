using System.Text.Json.Serialization;

namespace RecipeCraft.Models
{
    public class JSONQuery
    {
        [JsonPropertyName("logicalOperator")]
        public string LogicalOperator { get; set; } = "AND";

        [JsonPropertyName("conditions")]
        public List<QueryCondition> Conditions { get; set; } = new();

        [JsonPropertyName("orderBy")]
        public string? OrderBy { get; set; } = "Title";

        [JsonPropertyName("sortDirection")]
        public string? SortDirection { get; set; } = "ASC";

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }
    }

    public sealed class QueryCondition
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("operator")]
        public string Operator { get; set; } = "contains";

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}

