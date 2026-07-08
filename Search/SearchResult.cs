using Rimdex.Data;

namespace Rimdex.Search;

internal sealed record SearchResult(SearchCandidateRow Row, float Distance, double RankScore);