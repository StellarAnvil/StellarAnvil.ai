using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace StellarAnvil.Application.Skills;

/// <summary>
/// Jira MCP (Model Context Protocol) skills for Business Analysts
/// </summary>
public class JiraMcpSkills
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public JiraMcpSkills(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    [KernelFunction, Description("Connect to Jira instance")]
    public async Task<string> ConnectJiraAsync(
        [Description("Jira base URL")] string baseUrl,
        [Description("API token or password")] string apiToken,
        [Description("Username or email")] string username)
    {
        try
        {
            // Test connection by getting user info
            var authString = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{apiToken}"));
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

            var response = await _httpClient.GetAsync($"{baseUrl}/rest/api/3/myself");
            
            if (response.IsSuccessStatusCode)
            {
                var userInfo = await response.Content.ReadAsStringAsync();
                return $"Successfully connected to Jira at {baseUrl}. User info: {userInfo}";
            }
            else
            {
                return $"Failed to connect to Jira: {response.StatusCode} - {response.ReasonPhrase}";
            }
        }
        catch (Exception ex)
        {
            // Return tool call for connection request
            return JsonSerializer.Serialize(new
            {
                type = "function",
                function = new
                {
                    name = "connect_jira",
                    arguments = JsonSerializer.Serialize(new
                    {
                        message = "Please connect Jira to track requirements and issues",
                        baseUrl,
                        error = ex.Message
                    })
                }
            });
        }
    }

    [KernelFunction, Description("Create a new Jira issue")]
    public async Task<string> CreateJiraIssueAsync(
        [Description("Project key")] string projectKey,
        [Description("Issue type (Story, Task, Bug, etc.)")] string issueType,
        [Description("Issue summary")] string summary,
        [Description("Issue description")] string description,
        [Description("Priority (Highest, High, Medium, Low, Lowest)")] string priority = "Medium")
    {
        try
        {
            var jiraBaseUrl = _configuration["Jira:BaseUrl"];
            if (string.IsNullOrEmpty(jiraBaseUrl))
            {
                return JsonSerializer.Serialize(new
                {
                    type = "function",
                    function = new
                    {
                        name = "connect_jira",
                        arguments = JsonSerializer.Serialize(new
                        {
                            message = "Jira is not configured. Please provide Jira connection details."
                        })
                    }
                });
            }

            var issueData = new
            {
                fields = new
                {
                    project = new { key = projectKey },
                    summary,
                    description = new
                    {
                        type = "doc",
                        version = 1,
                        content = new[]
                        {
                            new
                            {
                                type = "paragraph",
                                content = new[]
                                {
                                    new { type = "text", text = description }
                                }
                            }
                        }
                    },
                    issuetype = new { name = issueType },
                    priority = new { name = priority }
                }
            };

            var json = JsonSerializer.Serialize(issueData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{jiraBaseUrl}/rest/api/3/issue", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return $"Successfully created Jira issue: {result}";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Failed to create Jira issue: {response.StatusCode} - {error}";
            }
        }
        catch (Exception ex)
        {
            return $"Error creating Jira issue: {ex.Message}";
        }
    }

    [KernelFunction, Description("Search for Jira issues")]
    public async Task<string> SearchJiraIssuesAsync(
        [Description("JQL (Jira Query Language) search string")] string jql,
        [Description("Maximum number of results")] int maxResults = 50)
    {
        try
        {
            var jiraBaseUrl = _configuration["Jira:BaseUrl"];
            if (string.IsNullOrEmpty(jiraBaseUrl))
            {
                return JsonSerializer.Serialize(new
                {
                    type = "function",
                    function = new
                    {
                        name = "connect_jira",
                        arguments = JsonSerializer.Serialize(new
                        {
                            message = "Jira is not configured. Please provide Jira connection details."
                        })
                    }
                });
            }

            var encodedJql = Uri.EscapeDataString(jql);
            var response = await _httpClient.GetAsync($"{jiraBaseUrl}/rest/api/3/search?jql={encodedJql}&maxResults={maxResults}");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return $"Jira search results: {result}";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Failed to search Jira issues: {response.StatusCode} - {error}";
            }
        }
        catch (Exception ex)
        {
            return $"Error searching Jira issues: {ex.Message}";
        }
    }

    [KernelFunction, Description("Update a Jira issue")]
    public async Task<string> UpdateJiraIssueAsync(
        [Description("Issue key (e.g., PROJ-123)")] string issueKey,
        [Description("Fields to update as JSON")] string fieldsJson)
    {
        try
        {
            var jiraBaseUrl = _configuration["Jira:BaseUrl"];
            if (string.IsNullOrEmpty(jiraBaseUrl))
            {
                return JsonSerializer.Serialize(new
                {
                    type = "function",
                    function = new
                    {
                        name = "connect_jira",
                        arguments = JsonSerializer.Serialize(new
                        {
                            message = "Jira is not configured. Please provide Jira connection details."
                        })
                    }
                });
            }

            var updateData = new { fields = JsonSerializer.Deserialize<object>(fieldsJson) };
            var json = JsonSerializer.Serialize(updateData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{jiraBaseUrl}/rest/api/3/issue/{issueKey}", content);
            
            if (response.IsSuccessStatusCode)
            {
                return $"Successfully updated Jira issue {issueKey}";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Failed to update Jira issue: {response.StatusCode} - {error}";
            }
        }
        catch (Exception ex)
        {
            return $"Error updating Jira issue: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get Jira issue details")]
    public async Task<string> GetJiraIssueAsync(
        [Description("Issue key (e.g., PROJ-123)")] string issueKey)
    {
        try
        {
            var jiraBaseUrl = _configuration["Jira:BaseUrl"];
            if (string.IsNullOrEmpty(jiraBaseUrl))
            {
                return JsonSerializer.Serialize(new
                {
                    type = "function",
                    function = new
                    {
                        name = "connect_jira",
                        arguments = JsonSerializer.Serialize(new
                        {
                            message = "Jira is not configured. Please provide Jira connection details."
                        })
                    }
                });
            }

            var response = await _httpClient.GetAsync($"{jiraBaseUrl}/rest/api/3/issue/{issueKey}");
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return $"Jira issue details: {result}";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Failed to get Jira issue: {response.StatusCode} - {error}";
            }
        }
        catch (Exception ex)
        {
            return $"Error getting Jira issue: {ex.Message}";
        }
    }

    [KernelFunction, Description("Add comment to Jira issue")]
    public async Task<string> AddJiraCommentAsync(
        [Description("Issue key (e.g., PROJ-123)")] string issueKey,
        [Description("Comment text")] string comment)
    {
        try
        {
            var jiraBaseUrl = _configuration["Jira:BaseUrl"];
            if (string.IsNullOrEmpty(jiraBaseUrl))
            {
                return JsonSerializer.Serialize(new
                {
                    type = "function",
                    function = new
                    {
                        name = "connect_jira",
                        arguments = JsonSerializer.Serialize(new
                        {
                            message = "Jira is not configured. Please provide Jira connection details."
                        })
                    }
                });
            }

            var commentData = new
            {
                body = new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                        new
                        {
                            type = "paragraph",
                            content = new[]
                            {
                                new { type = "text", text = comment }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(commentData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{jiraBaseUrl}/rest/api/3/issue/{issueKey}/comment", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return $"Successfully added comment to Jira issue {issueKey}: {result}";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Failed to add comment to Jira issue: {response.StatusCode} - {error}";
            }
        }
        catch (Exception ex)
        {
            return $"Error adding comment to Jira issue: {ex.Message}";
        }
    }
}
