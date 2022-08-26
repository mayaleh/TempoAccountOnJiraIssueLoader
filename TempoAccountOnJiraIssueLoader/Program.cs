// See https://aka.ms/new-console-template for more information

using TempoAccountOnJiraIssueLoader;
using Maya.Ext.Rop;
using Maya.Ext;

var filePath = HandleValidInput("Enter Excel file path"); // Singleton

var excelService = new ExcelService(); // Singleton

await LoadIssuesKeysFromExcelProgram(filePath, excelService)
    .BindAsync(issuesKeys =>
    {
        var r = LoadUserConfigProgram()
            .MapAsync(userConfig => Task.FromResult((UserConfig: userConfig, IssuesKeys: issuesKeys)));
        return r;
    })
    .BindAsync(configIssuesKeys =>
    {
        return LoadJiraIssuesProgram(configIssuesKeys.UserConfig, configIssuesKeys.IssuesKeys)
            .MapAsync(issues => Task.FromResult((configIssuesKeys.UserConfig, Issues: issues)));
    })
    .BindAsync(data =>
    {
        return LoadTempoAccountsProgram(data.UserConfig)
            .MapAsync(accounts => Task.FromResult((data.Issues.Issues, Accounts: accounts))); // closure on issues
    })
    .BindAsync(data => FillIssuesTempoAccountIntoExcelProgram(filePath, excelService, data.Issues, data.Accounts))
    .MatchFailureAsync(fail =>
    {
        Console.WriteLine(fail.Message);
        return Task.CompletedTask;
    });

// Program Workflows:

static async Task<Result<UserConfig, Exception>> LoadUserConfigProgram()
{
    try
    {
        var hasConfig =
            HandleValidInput(
                "If you want to use JSON config type Y and press enter, otherwise you will set the required data manually.");
        if (hasConfig.Equals("Y") == false)
        {
            return Result<UserConfig, Exception>.Succeeded(new UserConfig(string.Empty, string.Empty, string.Empty,
                string.Empty));
        }

        var configFilePath = HandleValidInput("Enter JSON config file path:");
        if (File.Exists(configFilePath) == false)
        {
            return Result<UserConfig, Exception>.Failed(new Exception($"Json config file not found! {configFilePath}"));
        }

        var json = await File.ReadAllTextAsync(configFilePath);

        var userConfig = System.Text.Json.JsonSerializer.Deserialize<UserConfig>(json);

        return Result<UserConfig, Exception>.Succeeded((userConfig ?? new UserConfig(string.Empty, string.Empty,
            string.Empty,
            string.Empty)));
    }
    catch (Exception e)
    {
        return Result<UserConfig, Exception>.Failed(e);
    }
}

static async Task<Result<IssuesSearchResponse, Exception>> LoadJiraIssuesProgram(UserConfig userConfig,
    List<string> issuesKeys)
{
    var (endpoint, email, apiKey) = (
        HandleValidInput("Enter JIRA endpoint in this format: https://mytenant.atlassian.net",
            userConfig.JiraEndpoint,
            (i) => string.IsNullOrWhiteSpace(i) == false
                   || !i.Contains("atlassian.net")
                   || !i.StartsWith("https://")),
        HandleValidInput("Enter JIRA email", userConfig.JiraEmail,
            (i) => string.IsNullOrWhiteSpace(i) == false || !i.Contains('@')),
        HandleValidInput("Enter JIRA (Atlassian) api Key created in your atlassian profile", userConfig.JiraApiKey));

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

static async Task<Result<AccountResponse[], Exception>> LoadTempoAccountsProgram(UserConfig userConfig)
{
    var tempoService = new TempoService(HandleValidInput("Enter TEMPO access token", userConfig.TempoAccessToken));

    var tempoAccountsResult = await tempoService.Accounts();

    tempoAccountsResult.MatchFailureAction(fail => { Console.WriteLine("Failled to load TEMPO Accounts"); });

    return tempoAccountsResult.Map(r => r.Results);
}

static async Task<Result<List<string>, Exception>> LoadIssuesKeysFromExcelProgram(string filePath,
    ExcelService excelService)
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

static async Task<Result<Unit, Exception>> FillIssuesTempoAccountIntoExcelProgram(string filePath,
    ExcelService excelService, List<IssueSearchResponse> issues, AccountResponse[] accounts)
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

        //using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write);
        //await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Write);
        return await excelService.FillAccountToExcel(filePath, issueAccountKey);
    }
    catch (Exception e)
    {
        return Result<Unit, Exception>.Failed(e);
    }
}

// Program Helper functions

static string HandleValidInput(string message, string? initValue = null, Func<string?, bool>? isInvalid = null)
{
    isInvalid ??= (i) => string.IsNullOrWhiteSpace(i) == false;

    if (isInvalid.Invoke(initValue) != false)
    {
        return initValue!;
    }

    Console.WriteLine(message);
    var input = Console.ReadLine();

    while (isInvalid.Invoke(input) == false)
    {
        Console.WriteLine("Invalid input! Please fill valid and required info:");
        Console.WriteLine(message);
        input = Console.ReadLine();
    }

    return input!;
}


internal record UserConfig(string JiraEmail, string JiraApiKey, string JiraEndpoint, string TempoAccessToken);