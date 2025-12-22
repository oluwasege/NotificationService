using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using Scriban;
using Scriban.Runtime;

namespace NotificationService.Application.Services;

/// <summary>
/// Service for managing and rendering notification templates using Scriban.
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(
        IUnitOfWork unitOfWork,
        ILogger<TemplateService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<string> RenderAsync(string template, Dictionary<string, object>? data)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        try
        {
            var scribanTemplate = Template.Parse(template);
            
            if (scribanTemplate.HasErrors)
            {
                var errors = string.Join(", ", scribanTemplate.Messages.Select(m => m.Message));
                _logger.LogWarning("Template parsing errors: {Errors}", errors);
                throw new InvalidOperationException($"Template parsing failed: {errors}");
            }

            var scriptObject = new ScriptObject();
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    scriptObject.Add(kvp.Key, kvp.Value);
                }
            }

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            var result = await scribanTemplate.RenderAsync(context);
            return result;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error rendering template");
            throw new InvalidOperationException($"Template rendering failed: {ex.Message}", ex);
        }
    }

    public async Task<(string Subject, string Body)?> RenderTemplateAsync(
        Guid templateId,
        Dictionary<string, object>? data,
        CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.GetRepository<NotificationTemplate>()
            .QueryNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId && t.IsActive, cancellationToken);

        if (template == null)
        {
            _logger.LogWarning("Template {TemplateId} not found or inactive", templateId);
            return null;
        }

        var subject = await RenderAsync(template.SubjectTemplate, data);
        var body = await RenderAsync(template.BodyTemplate, data);

        return (subject, body);
    }

    public async Task<TemplateDto> CreateTemplateAsync(
        Guid subscriptionId,
        CreateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating template {Name} for subscription {SubscriptionId}", 
            request.Name, subscriptionId);

        // Validate templates compile correctly
        ValidateTemplate(request.SubjectTemplate, "SubjectTemplate");
        ValidateTemplate(request.BodyTemplate, "BodyTemplate");

        var template = new NotificationTemplate
        {
            SubscriptionId = subscriptionId,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            SubjectTemplate = request.SubjectTemplate,
            BodyTemplate = request.BodyTemplate,
            IsActive = true
        };

        await _unitOfWork.GetRepository<NotificationTemplate>().AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created template {Id} for subscription {SubscriptionId}", 
            template.Id, subscriptionId);

        return MapToDto(template);
    }

    public async Task<List<TemplateDto>> GetTemplatesAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var templates = await _unitOfWork.GetRepository<NotificationTemplate>()
            .QueryNoTracking()
            .Where(t => t.SubscriptionId == subscriptionId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return templates.Select(MapToDto).ToList();
    }

    public async Task<TemplateDto?> GetTemplateByIdAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.GetRepository<NotificationTemplate>()
            .GetByIdAsync(templateId, cancellationToken);

        return template == null ? null : MapToDto(template);
    }

    public async Task<TemplateDto?> UpdateTemplateAsync(
        Guid templateId,
        UpdateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.GetRepository<NotificationTemplate>()
            .GetByIdAsync(templateId, cancellationToken);

        if (template == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            template.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            template.Description = request.Description;
        }

        if (!string.IsNullOrWhiteSpace(request.SubjectTemplate))
        {
            ValidateTemplate(request.SubjectTemplate, "SubjectTemplate");
            template.SubjectTemplate = request.SubjectTemplate;
        }

        if (!string.IsNullOrWhiteSpace(request.BodyTemplate))
        {
            ValidateTemplate(request.BodyTemplate, "BodyTemplate");
            template.BodyTemplate = request.BodyTemplate;
        }

        if (request.IsActive.HasValue)
        {
            template.IsActive = request.IsActive.Value;
        }

        await _unitOfWork.GetRepository<NotificationTemplate>().UpdateAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated template {Id}", templateId);

        return MapToDto(template);
    }

    public async Task<bool> DeleteTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _unitOfWork.GetRepository<NotificationTemplate>()
            .GetByIdAsync(templateId, cancellationToken);

        if (template == null)
        {
            return false;
        }

        await _unitOfWork.GetRepository<NotificationTemplate>().SoftDeleteAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted template {Id}", templateId);

        return true;
    }

    private void ValidateTemplate(string template, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return;
        }

        var scribanTemplate = Template.Parse(template);
        if (scribanTemplate.HasErrors)
        {
            var errors = string.Join(", ", scribanTemplate.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"{fieldName} contains invalid Scriban syntax: {errors}");
        }
    }

    private static TemplateDto MapToDto(NotificationTemplate template)
    {
        return new TemplateDto(
            template.Id,
            template.Name,
            template.Description,
            template.Type,
            template.SubjectTemplate,
            template.BodyTemplate,
            template.IsActive,
            template.CreatedAt
        );
    }
}
