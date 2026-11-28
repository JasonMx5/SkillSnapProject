using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using SkillSnap.Shared.Models;

namespace SkillSnap.Client.Services;

public class ProjectService
{
    public const long MaxImageFileSizeBytes = 5 * 1024 * 1024;

    private readonly HttpClient _httpClient;

    public ProjectService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("Api");
    }

    public async Task<List<Project>> GetProjectsAsync()
    {
        var projects = await _httpClient.GetFromJsonAsync<List<Project>>("api/projects");
        return projects ?? throw new InvalidOperationException("The projects endpoint returned an empty response.");
    }

    public async Task<ProjectDetailsResponse> GetProjectDetailsAsync(int projectId)
    {
        var details = await _httpClient.GetFromJsonAsync<ProjectDetailsResponse>($"api/projects/{projectId}");
        return details ?? throw new InvalidOperationException("The project details endpoint returned an empty response.");
    }

    public async Task<List<ProjectEnrollmentSummary>> GetMyEnrollmentsAsync()
    {
        var enrollments = await _httpClient.GetFromJsonAsync<List<ProjectEnrollmentSummary>>("api/projects/enrollments");
        return enrollments ?? throw new InvalidOperationException("The enrollment endpoint returned an empty response.");
    }

    public async Task<List<ProjectEnrollmentAdminSummary>> GetProjectEnrollmentsAsync(int projectId)
    {
        var enrollments = await _httpClient.GetFromJsonAsync<List<ProjectEnrollmentAdminSummary>>(
            $"api/projects/{projectId}/enrollments");
        return enrollments ?? throw new InvalidOperationException("The project enrollment endpoint returned an empty response.");
    }

    public async Task UpdateProjectEnrollmentStatusAsync(
        int projectId,
        string userId,
        ProjectEnrollmentStatus status)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/projects/{projectId}/enrollments/{Uri.EscapeDataString(userId)}/status",
            new ProjectEnrollmentStatusUpdateRequest(status));
        response.EnsureSuccessStatusCode();
    }

    public async Task EnrollInProjectAsync(int projectId)
    {
        using var response = await _httpClient.PostAsync($"api/projects/{projectId}/enroll", content: null);
        response.EnsureSuccessStatusCode();
    }

    public async Task UnenrollFromProjectAsync(int projectId)
    {
        using var response = await _httpClient.DeleteAsync($"api/projects/{projectId}/enroll");
        response.EnsureSuccessStatusCode();
    }

    public async Task AddProjectAsync(Project newProject)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/projects", newProject);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateProjectAsync(Project project)
    {
        using var response = await _httpClient.PutAsJsonAsync($"api/projects/{project.Id}", project);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProjectAsync(int projectId)
    {
        using var response = await _httpClient.DeleteAsync($"api/projects/{projectId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> UploadProjectImageAsync(IBrowserFile file)
    {
        if (file.Size > MaxImageFileSizeBytes)
        {
            throw new InvalidOperationException("Images must be 5 MB or smaller.");
        }

        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(file.OpenReadStream(MaxImageFileSizeBytes));
        content.Add(fileContent, "file", Path.GetFileName(file.Name));

        using var response = await _httpClient.PostAsync("api/projects/images", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProjectImageUploadResponse>();
        return string.IsNullOrWhiteSpace(result?.ImageUrl)
            ? throw new InvalidOperationException("The image upload endpoint returned no image URL.")
            : result.ImageUrl;
    }

    public string? GetImageSource(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteImageUrl))
        {
            if (string.Equals(absoluteImageUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(absoluteImageUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return absoluteImageUrl.AbsoluteUri;
            }

            if (!absoluteImageUrl.IsFile ||
                !absoluteImageUrl.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            imageUrl = absoluteImageUrl.AbsolutePath;
        }

        var apiBaseAddress = _httpClient.BaseAddress
            ?? throw new InvalidOperationException("The API base address is not configured.");
        return Uri.TryCreate(apiBaseAddress, imageUrl, out var resolvedImageUrl)
            ? resolvedImageUrl.AbsoluteUri
            : null;
    }
}
