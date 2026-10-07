using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Helpdesk.Tests.Api;

internal static class ProblemAssertions
{
    public static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        return problem;
    }
}
