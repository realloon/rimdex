using Rimdex.Data;

namespace Rimdex.Search;

internal sealed record SearchResult(SearchModRow Row, float Distance, double RankScore);