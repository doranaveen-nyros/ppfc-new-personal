using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Admin
{
    public partial class SMS
    {
        [Inject] UserContext UserContext { get; set; }
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private CompanyDto? company;
        private List<BranchDto> branches = new();
        private List<MessageReceiverDto> receivers = new();
        private List<SmsDto> smsList = new();

        private int selectedBranchId;
        private string selectedType = "-Select-";
        private IEnumerable<string> selectedReceivers = new List<string>();
        private string messageText;
        private int companyId { get; set; }
        private string currentUserName {  get; set; }
        private int messageLength = 0;
        private bool IsLoading = true;
        private bool IsSending = false;
        private bool IsLoadedSMS = false;
        private bool IsReSendingSMS = false;
        private int? IsReSendSingleSMS = null;
        private bool IsDeletingSMS = false;
        private bool IsReceiversLoading = false;

        protected override async Task OnInitializedAsync()
        {
            companyId = UserContext.CompanyId;
            currentUserName = UserContext.UserName;

            await LoadData();
        }

        private async Task LoadData()
        {
            // Load company and branches on page load
            var companies = await Http.GetFromJsonAsync<List<CompanyDto>>($"Admin/GetCompanies/{companyId}");
            company = companies?.FirstOrDefault();
            branches = await Http.GetFromJsonAsync<List<BranchDto>>($"Admin/GetBranches/{companyId}");

            IsLoading = false;
        }

        private async Task OnBranchChanged(ChangeEventArgs e)
        {
            selectedBranchId = Convert.ToInt32(e.Value);
            receivers.Clear();
            selectedReceivers = new List<string>();
            selectedType = "-Select-";
        }

        private async Task OnTypeChanged(ChangeEventArgs e)
        {
            selectedType = e.Value?.ToString() ?? "-Select-";
            receivers.Clear();
            selectedReceivers = new List<string>();
            IsReceiversLoading = true;

            if (selectedType != "-Select-" && companyId > 0)
            {
                try
                {
                    receivers = await Http.GetFromJsonAsync<List<MessageReceiverDto>>(
                        $"Admin/GetMessageReceivers?type={selectedType}&companyId={companyId}&branchId={selectedBranchId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error loading receivers: " + ex.Message);
                }
            }

            IsReceiversLoading = false;
        }

        private void OnMessageInput(ChangeEventArgs e)
        {
            messageText = e.Value?.ToString() ?? string.Empty;
            messageLength = messageText.Length;
        }

        private void ResetForm()
        {
            selectedBranchId = 0;
            selectedType = "-Select-";
            selectedReceivers = new List<string>();
            messageText = string.Empty;
            messageLength = 0;
            receivers.Clear();
            IsReceiversLoading = false;
            StateHasChanged();
        }

        private async Task OnSendClicked()
        {
            

            if (string.IsNullOrWhiteSpace(messageText))
            {
                Notifier.Warning("Alert", "Please enter a message.");
                return;
            }

            if (selectedReceivers == null || !selectedReceivers.Any())
            {
                Notifier.Warning("Alert", "Please select at least one recipient.");
                return;
            }

            IsSending = true;
            StateHasChanged();

            // Build recipient list — no need to handle "All" anymore
            var recipientsToSend = receivers
                .Where(r => selectedReceivers.Contains(r.PhoneNumber))
                .Select(r => new SmsRecipientDto
                {
                    ReceiverName = r.ReceiverName,
                    PhoneNumber = r.PhoneNumber
                }).ToList();

            var request = new SmsRequestDto
            {
                MessageType = "Promo",
                Message = messageText,
                UserName = currentUserName,
                CompanyId = companyId,
                BranchId = selectedBranchId,
                Recipients = recipientsToSend
            };

            var response = await Http.PostAsJsonAsync("Admin/QueueSms", request);

            if (response.IsSuccessStatusCode)
            {
                Notifier.Success("Success", "Message sent successfully!");
                messageText = string.Empty;
                selectedReceivers = new List<string>();
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                Notifier.Error("Failed", $"Failed to send message. {err}");
            }

            IsSending = false;
            StateHasChanged();
        }

        private async Task LoadSMSData()
        {
            IsLoadedSMS = true;
            try
            {
                smsList = await Http.GetFromJsonAsync<List<SmsDto>>(
                    $"Admin/GetUndeliveredSMS?companyId={companyId}");

                if (smsList == null || smsList.Count == 0)
                {
                    Notifier.Error("alert", "No undelivered SMS found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading SMS data: " + ex.Message);
                Notifier.Error("alert", "Failed to load SMS data.");
            }

            IsLoadedSMS = false;
        }

        private async Task ResendSingle(int smsId)
        {
            IsReSendSingleSMS = smsId;
            try
            {
                var response = await Http.PostAsJsonAsync($"Admin/ResendSmsById/{smsId}", new { });

                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "Message resent successfully.");
                    await LoadSMSData();
                }
                else
                {
                    Notifier.Error("Error", "Failed to resend message.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"Failed to resend: {ex.Message}");
            }
            IsReSendSingleSMS = null;
        }

        private bool IsAllSelected => smsList.All(x => x.IsSelected);

        private void ToggleSelectAll(ChangeEventArgs e)
        {
            bool selectAll = (bool)e.Value;
            foreach (var sms in smsList)
                sms.IsSelected = selectAll;
        }

        private async Task DeleteSelected()
        {
            var selected = smsList.Where(s => s.IsSelected).ToList();
            if (!selected.Any())
            {
                Notifier.Warning("Alert", "Please select at least one record to delete.");
                return;
            }        

            bool? confirm = await DialogService.Confirm(
                $"Are you sure you want to delete {selected.Count} SMS?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirm != true)
                return;

            IsDeletingSMS = true;
            StateHasChanged();

            try
            {
                var ids = selected.Select(x => x.SMSId).ToList();
                var response = await Http.PostAsJsonAsync("Admin/DeleteMultipleSms", ids);

                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "Selected SMS deleted successfully.");
                    await LoadSMSData();
                }
                else
                {
                    Notifier.Error("Error", "Failed to delete SMS.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"Failed to delete: {ex.Message}");
            }

            IsDeletingSMS = false;
            StateHasChanged();
        }

        private async Task ResendSelected()
        {
            
            var selected = smsList.Where(s => s.IsSelected).ToList();
            if (!selected.Any())
            {
                Notifier.Warning("Alert", "Please select at least one record to resend.");
                return;
            }

            bool? confirm = await DialogService.Confirm(
                $"Are you sure you want to resend {selected.Count} SMS?",
                "Confirm Resend",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirm != true)
                return;

            IsReSendingSMS = true;
            StateHasChanged();

            try
            {
                var ids = selected.Select(x => x.SMSId).ToList();
                var response = await Http.PostAsJsonAsync("Admin/ResendMultipleSms", ids);

                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "Selected messages resent successfully.");
                    await LoadSMSData();
                }
                else
                {
                    Notifier.Error("Error", "Failed to resend messages.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"Failed to resend: {ex.Message}");
            }

            IsReSendingSMS = false;
            StateHasChanged();
        }


    }
}
