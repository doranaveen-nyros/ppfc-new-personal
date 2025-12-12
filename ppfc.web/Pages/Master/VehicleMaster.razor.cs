using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Diagnostics;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class VehicleMaster
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<VehicleDto> grid;
        public List<VehicleDto> vehicles = new();

        public bool IsLoading = true;
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
                vehicles = await Http.GetFromJsonAsync<List<VehicleDto>>($"Master/GetVehicles/{companyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Vehicles: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditVehicle(VehicleDto vehicle)
        {
            await grid.EditRow(vehicle);
            await Task.Yield();
        }

        public async Task CancelEdit(VehicleDto vehicle)
        {
            grid.CancelEditRow(vehicle);
            StateHasChanged();
        }

        public async Task AddNew()
        {
            var newVehicle = new VehicleDto { IsNew = true, CompanyId = companyId };
            vehicles.Insert(0, newVehicle);
            await grid.InsertRow(newVehicle);
        }

        public async Task SaveVehicle(VehicleDto vehicle) 
        {             
            if(string.IsNullOrWhiteSpace(vehicle.VehicleName))
            {
                Notifier.Warning("Vehicle Name is required.");
                return;
            }
            if(string.IsNullOrWhiteSpace(vehicle.VCode))
            {
                Notifier.Warning("Vehicle Code is required.");
                return;
            }

            try
            {
                if (vehicle.VehicleId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddVehicle", vehicle);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadData();
                        Notifier.Success("Vehicle created successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to Create!", message);
                    }
                }
                else
                {
                    var response = await Http.PutAsJsonAsync("Master/UpdateVehicle", vehicle);
                    if (response.IsSuccessStatusCode)
                    {
                        await grid.UpdateRow(vehicle);
                        Notifier.Success("Vehicle updated successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to Update!", message);
                    }
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }
        }

        public async Task DeleteVehicle(VehicleDto vehicle)
        {
            bool? confirmed = await DialogService.Confirm(
                            $"Are you sure you want to delete this Vehicle?",
                            "Confirm Delete",
                            new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
                        );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteVehicle/{vehicle.VehicleId}");
                if (response.IsSuccessStatusCode)
                {
                    vehicles.Remove(vehicle);
                    await grid.Reload();
                    Notifier.Success("Vehicle deleted successfully.");
                }
                else
                {
                    Notifier.Error("Failed to delete Vehicle.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }
        }
    }
}
