namespace AtlasNOC.Domain.Enums;

public enum NotificationChannelType
{
    Email = 1,
    Webhook = 2,
    Slack = 3,
    Teams = 4,
    PagerDuty = 5,
    Opsgenie = 6,
    Sms = 7,
    Custom = 100
}