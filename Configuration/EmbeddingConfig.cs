namespace Rimdex.Configuration;

internal sealed record EmbeddingConfig(string ApiKey, string BaseUrl, string Model) {
    public Uri BaseUri {
        get {
            var normalized = BaseUrl.EndsWith("/", StringComparison.Ordinal) ? BaseUrl : $"{BaseUrl}/";
            return new Uri(normalized, UriKind.Absolute);
        }
    }

    public void Validate(string path) {
        if (string.IsNullOrWhiteSpace(ApiKey)) {
            throw new InvalidDataException($"Missing embedding.apiKey in {path}");
        }

        if (string.IsNullOrWhiteSpace(BaseUrl)) {
            throw new InvalidDataException($"Missing embedding.baseUrl in {path}");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _)) {
            throw new InvalidDataException($"Invalid embedding.baseUrl in {path}");
        }

        if (string.IsNullOrWhiteSpace(Model)) {
            throw new InvalidDataException($"Missing embedding.model in {path}");
        }
    }
}