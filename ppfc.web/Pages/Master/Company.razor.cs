using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen.Blazor;

namespace ppfc.web.Pages.Master
{
    public partial class Company
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<CompaniesDto> grid;
        public List<CompaniesDto> companies = new();

        private bool IsLoading = true;
        private bool IsUpdating = false;

        public int companyId { get; set; }

        protected override async Task OnInitializedAsync()
        {
            companyId = UserContext.CompanyId;

            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                companies = await Http.GetFromJsonAsync<List<CompaniesDto>>($"Master/GetCompany/{companyId}");
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", "Failed to load Company data.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditCompany(CompaniesDto company)
        {
            await grid.EditRow(company);
            await Task.Yield();
        }

        public async Task CancelEdit(CompaniesDto company)
        {
            grid.CancelEditRow(company);
            StateHasChanged();
        }

        public async Task UpdateCompany(CompaniesDto company)
        {
            IsUpdating = true;
            try
            {
                var response = await Http.PutAsJsonAsync($"Master/UpdateCompany/{companyId}", company);
                if (response.IsSuccessStatusCode)
                {
                    await grid.UpdateRow(company);
                    Notifier.Success("Success", "Company updated successfully.");
                }
                else
                {
                    Notifier.Error("Error", "Failed to update Company.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to update Company: {ex.Message}");
                Notifier.Error("Error", "Failed to update Company.");
            }
            finally
            {
                IsUpdating = false;
            }
        }
    }
}
