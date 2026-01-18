namespace Academikus.AgenteInteligenteMentoresWebApi.Business.Common.Salesforce.ApiClient;

public interface ISalesforceApiClient
{
    Task<string> GetAsync(string requestUri, string accessToken);
    Task<string?> QuerySingleValueAsync(string soql, string field, string accessToken);
    Task<string> PostAsync(string requestUri, string accessToken, string jsonBody);
    Task<string> RequestTokenAsync(string tokenRelativeUrl, IEnumerable<KeyValuePair<string, string>> form);
    Task<List<string>> GetCasePicklistValuesAsync(string fieldName, string accessToken);

    Task<dynamic> DescribeObjectAsync(string objectName, string accessToken);
}
