using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen.Blazor;
using System.Net.Http;
using System.Text.Json;

namespace ppfc.web.Pages.Transactions
{
    public partial class HpEntry
    {
        [Inject] UserContext UserContext { get; set; }
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private RadzenDataGrid<HpPopupDto> grid;
        public List<BranchesDto> branches = new();
        public List<AutoConsultantDto> consultants = new();
        public List<AreaDto> areas = new();
        public List<MandalDto> mandals = new();
        public List<VehicleDto> vehicles = new();
        public List<EmployeesDto> employees = new();

        public List<DropItem> districts = new()
        {
            new () { Id = 1, Name = "Adilabad" },
            new () { Id = 2, Name = "Ananthapuram" },
            new () { Id = 3, Name = "Chittor" },
            new () { Id = 4, Name = "East Godavari" },
            new () { Id = 5, Name = "Guntur" },
            new () { Id = 6, Name = "Hyderabad" },
            new () { Id = 7, Name = "Kadapa" },
            new () { Id = 8, Name = "Kareem Nagar" },
            new () { Id = 9, Name = "Karnool" },
            new () { Id = 10, Name = "Khammam" },
            new () { Id = 11, Name = "Krishna" },
            new () { Id = 12, Name = "Mahaboob Nagar" },
            new () { Id = 13, Name = "Medak" },
            new () { Id = 14, Name = "Nalgonda" },
            new () { Id = 15, Name = "Nelluru" },
            new () { Id = 16, Name = "Nijamabad" },
            new () { Id = 17, Name = "Prakasam" },
            new () { Id = 18, Name = "Rangareddi" },
            new () { Id = 19, Name = "Srikakulam" },
            new () { Id = 20, Name = "Vijayanagaram" },
            new () { Id = 21, Name = "Visakhapatnam" },
            new () { Id = 22, Name = "Warangal" },
            new () { Id = 23, Name = "West godavari" },
            new () { Id = 24, Name = "None" }
        };

        public List<DropItem> months = new()
        {
            new () { Id = 1, Name = "Jan" },
            new () { Id = 2, Name = "Feb" },
            new () { Id = 3, Name = "Mar" },
            new () { Id = 4, Name = "Apr" },
            new () { Id = 5, Name = "May" },
            new () { Id = 6, Name = "Jun" },
            new () { Id = 7, Name = "Jul" },
            new () { Id = 8, Name = "Aug" },
            new () { Id = 9, Name = "Sep" },
            new () { Id = 10, Name = "Oct" },
            new () { Id = 11, Name = "Nov" },
            new () { Id = 12, Name = "Dec" }
        };

        public List<int> years = Enumerable.Range(1900, DateTime.Now.Year - 1899).ToList();

        public List<HpPopupDto> hpRecords = new();
        public List<DropdownItemDto> HpEntryDropdown = new();
        public HpEntryDetailsDto HpDetails { get; set; } = new();
        HpPopupDto hp = new();

        public bool IsLoading = true;
        public bool IsSearchLoading = false;
        public long? IsClosingAccount = null;
        public bool IsLoadingbyArea = false;
        public bool IsLoadingbyId = false;
        public bool IsUpdating = false;
        public bool IsSaving = false;
        public bool IsDeleting = false;
        
        public int companyId { get; set; }
        public int userId { get; set; }
        public string SearchHpEntryId { get; set; }        
        public int? SelectedHpEntryId { get; set; }
        public DateTime FromDate = DateTime.Today.AddDays(-30), ToDate = DateTime.Today;
        public int? SelectedAreaId { get; set; }
        public int? SelectedDistrictId;
        private decimal? AvgInstallmentAmount { get; set; } = 0;                
        public string RecordStatus;
        public int? SelectedVehicleMakeId;
        public bool AllowSteps = true;
        public bool IsEditMode = false;
        public string AutoConsultantErrorMessage = "";
        public Dictionary<string, string> ValidationMessages { get; set; } = new Dictionary<string, string>();

        private int SelectedStepIndex = 0;
        public bool IsSelectionLocked { get; set; } = false;




        protected override async Task OnInitializedAsync()
        {
            companyId = UserContext.CompanyId;
            userId = UserContext.UserId;

            HpDetails = new HpEntryDetailsDto
            {
                InsuranceDate = DateTime.Today,
                AdjustDate = DateTime.Today
            };


            await LoadData();
        }

