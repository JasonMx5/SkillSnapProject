using System.Net.Http.Json;
using SkillSnap.Shared.Models;

namespace SkillSnap.Client.Services;

public class SkillService
{
    private readonly HttpClient _httpClient;

    public SkillService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("Api");
    }

    public async Task<List<Skill>> GetSkillsAsync()
    {
        var skills = await _httpClient.GetFromJsonAsync<List<Skill>>("api/skills");
        return skills ?? throw new InvalidOperationException("The skills endpoint returned an empty response.");
    }

    public async Task AddSkillAsync(Skill newSkill)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/skills", newSkill);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateSkillAsync(Skill skill)
    {
        using var response = await _httpClient.PutAsJsonAsync($"api/skills/{skill.Id}", skill);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSkillAsync(int skillId)
    {
        using var response = await _httpClient.DeleteAsync($"api/skills/{skillId}");
        response.EnsureSuccessStatusCode();
    }
}
