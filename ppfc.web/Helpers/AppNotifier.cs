using Radzen;

namespace ppfc.web.Helpers
{
    public class AppNotifier
    {
        private readonly NotificationService _notificationService;

        public AppNotifier(NotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        private void Show(NotificationSeverity severity, string summary, string detail)
        {
            _notificationService.Notify(new NotificationMessage
            {
                Severity = severity,
                Summary = summary,
                Detail = detail,
                Duration = 3000,
                ShowProgress = true,
                Style = "position: fixed; top: 20px; right: 20px;",
                CloseOnClick = true
            });
        }

        public void Success(string summary, string detail = "")
            => Show(NotificationSeverity.Success, summary, detail);

        public void Error(string summary, string detail = "")
            => Show(NotificationSeverity.Error, summary, detail);

        public void Warning(string summary, string detail = "")
            => Show(NotificationSeverity.Warning, summary, detail);

        public void Info(string summary, string detail = "")
            => Show(NotificationSeverity.Info, summary, detail);
    }
}

