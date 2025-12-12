using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Admin
{
    public partial class LateInterestAmount
    {
        [Inject] UserContext UserContext { get; set; }
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private List<BranchDto> branches = new();
        private List<CampChargeAmountDto> campcharges = new();
        private RadzenDataGrid<CampChargeAmountDto> grid;

        private bool IsLoading = true;
        public int companyId { get; set; }
        private int selectedBranchId;

        protected override async Task OnInitializedAsync()
        {
            companyId = UserContext.CompanyId;

            await LoadData();
        }

        private async Task LoadData()
        {
            branches = await Http.GetFromJsonAsync<List<BranchDto>>($"Admin/GetBranches/{companyId}");
            IsLoading = false;
        }

        private async Task OnBranchChanged(ChangeEventArgs e)
        {
            selectedBranchId = Convert.ToInt32(e.Value);

            if(selectedBranchId != 0)
            {
                try
                {
                    campcharges = await Http.GetFromJsonAsync<List<CampChargeAmountDto>>($"Admin/GetCampChargesAmounts/{companyId}/{selectedBranchId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to load Camp Charge Amount: {ex.Message}");
                }
            }
            else
            {
                
            }
        }

        public async Task AddNew()
        {
            var newLateIntrestAmount = new CampChargeAmountDto
            {
                BranchId = 0,
                InstallmentNo = 0,
                Amount = 0,
                IsNew = true
            };
            campcharges.Insert(0, newLateIntrestAmount);
            await grid.InsertRow(newLateIntrestAmount);
        }

        public async Task EditCampCharge(CampChargeAmountDto campCharge)
        {
            await grid.EditRow(campCharge);
            await Task.Yield();
        }
        public async Task CancelEdit(CampChargeAmountDto campCharge)
        {
            grid.CancelEditRow(campCharge);
            StateHasChanged();
        }

        public async Task SaveCampCharge(CampChargeAmountDto campCharge)
        {
            try
            {
                if (campCharge.BranchId == 0)
                {
                    Notifier.Warning("Validation Error", "Please select a branch.");
                    return;
                }

                if (campCharge.InstallmentNo <= 0)
                {
                    Notifier.Warning("Validation Error", "Please enter a valid installment number.");
                    return;
                }

                if (campCharge.Amount < 0)
                {
                    Notifier.Warning("Validation Error", "Please enter a valid amount.");
                    return;
                }

                var request = new
                {
                    BranchId = campCharge.BranchId,
                    InstallmentNo = campCharge.InstallmentNo,
                    Amount = campCharge.Amount
                };

                HttpResponseMessage response;

                if (campCharge.CCAmountId == 0)
                {
                    response = await Http.PostAsJsonAsync("Admin/CreateCampChargesAmount", request);
                }
                else
                {
                    response = await Http.PutAsJsonAsync($"Admin/UpdateCampChargeAmount/{campCharge.CCAmountId}", request);
                }

                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Late Intrest saved successfully.");
                    campcharges = await Http.GetFromJsonAsync<List<CampChargeAmountDto>>($"Admin/GetCampChargesAmounts/{companyId}/{selectedBranchId}");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred while saving: {ex.Message}");
            }

        }

        public async Task DeleteCampCharge(CampChargeAmountDto campCharge)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Late Intrest?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );
            if (confirmed == true)
            {
                try
                {
                    var response = await Http.DeleteAsync($"Admin/DeleteCampChargeAmount/{campCharge.CCAmountId}");
                    if (response.IsSuccessStatusCode)
                    {
                        campcharges.Remove(campCharge);
                        await grid.Reload();
                        Notifier.Success("Late Intrest deleted successfully.");
                    }
                    else
                    {
                        Notifier.Error("Failed to delete Late Intrest.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error deleting Late Intrest: {ex.Message}");
                    Notifier.Error("An error occurred while deleting.");
                }
            }
        }
    }
}
