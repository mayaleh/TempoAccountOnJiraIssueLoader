using Maya.AnyHttpClient.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TempoAccountOnJiraIssueLoader
{
    internal class JiraService : Maya.AnyHttpClient.ApiService
    {
        public JiraService(string endpoint, string email, string apiToken) : base(new Maya.AnyHttpClient.Model.HttpClientConnector
        {
            Endpoint = endpoint,
            TimeoutSeconds = 30,
            AuthType = Maya.AnyHttpClient.Model.AuthTypeKinds.Basic,
            UserName = email,
            Password = apiToken,
            CustomJsonSerializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
        })
        {
        }

        public async Task<Maya.Ext.Rop.Result<IssuesSearchResponse, Exception>> IssueSearch(IssueSearchRequest request)
        {
            try
            {
                var keysCondition = "key in (" + string.Join(',', request.Keys.Select(k => $"\"{k}\"")) + ")";
                var searchRequest = new SearchRequest()
                {
                    Fields = new()
                    {
                        "key",
                        "io.tempo.jira__account"
                    },
                    Properties = new()
                    {
                        "internal"
                    },
                    StartAt = 0,
                    MaxResults = 200,
                    Jql = keysCondition,
                };

                // /rest/api/2/search/
                var uriRequest = new Maya.AnyHttpClient.Model.UriRequest(new string[] { "rest", "api", "2", "search" });

                return await this.HttpPost<IssuesSearchResponse>(uriRequest, searchRequest);
            }
            catch (Exception e)
            {
                return Maya.Ext.Rop.Result<IssuesSearchResponse, Exception>.Failed(e);
            }
        }
    }

    class SearchRequest
    {
        public string Jql { get; set; } = null!;
        public string ValidateQuery { get; set; } = "none";
        public List<string> Fields { get; set; } = new();
        public List<string> Properties { get; set; } = new();
        public int StartAt { get; set; }
        public int MaxResults { get; set; }
    }

    class IssueSearchRequest // create jql query builder like rpc2
    {
        public List<string> Keys { get; set; } = null!;
    }

    class SearchResponse
    {
        public string Expand { get; set; } = null!;
        public int StartAt { get; set; }
        public int MaxResults { get; set; }
        public int Total { get; set; }
    }

    class IssuesSearchResponse : SearchResponse
    {
        public List<IssueSearchResponse> Issues { get; set; } = new List<IssueSearchResponse>();
    }

    public class IssueSearchResponse
    {
        public string Expand { get; set; } = null!;
        public string Id { get; set; } = null!;
        public string Self { get; set; } = null!;
        public string Key { get; set; } = null!;
        public Properties? Properties { get; set; }
        public Fields? Fields { get; set; }
    }

    public class Properties
    {
    }

    public class Fields
    {
        [JsonPropertyName("io.tempo.jira__account")]
        public IoTempoJira__Account? IoTempoJira__account { get; set; }
    }

    public class IoTempoJira__Account
    {
        public int Id { get; set; }
        public string Value { get; set; } = null!;
    }

}
