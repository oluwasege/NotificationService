using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces;

/// <summary>
/// Service for rendering notification templates with variable substitution.
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Renders a template with the provided data.
    /// </summary>
    /// <param name="template">The template string (Scriban syntax)</param>
    /// <param name="data">The data to substitute into the template</param>
    /// <returns>The rendered string</returns>
    Task<string> RenderAsync(string template, Dictionary<string, object>? data);

    /// <summary>
    /// Gets a template by ID and renders it with the provided data.
    /// </summary>
    Task<(string Subject, string Body)?> RenderTemplateAsync(
        Guid templateId, 
        Dictionary<string, object>? data, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new template.
    /// </summary>
    Task<TemplateDto> CreateTemplateAsync(
        Guid subscriptionId, 
        CreateTemplateRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all templates for a subscription.
    /// </summary>
    Task<List<TemplateDto>> GetTemplatesAsync(
        Guid subscriptionId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    Task<TemplateDto?> GetTemplateByIdAsync(
        Guid templateId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a template.
    /// </summary>
    Task<TemplateDto?> UpdateTemplateAsync(
        Guid templateId, 
        UpdateTemplateRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a template.
    /// </summary>
    Task<bool> DeleteTemplateAsync(
        Guid templateId, 
        CancellationToken cancellationToken = default);
}
