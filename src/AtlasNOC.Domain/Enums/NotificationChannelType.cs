namespace AtlasNOC.Domain.Enums;

public enum NotificationChannelType
{
    Email = 1,
    Webhook = 2,
    Slack = 3,
    Teams = 4,
    PagerDuty = 5,
    Sms = 6,
    Custom = 100
}