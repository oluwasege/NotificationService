namespace NotificationService.Api.Extensions;

public static class HttpContextExtensions
{
    public static Guid GetUserId(this HttpContext context)
    {
        if (context.Items.TryGetValue("UserId", out var userId) && userId is Guid id)
        {
            return id;
        }
        throw new UnauthorizedAccessException("User ID not found in context");
    }

    public static Guid GetSubscriptionId(this HttpContext context)
    {
        if (context.Items.TryGetValue("SubscriptionId", out var subscriptionId) && subscriptionId is Guid id)
        {
            return id;
        }
        throw new UnauthorizedAccessException("Subscription ID not found in context");
    }

    public static bool CanSendSms(this HttpContext context)
    {
        return context.Items.TryGetValue("AllowSms", out var allowSms) && allowSms is true;
    }

    public static bool CanSendEmail(this HttpContext context)
    {
        return context.Items.TryGetValue("AllowEmail", out var allowEmail) && allowEmail is true;
    }

    public static int GetRemainingDailyQuota(this HttpContext context)
    {
        return context.Items.TryGetValue("RemainingDailyQuota", out var quota) && quota is int q ? q : 0;
    }

    public static int GetRemainingMonthlyQuota(this HttpContext context)
    {
        return context.Items.TryGetValue("RemainingMonthlyQuota", out var quota) && quota is int q ? q : 0;
    }
}
