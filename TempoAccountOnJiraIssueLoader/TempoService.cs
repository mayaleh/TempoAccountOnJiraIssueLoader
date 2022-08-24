using Maya.Ext.Rop;
using System.Collections.Generic;
using System.Text.Json;

namespace TempoAccountOnJiraIssueLoader
{
    internal class TempoService : Maya.AnyHttpClient.ApiService
    {
        const string TempoUri = "https://api.tempo.io/4";
        const int TimeoutRequestSeconds = 30;

        public TempoService(string accessToken) : base(new Maya.AnyHttpClient.Model.HttpClientConnector
        {
            Endpoint = TempoUri,
            TimeoutSeconds = TimeoutRequestSeconds,
            AuthType = Maya.AnyHttpClient.Model.AuthTypeKinds.Bearer,
            Token = accessToken,
            CustomJsonSerializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
        })
        {
        }

        public async Task<Result<DataResponse<AccountResponse>, Exception>> Accounts(int limit = 100)
        {
            try
            {
                var uriRequest = new Maya.AnyHttpClient.Model.UriRequest(new string[] { "accounts" }, new KeyValuePair<string, string>("limit", limit.ToString()));

                return await this.HttpGet<DataResponse<AccountResponse>>(uriRequest)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                return Result<DataResponse<AccountResponse>, Exception>.Failed(e);
            }
        }
    }

    class AccountResponse
    {
        public int Id { get; set; }
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
    }


    public class DataResponse<T>
    {
        public Metadata? Metadata { get; set; }
        public T[] Results { get; set; } = new T[0];
        public string? Self { get; set; }
    }

    public class Metadata
    {
        public int Count { get; set; }
        public int Limit { get; set; }
        public string? Next { get; set; }
        public int Offset { get; set; }
        public string? Previous { get; set; }
    }
}