        private async Task LoadData()
        {
            IsLoading = true;
            try
            {
                areas = await Http.GetFromJsonAsync<List<AreaDto>>($"Transactions/GetAreas/{companyId}");
                branches = await Http.GetFromJsonAsync<List<BranchesDto>>($"Master/GetBranches/{companyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Branches: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #region Search & Filter

        public async Task HpEntrySearch()
        {
            IsSearchLoading = true;
            try
            {
                hpRecords = await Http.GetFromJsonAsync<List<HpPopupDto>>($"Transactions/GetHpEntryPopup?surname={hp.SurName}&adharNo={hp.AdharNumber}&vehicleNo={hp.VehicleNumber}&mobileNo={hp.MobileNumber}");
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred while fetching HP Entry records: {ex.Message}");
            }
            finally
            {
                IsSearchLoading = false;
            }
        }

        public async Task CloseHpAccount(HpPopupDto selectedHp)
        {
            IsClosingAccount = selectedHp.HpEntryId;

            var closeAccount = new CloseAccountDto
            {
                HpEntryId = selectedHp.HpEntryId,
                BranchId = selectedHp.BranchId,
                Description = selectedHp.Description,
                UserId = userId
            };
            try
            {
                var response = await Http.PostAsJsonAsync("Transactions/CloseHpAccount", closeAccount);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "HP Account closed successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to close HP Account!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred while closing HP Account: {ex.Message}");
            }

            IsClosingAccount = null;
        }

        #endregion

        #region HP Search

        public async Task SearchByHpEntryId()
        {
            IsLoadingbyId = true;
            if (string.IsNullOrWhiteSpace(SearchHpEntryId)) 
            {
                IsLoadingbyId = false;
                Notifier.Warning("Missing HP Entry ID", "Please enter HP Entry ID before searching");
                return;
            } 

            HpDetails = new HpEntryDetailsDto();
            ValidationMessages.Clear();
            StateHasChanged();

            HpEntryDropdown = await Http.GetFromJsonAsync<List<DropdownItemDto>>(
                $"Transactions/GetByHpEntryId?hpEntryId={SearchHpEntryId}");

            if (HpEntryDropdown.Count == 1)
            {
                SelectedHpEntryId = HpEntryDropdown[0].HPEntryId;

                IsSelectionLocked = true;

                await InvokeAsync(StateHasChanged);   // ✅ UI sync

                await LoadHpEntryDetails();
            }
            else
            {
                Notifier.Error("Hp Entry Not found");
            }
            IsLoadingbyId = false;
            StateHasChanged();
        }

        public async Task SearchByDateAndArea()
        {
            IsLoadingbyArea = true;

            HpDetails = new HpEntryDetailsDto();
            ValidationMessages.Clear();
            StateHasChanged();

            if (SelectedAreaId is null)
            {
                IsLoadingbyArea = false;
                Notifier.Warning("Missing Area", "Please select an Area before searching");                
                return;
            }

            HpEntryDropdown = await Http.GetFromJsonAsync<List<DropdownItemDto>>(
                $"Transactions/GetByDateAndArea?from={FromDate:yyyy-MM-dd}&to={ToDate:yyyy-MM-dd}&areaId={SelectedAreaId}");

            IsLoadingbyArea = false;
            StateHasChanged();
        }

        #endregion

        #region On Change Events

        public async Task OnBranchChanged(object value)
        {
            // update model
            HpDetails.BranchId = value == null ? 0 : Convert.ToInt32(value);

            await LoadConsultants();
           
            await LoadEmployees();

            await LoadVehicles();

            // clear previously selected consultant and Employee
            HpDetails.AutoConsultantId = null;
            HpDetails.EmployeeId = null;

            StateHasChanged();
        }

        public async Task OnAreaChanged(object value)
        {
            HpDetails.AreaId = value == null ? 0 : Convert.ToInt32(value);

            // Load mandals for this area
            await LoadMandals();
        }

        public async Task OnHpEntrySelected(object value)
        {
            if (value != null)
            {
                IsSelectionLocked = true;          // ✅ lock NOW (only after user picked)
                await LoadHpEntryDetails();         // your existing flow
                await InvokeAsync(StateHasChanged);
            }
        }

        public async Task OnRecordChanged(int value)
        {
            AutoConsultantErrorMessage = "";

            if (HpDetails.AutoConsultantId == null || HpDetails.AutoConsultantId == 0)
            {
                AllowSteps = false;
                return;
            }

            // OK selected -> show panels (allow all steps)
            if (value == 1)
            {
                AllowSteps = true;
                AutoConsultantErrorMessage = "";
                return;
            }

            // Pending -> do checkAutoConsultantStatus via API
            var result = await Http.GetFromJsonAsync<ConsultantStatusDto>(
                $"Transactions/CheckAutoConsultantStatus?autoConsultantId={HpDetails.AutoConsultantId}"
            );

            if (result.Limit > result.Pending)
            {
                AllowSteps = true;
                AutoConsultantErrorMessage = "";
            }
            else
            {
                AllowSteps = false;
                AutoConsultantErrorMessage =
                    $"Can't enter HP Entry, AutoConsultant limit is {result.Limit} and Accounts pending are {result.Pending}";
                //SelectedStepIndex = 1; // force user back to first step
            }
        }

        public async Task OnStepChange(int newStep)
        {
            // Step 0 is always accessible
            if (newStep == 0)
            {
                SelectedStepIndex = 0;
                return;
            }

            // If they try going to Step > 1 and not allowed → block it
            if (!AllowSteps && newStep > 1)
            {
                Notifier.Error("Cannot go to further steps");
                // stay where they are (don't force back)
                return;
            }

            SelectedStepIndex = newStep;
        }

        public async Task OnAutoConsultantChanged(object value)
        {
            HpDetails.AutoConsultantId = value == null ? 0 : Convert.ToInt32(value);

            HpDetails.AutoConsultantPending = 0;
            AutoConsultantErrorMessage = "";
            AllowSteps = false;

            await InvokeAsync(StateHasChanged);
        }

        #endregion

        #region Load Data

        public async Task LoadConsultants()
        {
            consultants = await Http.GetFromJsonAsync<List<AutoConsultantDto>>(
                $"Transactions/GetAutoConsultants?branchId={HpDetails.BranchId}");
        }

        public async Task LoadMandals()
        {
            if (HpDetails.AreaId is null || HpDetails.AreaId == 0)
            {
                mandals.Clear();
                HpDetails.MandalId = null;
                return;
            }

            mandals = await Http.GetFromJsonAsync<List<MandalDto>>(
                $"Transactions/GetMandals?areaId={HpDetails.AreaId}");

            // if no matching Mandal exists in the new list, reset it
            if (!mandals.Any(m => m.MandalId == HpDetails.MandalId))
                HpDetails.MandalId = null;

            StateHasChanged();
        }

        public async Task LoadVehicles()
        {
            vehicles = await Http.GetFromJsonAsync<List<VehicleDto>>(
                $"Transactions/GetVehicles?companyId={companyId}&branchId={HpDetails.BranchId}");
        }

        public async Task LoadEmployees()
        {
            if (HpDetails.BranchId == null || HpDetails.BranchId == 0)
            {
                employees.Clear();
                HpDetails.EmployeeId = null;
                return;
            }

            employees = await Http.GetFromJsonAsync<List<EmployeesDto>>(
                $"Transactions/GetEmployeesByBranch?branchId={HpDetails.BranchId}");

            // Auto-select existing employee when editing record
            if (!employees.Any(e => e.EmployeeId == HpDetails.EmployeeId))
            {
                HpDetails.EmployeeId = null;
            }

            await InvokeAsync(StateHasChanged);
        }

        public async Task LoadHpEntryDetails()
        {
            if (SelectedHpEntryId is null) return;

            HpDetails = await Http.GetFromJsonAsync<HpEntryDetailsDto>(
                $"Transactions/GetHpEntryDetails?hpEntryId={SelectedHpEntryId}");

            IsEditMode = true;

            ParseVehicleModel();

            await LoadConsultants();

            await LoadMandals();

            await LoadVehicles();

            CalculateInstallmentAmount();

            await LoadEmployees();

            StateHasChanged();

        }

        #endregion

        #region Binding Vehicle Model

        private void ParseVehicleModel()
        {
            if (!string.IsNullOrWhiteSpace(HpDetails.VehicleModel))
            {
                var parts = HpDetails.VehicleModel.Split(' ');
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0], out var month))
                        HpDetails.VehicleModelMonth = month;

                    if (int.TryParse(parts[1], out var year))
                        HpDetails.VehicleModelYear = year;
                }
            }
        }

        private void BuildVehicleModel()
        {
            if (HpDetails.VehicleModelMonth > 0 && HpDetails.VehicleModelYear > 0)
            {
                HpDetails.VehicleModel = $"{HpDetails.VehicleModelMonth} {HpDetails.VehicleModelYear}";
            }
            else
            {
                HpDetails.VehicleModel = "";
            }
        }

        #endregion

        #region Reset Buttons

        public async Task ResetSearch()
        {
            SearchHpEntryId = null;
            SelectedAreaId = null;
            HpEntryDropdown.Clear();
            SelectedHpEntryId = null;

            // also clear full UI details:
            HpDetails = new HpEntryDetailsDto();

            IsSelectionLocked = false;
            StateHasChanged();
        }

        private void ClearStep2()
        {
            HpDetails.SurName = "";
            HpDetails.Name = "";
            HpDetails.FatherName = "";
            HpDetails.Age = null;
            HpDetails.Address = "";
            HpDetails.MobileNumber = "";
            HpDetails.LandNumber = "";
            HpDetails.Town = "";
            HpDetails.WhatsappNumber = "";
            HpDetails.Occupation = "";
            HpDetails.FaceBookId = "";
            HpDetails.AdharNumber = "";
            HpDetails.DOB = null;
            HpDetails.DistrictId = 0;
            HpDetails.ResidenceProof = false;
            HpDetails.IncomeProof = false;

            // clear validation messages only of fields in this step
            ValidationMessages.Remove("SurName");
            ValidationMessages.Remove("Name");
            ValidationMessages.Remove("FatherName");
            ValidationMessages.Remove("Age");
            ValidationMessages.Remove("Address");
            ValidationMessages.Remove("MobileNumber");
            ValidationMessages.Remove("Town");
            ValidationMessages.Remove("Occupation");
            ValidationMessages.Remove("WhatsappNumber");

            StateHasChanged();
        }

        private void ClearStep3()
        {
            HpDetails.VehicleId = 0;
            HpDetails.VehicleModel = "";
            HpDetails.VehicleModelMonth = null;
            HpDetails.VehicleModelYear = null;
            HpDetails.VehicleNumber = "";
            HpDetails.InsuranceDate = null;
            HpDetails.EngineNumber = "";
            HpDetails.ChasisNumber = "";
            HpDetails.MarketValue = 0;
            HpDetails.CBook = false;
            HpDetails.SaleLetter = false;
            HpDetails.FinanceClearLetter = false;
            HpDetails.NoTR = false;

            ValidationMessages.Remove("VehicleId");
            ValidationMessages.Remove("VehicleNumber");
            ValidationMessages.Remove("VehicleModel");
            ValidationMessages.Remove("EngineNumber");
            ValidationMessages.Remove("ChasisNumber");
            ValidationMessages.Remove("MarketValue");

            StateHasChanged();
        }


        private void ClearStep4()
        {
            HpDetails.FinanceValue = 0;
            HpDetails.RTOCharge = 0;
            HpDetails.HPCharge = 0;
            HpDetails.DocCharges = 0;
            HpDetails.PartyDebitAmount = 0;
            HpDetails.FinanceName = "";
            HpDetails.BankName = "";
            HpDetails.AccountNo = "";
            HpDetails.FundingPercentage = 0;
            HpDetails.Installments = null;
            AvgInstallmentAmount = 0;
            HpDetails.AdjustDate = null;
            HpDetails.Description = "";
            HpDetails.EmployeeId = null;
            HpDetails.VerifiedBy = "";

            ValidationMessages.Remove("FinanceValue");
            ValidationMessages.Remove("Installments");
            ValidationMessages.Remove("FundingPercentage");
            ValidationMessages.Remove("EmployeeId");

            StateHasChanged();
        }



        #endregion

        #region Validations

        public string ValidationMsg(string id)
        {
            if (ValidationMessages != null)
            {
                if (ValidationMessages.ContainsKey(id))
                    return ValidationMessages[id];
                else
                    return "";
            }
            else
                return "";
        }

        private bool ValidateHpDetails()
        {
            ValidationMessages.Clear();
            bool valid = true;

            if (HpDetails.BranchId == 0)
            {
                if (!ValidationMessages.Keys.Contains("BranchName"))
                {
                    ValidationMessages.Add("BranchName", "Please select Branch");
                    valid = false;
                }
                else
                    valid = false;
            }

            if (HpDetails.AutoConsultantId == null)
            {
                if (!ValidationMessages.Keys.Contains("AutoConsultant"))
                {
                    ValidationMessages.Add("AutoConsultant", "Please select AutoConsultant");
                    valid = false;
                }
                else
                    valid = false;
            }

            if (HpDetails.AreaId == null)
            {
                if (!ValidationMessages.Keys.Contains("AreaId"))
                {
                    ValidationMessages.Add("AreaId", "Please select Area");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(HpDetails.MandalId == null)
            {
                if (!ValidationMessages.Keys.Contains("MandalId"))
                {
                    ValidationMessages.Add("MandalId", "Please select Mandal");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.SurName))
            {
                if (!ValidationMessages.Keys.Contains("SurName"))
                {
                    ValidationMessages.Add("SurName", "Please enter SurName");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.Name))
            {
                if (!ValidationMessages.Keys.Contains("Name"))
                {
                    ValidationMessages.Add("Name", "Please enter Name");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.FatherName))
            {
                if (!ValidationMessages.Keys.Contains("FatherName"))
                {
                    ValidationMessages.Add("FatherName", "Please enter FatherName");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(HpDetails.Age == null)
            {
                if (!ValidationMessages.Keys.Contains("Age"))
                {
                    ValidationMessages.Add("Age", "Please enter Age");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.MobileNumber))
            {
                if (!ValidationMessages.Keys.Contains("MobileNumber"))
                {
                    ValidationMessages.Add("MobileNumber", "Please enter MobileNumber");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.Town))
            {
                if (!ValidationMessages.Keys.Contains("Town"))
                {
                    ValidationMessages.Add("Town", "Please enter Town");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(HpDetails.DistrictId == 0)
            {
                if (!ValidationMessages.Keys.Contains("DistrictId"))
                {
                    ValidationMessages.Add("DistrictId", "Please select District");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.Occupation))
            {
                if (!ValidationMessages.Keys.Contains("Occupation"))
                {
                    ValidationMessages.Add("Occupation", "Please enter Occupation");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.Address))
            {
                if (!ValidationMessages.Keys.Contains("Address"))
                {
                    ValidationMessages.Add("Address", "Please enter Address");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.WhatsappNumber))
            {
                if (!ValidationMessages.Keys.Contains("WhatsappNumber"))
                {
                    ValidationMessages.Add("WhatsappNumber", "Please enter Whatsapp Number");
                    valid = false;
                }
                else
                    valid = false;
            }

            if (string.IsNullOrWhiteSpace(HpDetails.AdharNumber))
            {
                ValidationMessages["AdharNumber"] = "Please enter Aadhaar number";
                valid = false;
            }
            else if (HpDetails.AdharNumber.Length != 12)
            {
                ValidationMessages["AdharNumber"] = "Aadhaar number must be exactly 12 digits";
                valid = false;
            }
            else if (!HpDetails.AdharNumber.All(char.IsDigit))
            {
                ValidationMessages["AdharNumber"] = "Aadhaar number must contain only digits";
                valid = false;
            }


            if (HpDetails.VehicleId == 0)
            {
                if (!ValidationMessages.Keys.Contains("VehicleId"))
                {
                    ValidationMessages.Add("VehicleId", "Please select Vehicle Make");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.VehicleNumber))
            {
                if (!ValidationMessages.Keys.Contains("VehicleNumber"))
                {
                    ValidationMessages.Add("VehicleNumber", "Please enter VehicleNumber");
                    valid = false;
                }
                else
                    valid = false;
            }

            if (string.IsNullOrEmpty(HpDetails.EngineNumber))
            {
                if (!ValidationMessages.Keys.Contains("EngineNumber"))
                {
                    ValidationMessages.Add("EngineNumber", "Please enter EngineNumber");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.ChasisNumber))
            {
                if (!ValidationMessages.Keys.Contains("ChasisNumber"))
                {
                    ValidationMessages.Add("ChasisNumber", "Please enter ChasisNumber");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(HpDetails.MarketValue == 0)
            {
                if (!ValidationMessages.Keys.Contains("MarketValue"))
                {
                    ValidationMessages.Add("MarketValue", "Please enter MarketValue");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(HpDetails.FundingPercentage == 0)
            {
                if (!ValidationMessages.Keys.Contains("FundingPercentage"))
                {
                    ValidationMessages.Add("FundingPercentage", "Please enter FundingPercentage");
                    valid = false;
                }
                else
                    valid = false;
            }

            if (HpDetails.FinanceValue == 0)
            {
                if (!ValidationMessages.Keys.Contains("FinanceValue"))
                {
                    ValidationMessages.Add("FinanceValue", "Please enter FinanceValue");
                    valid = false;
                }
                else
                    valid = false;
            }

            if (HpDetails.Installments == null)
            {
                if (!ValidationMessages.Keys.Contains("Installments"))
                {
                    ValidationMessages.Add("Installments", "Please enter no. of Installments");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(HpDetails.EmployeeId == null)
            {
                if (!ValidationMessages.Keys.Contains("EmployeeId"))
                {
                    ValidationMessages.Add("EmployeeId", "Please select Agreement By");
                    valid = false;
                }
                else
                    valid = false;
            }

            if(string.IsNullOrEmpty(HpDetails.VerifiedBy))
            {
                if (!ValidationMessages.Keys.Contains("VerifiedBy"))
                {
                    ValidationMessages.Add("VerifiedBy", "Please enter Verified Name");
                    valid = false;
                }
                else
                    valid = false;
            }

            return valid;
        }

        #endregion

        public async Task CheckAadhar(string aadhar)
        {
            if (string.IsNullOrWhiteSpace(aadhar))
                return;

            try
            {
                var response = await Http.GetFromJsonAsync<AadharCheckResult>($"Transactions/CheckAadhar?aadharNumber={aadhar}");

                if (response.IsBlocked)
                {
                    ValidationMessages["AdharNumber"] = response.BlockedInfo;
                    StateHasChanged();
                }
                else
                {
                    // Clear previous validation if not blocked
                    if (ValidationMessages.ContainsKey("AdharNumber"))
                        ValidationMessages.Remove("AdharNumber");
                }
            }
            catch (Exception ex)
            {
                ValidationMessages["AdharNumber"] = "Server error while checking Aadhaar";
            }
        }

        public async Task CheckVehicle()
        {

        }


        private void CalculateInstallmentAmount()
        {
            var finance = HpDetails.FinanceValue;
            var rto = HpDetails.RTOCharge;
            var doc = HpDetails.DocCharges;
            var hp = HpDetails.HPCharge;
            var installments = HpDetails.Installments ?? 0;

            if (installments > 0)
            {
                var total = finance + rto + doc + hp;
                AvgInstallmentAmount = Math.Round(total / installments, 2);
            }
            else
            {
                AvgInstallmentAmount = 0;
            }
        }

        public async Task SubmitEntry()
        {
            if (!ValidateHpDetails()) return;
            BuildVehicleModel();

            IsSaving = true;

            try
            {
                HpDetails.CompanyId = companyId;
                HpDetails.UserId = userId;

                var response = await Http.PostAsJsonAsync("Transactions/CreateHpEntry", HpDetails);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "HP Entry created successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to create HP Entry!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred while creating HP Entry: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        public async Task UpdateEntry()
        {
            if (!ValidateHpDetails()) return;

            BuildVehicleModel();

            IsUpdating = true;

            try
            {
                var response = await Http.PutAsJsonAsync("Transactions/UpdateHpEntry", HpDetails);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "HP Entry updated successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to update HP Entry!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred while updating HP Entry: {ex.Message}");
            }
            finally
            {
                IsUpdating = false;
            }
        }

        public async Task DeleteEntry()
        {
            if(HpDetails.HPEntryId == 0)
            {
                Notifier.Error("Select any Hp Entry for Deleting");
                return;
            }

            IsDeleting = true;

            try
            {
                var response = await Http.DeleteAsync($"Transactions/DeleteHpEntry/{HpDetails.HPEntryId}");
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "HP Entry deleted successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete HP Entry!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred while deleting HP Entry: {ex.Message}");
            }
            finally
            {
                IsDeleting = false;
            }
        }
    }
}
