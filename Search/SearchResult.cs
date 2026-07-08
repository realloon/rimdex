using Rimdex.Data;

namespace Rimdex.Search;

internal sealed record SearchResult(SearchEmbeddingRow Row, float Distance, double RankScore);