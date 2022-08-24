// See https://aka.ms/new-console-template for more information
using TempoAccountOnJiraIssueLoader;
using Maya.Ext.Rop;

await LoadJiraIssuesProgram()
    .BindAsync(async issues =>
    {
        return await LoadTempoAccountsProgram()
            .MapAsync(accounts => Task.FromResult((Issues: issues, Accounts: accounts)));
         
    })
    // TODO: fill excel account key for issues worklog program
    .MatchFailureAsync(fail =>
    {
        Console.WriteLine(fail.Message);
        return Task.CompletedTask;
    }); ;

static async Task<Maya.Ext.Rop.Result<IssuesSearchResponse, Exception>> LoadJiraIssuesProgram()
{
    var (endpoint, email, apiKey) = (
            HandleValidInput("Enter JIRA endpoint in this format: https://mytenant.atlassian.net",
            (i) => string.IsNullOrWhiteSpace(i)
                || !i.Contains("atlassian.net")
                || !i.StartsWith("https://")),
            HandleValidInput("Enter JIRA email", (i) => string.IsNullOrWhiteSpace(i) || !i.Contains('@')),
            HandleValidInput("Enter JIRA (Atlassian) api Key created in your atlassian profile"));

    var issues = new IssueSearchRequest()
    {
        Keys = HandleValidInput("Enter Issues separated by comma").Split(',').ToList()
    };

    var jiraService = new JiraService(endpoint, email, apiKey);

    var issuesResonseResult = await jiraService.IssueSearch(issues);

    issuesResonseResult.MatchFailureAction(fail =>
    {
        Console.WriteLine("Failled to load JIRA issues: ");
        issues.Keys.ForEach(k => Console.WriteLine(k));
    });

    return issuesResonseResult;
}

// Load Tempo accounts
static async Task<Maya.Ext.Rop.Result<AccountResponse[], Exception>> LoadTempoAccountsProgram()
{
    var tempoService = new TempoService(HandleValidInput("Enter TEMPO access token"));

    var tempoAccountsResult = await tempoService.Accounts();

    tempoAccountsResult.MatchFailureAction(fail =>
    {
        Console.WriteLine("Failled to load TEMPO Accounts");
    });

    return tempoAccountsResult.Map(r => r.Results);
}

static string HandleValidInput(string message, Func<string?, bool>? isInvalid = null)
{
    Console.WriteLine(message);
    var input = Console.ReadLine();

    isInvalid ??= (i) => string.IsNullOrWhiteSpace(i);
    while (isInvalid.Invoke(input))
    {
        Console.WriteLine("Invalid input! Please fill valid and required info:");
        Console.WriteLine(message);
        input = Console.ReadLine();
    }

    return input;
}