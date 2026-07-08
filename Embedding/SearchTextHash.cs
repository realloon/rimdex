using System.Security.Cryptography;
using System.Text;

namespace Rimdex.Embedding;

internal static class SearchTextHash {
    public static string Compute(string searchText) {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(searchText));
        return Convert.ToHexString(bytes);
    }
}