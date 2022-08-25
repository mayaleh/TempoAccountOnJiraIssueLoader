// See https://aka.ms/new-console-template for more information
using TempoAccountOnJiraIssueLoader;
using Maya.Ext.Rop;
using Maya.Ext;

var filePath = HandleValidInput("Enter Excel file path"); // Singleton

var excelService = new ExcelService(); // Singleton

await LoadIssuesKeysFromExcelProgram(filePath, excelService)
    .BindAsync(issuesKeys => LoadJiraIssuesProgram(issuesKeys))
    .BindAsync(async issues =>
    {
        return await LoadTempoAccountsProgram()
            .MapAsync(accounts => Task.FromResult((issues.Issues, Accounts: accounts))); // closure on issues
         
    })
    .BindAsync(async data => await FillIssuesTempoAccountIntoExcelProgram(filePath, excelService, data.Issues, data.Accounts))
    .MatchFailureAsync(fail =>
    {
        Console.WriteLine(fail.Message);
        return Task.CompletedTask;
    });

// Program Workflows:

static async Task<Result<IssuesSearchResponse, Exception>> LoadJiraIssuesProgram(List<string> issuesKeys)
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
        Keys = issuesKeys.Any() ? issuesKeys : HandleValidInput("Enter Issues separated by comma").Split(',').ToList()
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

static async Task<Result<AccountResponse[], Exception>> LoadTempoAccountsProgram()
{
    var tempoService = new TempoService(HandleValidInput("Enter TEMPO access token"));

    var tempoAccountsResult = await tempoService.Accounts();

    tempoAccountsResult.MatchFailureAction(fail =>
    {
        Console.WriteLine("Failled to load TEMPO Accounts");
    });

    return tempoAccountsResult.Map(r => r.Results);
}

static async Task<Result<List<string>, Exception>> LoadIssuesKeysFromExcelProgram(string filePath, ExcelService excelService)
{
    try
    {
        if (File.Exists(filePath) == false)
        {
            throw new Exception($"File {filePath} does not exists!");
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
        var results = await excelService.ReadIIssuesKeysFileAsync(stream);
        if (results.Any(x => x.IsFailure))
        {
            results.ForEach(x =>
            {
                if (x.IsSuccess) return;
                Console.WriteLine($"Error in excel Row {x.Failure.RowNr}: {x.Failure.Exception.Message}");
            });
        }

        var issues = results.Where(x => x.IsSuccess)
            .Select(x => x.Success)
            .Distinct()
            .ToList();

        return Result<List<string>, Exception>.Succeeded(issues ?? new());
    }
    catch (Exception e)
    {
        return Result<List<string>, Exception>.Failed(e);
    }
}

static async Task<Result<Unit, Exception>> FillIssuesTempoAccountIntoExcelProgram(string filePath, ExcelService excelService, List<IssueSearchResponse> issues, AccountResponse[] accounts)
{
    try
    {
        var issueAccountKey = new Dictionary<string, AccountResponse>(issues.Count);

        foreach (var issue in issues)
        {
            if (issue.Fields == null || issue.Fields?.IoTempoJira__account == null)
            {
                continue;
            }

            var account = accounts.FirstOrDefault(a => a.Id == issue.Fields.IoTempoJira__account.Id);

            if (account == null)
            {
                continue;
            }

            issueAccountKey.Add(issue.Key, account);
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);

        return await excelService.FillAccountToExcel(stream, issueAccountKey);
    }
    catch (Exception e)
    {
        return Result<Unit, Exception>.Failed(e);
    }
}

// Program Helper functions

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

    return input!;
}