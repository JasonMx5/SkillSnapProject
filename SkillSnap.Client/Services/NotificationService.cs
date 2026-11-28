using System.Net.Http.Json;
using SkillSnap.Shared.Models;

namespace SkillSnap.Client.Services;

public class NotificationService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("Api");

    public async Task<List<NotificationResponse>> GetMyNotificationsAsync()
    {
        var notifications = await _httpClient.GetFromJsonAsync<List<NotificationResponse>>("api/notifications");
        return notifications ?? throw new InvalidOperationException("The notifications endpoint returned an empty response.");
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        using var response = await _httpClient.PostAsync($"api/notifications/{notificationId}/read", content: null);
        response.EnsureSuccessStatusCode();
    }
}
