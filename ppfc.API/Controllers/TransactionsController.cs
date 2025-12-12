using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ppfc.API.Services;
using ppfc.DTO;
using System.Data;
using System.Data.SqlClient;

namespace ppfc.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HpEntryService _hpEntryService;
        private readonly OpeningBalanceService _openingBalanceService;
        private readonly SmsService _smsService;

        public TransactionsController(IConfiguration configuration, HpEntryService hpEntryService, OpeningBalanceService openingBalanceService, SmsService smsService)
        {
            _configuration = configuration;
            _hpEntryService = hpEntryService;
            _openingBalanceService = openingBalanceService;
            _smsService = smsService;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        #region HpEntry Search

        [Authorize]
        [HttpGet("GetHpEntryPopup")]
        public async Task<IActionResult> GetHpEntryPopup(
                                                        [FromQuery] string? surname = "",
                                                        [FromQuery] string? adharNo = "",
                                                        [FromQuery] string? mobileNo = "",
                                                        [FromQuery] string? vehicleNo = "")
        {
            try
            {
                using (var conn = GetConnection())
                {
                    var filters = new List<string>();
                    var cmd = new SqlCommand();
                    cmd.Connection = conn;

                    var query = @"
                SELECT hpentryId, HPEntry_Id, adjustdate, branchId, branchname,
                       SurName, name, Fathername, MobileNumber, AdharNumber,
                       VehicleName, vehiclenumber, Town, Address,
                       COALESCE(AccloseId, 0) AS AccloseId,
                       (FinancedValue - PaidAmount) AS Totaldue
                FROM vw_HpDues
                WHERE 1 = 1
            ";

                    if (!string.IsNullOrWhiteSpace(surname))
                    {
                        filters.Add("SurName LIKE @Surname");
                        cmd.Parameters.AddWithValue("@Surname", "%" + surname + "%");
                    }

                    if (!string.IsNullOrWhiteSpace(mobileNo))
                    {
                        filters.Add("MobileNumber LIKE @MobileNo");
                        cmd.Parameters.AddWithValue("@MobileNo", "%" + mobileNo + "%");
                    }

                    if (!string.IsNullOrWhiteSpace(adharNo))
                    {
                        filters.Add("AdharNumber LIKE @AdharNo");
                        cmd.Parameters.AddWithValue("@AdharNo", "%" + adharNo + "%");
                    }

                    if (!string.IsNullOrWhiteSpace(vehicleNo))
                    {
                        filters.Add("VehicleNumber = @VehicleNo");
                        cmd.Parameters.AddWithValue("@VehicleNo", vehicleNo);
                    }

                    // ✅ Apply OR search only if filters exist
                    if (filters.Count > 0)
                        query += " AND (" + string.Join(" OR ", filters) + ")";

                    cmd.CommandText = query;

                    await conn.OpenAsync();
                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    var results = new List<HpPopupDto>();

                    while (await reader.ReadAsync())
                    {
                        results.Add(new HpPopupDto
                        {
                            HpEntryId = Convert.ToInt64(reader["hpentryId"]),
                            HPEntry_Id = Convert.ToInt64(reader["HPEntry_Id"]),
                            AdjustDate = reader.GetDateTime(reader.GetOrdinal("adjustdate")),
                            BranchId = reader.GetInt32(reader.GetOrdinal("branchId")),
                            BranchName = reader["branchname"].ToString() ?? "",
                            SurName = reader["SurName"].ToString() ?? "",
                            Name = reader["name"].ToString() ?? "",
                            FatherName = reader["Fathername"].ToString() ?? "",
                            MobileNumber = reader["MobileNumber"].ToString() ?? "",
                            AdharNumber = reader["AdharNumber"].ToString() ?? "",
                            VehicleName = reader["VehicleName"].ToString() ?? "",
                            VehicleNumber = reader["vehiclenumber"].ToString() ?? "",
                            Town = reader["Town"].ToString() ?? "",
                            Address = reader["Address"].ToString() ?? "",
                            ACCloseId = reader.GetInt32(reader.GetOrdinal("AccloseId")),
                            TotalDue = reader.IsDBNull(reader.GetOrdinal("Totaldue"))
                                ? 0
                                : reader.GetDecimal(reader.GetOrdinal("Totaldue"))
                        });
                    }

                    return Ok(results);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to load HP Entry popup data", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("CloseHpAccount")]
        public async Task<IActionResult> CloseHpAccount([FromBody] CloseAccountDto model)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    await conn.OpenAsync();

                    // ✅ 1. Check if already closed
                    var checkCmd = new SqlCommand(
                        "SELECT ReportAccountCloseId FROM ReportAccountClose WHERE HPEntryId = @HPEntryId",
                        conn
                    );
                    checkCmd.Parameters.AddWithValue("@HPEntryId", model.HpEntryId);

                    object existing = await checkCmd.ExecuteScalarAsync();

                    // ✅ 2. If exists → delete old record
                    if (existing != null)
                    {
                        var deleteCmd = new SqlCommand(
                            "DELETE FROM ReportAccountClose WHERE ReportAccountCloseId = @Id",
                            conn
                        );
                        deleteCmd.Parameters.AddWithValue("@Id", Convert.ToInt32(existing));
                        await deleteCmd.ExecuteNonQueryAsync();
                    }

                    // ✅ 3. Insert new close record
                    var insertCmd = new SqlCommand("sp_ReportAccountClose_Insert", conn);
                    insertCmd.CommandType = CommandType.StoredProcedure;

                    insertCmd.Parameters.AddWithValue("@BranchId", model.BranchId);
                    insertCmd.Parameters.AddWithValue("@HPEntryId", model.HpEntryId);
                    insertCmd.Parameters.AddWithValue("@Description", model.Description ?? "");
                    insertCmd.Parameters.AddWithValue("@Date", DateTime.Now);
                    insertCmd.Parameters.AddWithValue("@UsersId", model.UserId);

                    await insertCmd.ExecuteNonQueryAsync();

                    return Ok(new { Message = "Account closed successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to close account", Error = ex.Message });
            }
        }

        #endregion

        #region Hp Entry

        [Authorize]
        [HttpGet("GetByHpEntryId")]
        public IActionResult GetByHpEntryId(int hpEntryId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT HPEntryId,
                   (CONVERT(varchar(10),HPEntry_Id)+' - '+
                    CONVERT(varchar(30),VehicleNumber)+' - '+
                    VehicleName+' - '+Name) AS VehicleNo
            FROM vw_HPEntry
            WHERE HpEntryClose IS NULL
              AND HPEntry_Id = @HpEntryId
            ORDER BY HPEntry_Id";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@HpEntryId", hpEntryId);

                var list = new List<HpEntryDropdownDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new HpEntryDropdownDto
                        {
                            HpEntryId = Convert.ToInt32(dr["HPEntryId"]),
                            VehicleNo = dr["VehicleNo"].ToString()!
                        });
                    }
                }

                return Ok(list);
            }
        }

        [Authorize]
        [HttpGet("GetByDateAndArea")]
        public IActionResult GetByDateAndArea(DateTime from, DateTime to, int areaId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT HPEntryId,
                   (CONVERT(varchar(10),HPEntry_Id)+' - '+
                    CONVERT(varchar(30),VehicleNumber)+' - '+
                    VehicleName+' - '+Name) AS VehicleNo
            FROM vw_HPEntry
            WHERE HpEntryClose IS NULL
              AND Date BETWEEN @From AND @To
              AND AreaId = @AreaId
            ORDER BY HPEntry_Id";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@From", from);
                cmd.Parameters.AddWithValue("@To", to);
                cmd.Parameters.AddWithValue("@AreaId", areaId);

                var list = new List<HpEntryDropdownDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new HpEntryDropdownDto
                        {
                            HpEntryId = Convert.ToInt32(dr["HPEntryId"]),
                            VehicleNo = dr["VehicleNo"].ToString()!
                        });
                    }
                }

                return Ok(list);
            }
        }

        [Authorize]
        [HttpGet("GetHpEntryDetails")]
        public IActionResult GetHpEntryDetails(int hpEntryId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var cmd = new SqlCommand("sp_HPEntry_Select", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@HPEntryId", hpEntryId);

                using (var dr = cmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return NotFound("HP Entry not found.");

                    var dto = new HpEntryDetailsDto
                    {
                        HPEntryId = Convert.ToInt32(dr["HPEntryId"]),
                        HPEntry_Id = Convert.ToInt32(dr["HPEntry_Id"]),
                        CompanyId = Convert.ToInt32(dr["CompanyId"]),
                        UserId = Convert.ToInt32(dr["UsersId"]),

                        // Text fields (safe)
                        SurName = dr["SurName"]?.ToString() ?? "",
                        Name = dr["Name"]?.ToString() ?? "",
                        FatherName = dr["FatherName"]?.ToString() ?? "",
                        Town = dr["Town"]?.ToString() ?? "",
                        Occupation = dr["Occupation"]?.ToString() ?? "",
                        MobileNumber = dr["MobileNumber"]?.ToString() ?? "",
                        LandNumber = dr["LandNumber"]?.ToString() ?? "",
                        Address = dr["Address"]?.ToString() ?? "",
                        AdharNumber = dr["AdharNumber"]?.ToString() ?? "",
                        WhatsappNumber = dr["WhatsappNumber"]?.ToString() ?? "",
                        FaceBookId = dr["FaceBookId"]?.ToString() ?? "",
                        Description = dr["Description"]?.ToString() ?? "",

                        // Bool fields (must check null)
                        ResidenceProof = dr["ResidenceProof"] != DBNull.Value && Convert.ToBoolean(dr["ResidenceProof"]),
                        IncomeProof = dr["IncomeProof"] != DBNull.Value && Convert.ToBoolean(dr["IncomeProof"]),
                        CBook = dr["CBook"] != DBNull.Value && Convert.ToBoolean(dr["CBook"]),
                        SaleLetter = dr["SaleLetter"] != DBNull.Value && Convert.ToBoolean(dr["SaleLetter"]),
                        FinanceClearLetter = dr["FinanceClearLetter"] != DBNull.Value && Convert.ToBoolean(dr["FinanceClearLetter"]),
                        NoTR = dr["NoTR"] != DBNull.Value && Convert.ToBoolean(dr["NoTR"]),

                        // Nullable ints
                        Age = dr["Age"] == DBNull.Value ? null : Convert.ToInt32(dr["Age"]),
                        BranchId = dr["BranchId"] == DBNull.Value ? 0 : Convert.ToInt32(dr["BranchId"]),
                        AreaId = dr["AreaId"] == DBNull.Value ? 0 : Convert.ToInt32(dr["AreaId"]),
                        MandalId = dr["MandalId"] == DBNull.Value ? 0 : Convert.ToInt32(dr["MandalId"]),
                        DistrictId = dr["District"] == DBNull.Value ? 0 : Convert.ToInt32(dr["District"]),
                        VehicleId = dr["VehicleId"] == DBNull.Value ? 0 : Convert.ToInt32(dr["VehicleId"]),
                        Installments = dr["Installments"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Installments"]),
                        EmployeeId = dr["EmployeeId"] == DBNull.Value ? 0 : Convert.ToInt32(dr["EmployeeId"]),
                        AutoConsultantId = dr["AutoConsultantId"] == DBNull.Value ? 0 : Convert.ToInt32(dr["AutoConsultantId"]),
                        AutoConsultantPending = dr["AutoConsultantPending"] == DBNull.Value ? 0 : Convert.ToInt32(dr["AutoConsultantPending"]),

                        // Decimal values
                        MarketValue = dr["MarketValue"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["MarketValue"]),
                        FinanceValue = dr["FinanceValue"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["FinanceValue"]),
                        RTOCharge = dr["RTOCharge"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["RTOCharge"]),
                        HPCharge = dr["HPCharge"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["HPCharge"]),
                        DocCharges = dr["DOCCharges"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["DOCCharges"]),
                        PartyDebitAmount = dr["PartyDebitAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["PartyDebitAmount"]),

                        // Dates
                        DOB = dr["DOB"] == DBNull.Value ? null : Convert.ToDateTime(dr["DOB"]),
                        InsuranceDate = dr["InsuranceDate"] == DBNull.Value ? null : Convert.ToDateTime(dr["InsuranceDate"]),
                        AdjustDate = dr["AdjustDate"] == DBNull.Value ? null : Convert.ToDateTime(dr["AdjustDate"]),

                        // Other text values
                        VehicleNumber = dr["VehicleNumber"]?.ToString() ?? "",
                        VehicleModel = dr["VehicleModel"]?.ToString() ?? "",
                        EngineNumber = dr["EngineNumber"]?.ToString() ?? "",
                        ChasisNumber = dr["ChasisNumber"]?.ToString() ?? "",
                        FinanceName = dr["FinanceName"]?.ToString() ?? "",
                        BankName = dr["BankName"]?.ToString() ?? "",
                        AccountNo = dr["AccountNo"]?.ToString() ?? "",
                        FundingPercentage = dr["FundingPercentage"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["FundingPercentage"]),
                        VerifiedBy = dr["VerifiedBy"]?.ToString() ?? ""
                    };

                    return Ok(dto);
                }
            }
        }

        [Authorize]
        [HttpGet("GetAutoConsultants")]
        public IActionResult GetAutoConsultants(int branchId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT AutoConsultantId, AutoConsultantName
            FROM AutoConsultant
            WHERE BranchId = @BranchId and lock=0
            ";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@BranchId", branchId);

                var list = new List<AutoConsultantDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new AutoConsultantDto
                        {
                            AutoConsultantId = Convert.ToInt32(dr["AutoConsultantId"]),
                            AutoConsultantName = dr["AutoConsultantName"].ToString()!
                        });
                    }
                }

                return Ok(list);
            }
        }

        [Authorize]
        [HttpGet("GetAreas/{companyId}")]
        public IActionResult GetAreas(int companyId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT AreaId,
                   AreaName + ' - ' + BranchName AS AreaName,
                   BranchId
            FROM vw_Area
            WHERE CompanyId = @CompanyId
              AND Lock = 'False'
            ORDER BY BranchName";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@CompanyId", companyId);

                var list = new List<AreaDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new AreaDto
                        {
                            AreaId = Convert.ToInt32(dr["AreaId"]),
                            AreaName = dr["AreaName"].ToString()!,
                            BranchId = Convert.ToInt32(dr["BranchId"])
                        });
                    }
                }

                return Ok(list);
            }
        }

        [Authorize]
        [HttpGet("GetMandals")]
        public IActionResult GetMandals(int areaId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT MandalId, MandalName 
            FROM Mandal 
            WHERE AreaId = @AreaId
            ORDER BY MandalName";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@AreaId", areaId);

                var list = new List<MandalDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new MandalDto
                        {
                            MandalId = Convert.ToInt32(dr["MandalId"]),
                            MandalName = dr["MandalName"].ToString()!
                        });
                    }
                }

                return Ok(list);
            }
        }

        [Authorize]
        [HttpGet("GetVehicles")]
        public IActionResult GetVehicles(int companyId, int branchId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Apply the same conditional logic as WebForms
                int effectiveCompanyId;
                if (companyId == 1 || companyId == 2)
                    effectiveCompanyId = 1;
                else if (companyId == 3)
                    effectiveCompanyId = 3;
                else
                    effectiveCompanyId = 4;

                var query = @"
            SELECT VehicleId, VehicleName
            FROM Vehicle
            WHERE CompanyId = @CompanyId
              AND VehicleId NOT IN (
                SELECT VehicleId 
                FROM OverFunding 
                WHERE BranchId = @BranchId 
                AND VehicleStatus = 0
              )
            ORDER BY VehicleName";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@CompanyId", effectiveCompanyId);
                cmd.Parameters.AddWithValue("@BranchId", branchId);

                var list = new List<VehicleDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new VehicleDto
                        {
                            VehicleId = Convert.ToInt32(dr["VehicleId"]),
                            VehicleName = dr["VehicleName"].ToString()!
                        });
                    }
                }

                return Ok(list);
            }
        }

        [Authorize]
        [HttpGet("GetEmployeesByBranch")]
        public IActionResult GetEmployeesByBranch(int branchId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT EmployeeId, EmployeeName 
            FROM Employee
            WHERE BranchId = @BranchId
            ORDER BY EmployeeName";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@BranchId", branchId);

                var list = new List<EmployeesDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new EmployeesDto
                        {
                            EmployeeId = Convert.ToInt32(dr["EmployeeId"]),
                            EmployeeName = dr["EmployeeName"].ToString()!
                        });
                    }
                }

                return Ok(list);
            }
        }

        [HttpGet("CheckAutoConsultantStatus")]
        public async Task<IActionResult> CheckAutoConsultantStatus(int autoConsultantId)
        {
            using (var conn = GetConnection())
            {
                await conn.OpenAsync();

                // Get limit
                var limitCmd = new SqlCommand("SELECT limit FROM AutoConsultant WHERE AutoConsultantId=@id", conn);
                limitCmd.Parameters.AddWithValue("@id", autoConsultantId);
                int limit = Convert.ToInt32(await limitCmd.ExecuteScalarAsync());

                // Get pending accounts
                var pendingCmd = new SqlCommand("SELECT COUNT(*) FROM HpEntry WHERE AutoConsultantId=@id AND AccountPending=1 AND Free=0", conn);
                pendingCmd.Parameters.AddWithValue("@id", autoConsultantId);
                int pending = Convert.ToInt32(await pendingCmd.ExecuteScalarAsync());

                return Ok(new { limit, pending });
            }
            
        }

        [Authorize]
        [HttpPut("UpdateHpEntry")]
        public IActionResult UpdateHpEntry([FromBody] HpEntryDetailsDto dto)
        {

            // 1) PDR lock check
            int pdrStatus = _hpEntryService.CheckAmounts(dto.BranchId, dto.PartyDebitAmount);
            if (pdrStatus != 1)
                return BadRequest(new {Message = "Can't update, PDR amount is greater than PDR lock amount." });

            // First-time behavior handled in UI (labels → textbox)

            DateTime HPEntryDate = _hpEntryService.GetHpEntryDate(dto.HPEntryId);
            int adjustDays = _hpEntryService.GetAdjustDays();

            DateTime adjustFrom = HPEntryDate.AddDays(-adjustDays);
            DateTime adjustEnd = HPEntryDate.AddDays(adjustDays);

            if (dto.AdjustDate < adjustFrom || dto.AdjustDate > adjustEnd)
                return BadRequest(new {Message = $"Adjustment Date should be in between '{adjustFrom.ToShortDateString()}' and '{adjustEnd.ToShortDateString()}'." });

            // Receipt + party debit
            decimal receiptAmount = _hpEntryService.GetReceiptAmount(dto.HPEntryId);
            decimal partyDebitExisting = _hpEntryService.GetPartyDebitAmount(dto.HPEntryId);
            decimal totalPartyDebit = partyDebitExisting + dto.PartyDebitAmount;

            if (totalPartyDebit < receiptAmount)
                return BadRequest(new { Message = "Can't update.Party debit amount is less than paid amount in receipt." });

            // Finance check
            int financeStatus = _hpEntryService.CheckFinanceAmount(dto.HPEntryId, dto.FinanceValue, dto.HPCharge, dto.DocCharges, dto.RTOCharge);
            if (financeStatus == 1)
                return BadRequest(new { Message = "Can't update, Tot Finance Value is less than paid amount in receipt's." });

            // Apply update
            _hpEntryService.UpdateHPFinanceValue(dto.HPEntryId, dto.FinanceValue, dto.DocCharges, dto.RTOCharge);

            // Update main record
            _hpEntryService.Sp_UpdateHPEntry(dto);

            // Party Debit Entry logic
            if (dto.PartyDebitAmount > 0)
            {                
                if (!_hpEntryService.PartyDebitExists(dto.HPEntryId))
                {
                    _hpEntryService.InsertPartyDebitEntry(dto);
                }
                else
                {
                    _hpEntryService.UpdatePartyDebitEntry(dto.HPEntryId, dto.PartyDebitAmount);
                }
                    
            }
            else
            {
                if (_hpEntryService.PartyDebitExists(dto.HPEntryId))
                    _hpEntryService.DeletePartyDebitEntry(dto.HPEntryId);
            }

            return Ok("Data updated successfully.");
        }

        [Authorize]
        [HttpPost("CreateHpEntry")]
        public async Task <IActionResult> CreateHpEntry([FromBody] HpEntryDetailsDto dto)
        {
            // 1) PDR Lock
            if (_hpEntryService.CheckAmounts(dto.BranchId, dto.PartyDebitAmount) != 1)
                return BadRequest(new { Message = "Can't insert, PDR amount is greater than PDR lock amount." });

            // 2) Funding Restriction
            if (_hpEntryService.CheckFundingRestriction(dto.BranchId, dto.VehicleId, dto.VehicleModelYear, dto.FinanceValue, dto.DocCharges, dto.RTOCharge) != 1)
                return BadRequest(new { Message = "Can't insert, Finance value is greater than Funding Value." });

            // 3) Adjust Date Validation
            int adjustDays = _hpEntryService.GetAdjustDays();
            DateTime today = DateTime.Today;
            var adjustFrom = today.AddDays(-adjustDays);
            var adjustEnd = today.AddDays(adjustDays);

            if (dto.AdjustDate < adjustFrom || dto.AdjustDate > adjustEnd)
                return BadRequest(new { Message = $"Adjustment Date must be between {adjustFrom:dd-MM-yyyy} and {adjustEnd:dd-MM-yyyy}" });

            // 4) Cash Validation
            decimal closing = _openingBalanceService.GetClosingBalance(dto.BranchId, dto.CompanyId, DateTime.Today);
            decimal totalNeeded = dto.FinanceValue + dto.PartyDebitAmount;

            if (closing < totalNeeded)
                return BadRequest(new { Message = $"Can't insert, Balance is {closing}" });

            // 5) Insert HP Entry
            int newId = _hpEntryService.InsertHpEntry(dto);
            if (newId <= 0)
                return StatusCode(500, new { Message = "Failed to insert HP Entry" });

            // 6) Party Debit (optional)
            if (dto.PartyDebitAmount > 0)
                _hpEntryService.InsertPartyDebitEntry(newId, dto.PartyDebitAmount, dto.BranchId, dto.CompanyId, dto.UserId);

            // 7) Send SMS
            await _smsService.sendSMS(newId, dto);

            return Ok(new { HpEntryId = newId, Message = "HP Entry created successfully." });
        }

        [Authorize]
        [HttpGet("CheckAadhar")]
        public IActionResult CheckAadhar(string aadharNumber)
        {
            if (string.IsNullOrWhiteSpace(aadharNumber))
                return BadRequest("Aadhar number is required.");

            using var con = GetConnection();

            try
            {
                con.Open();
                var cmd = new SqlCommand(@"
            SELECT HPEntry_Id, Date, Description, companyCode, BranchName
            FROM vw_BlockCustomer
            WHERE BlockCustomerId IS NOT NULL AND AdharNumber LIKE @Aadhar", con);

                cmd.Parameters.AddWithValue("@Aadhar", aadharNumber);

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    return Ok(new
                    {
                        IsBlocked = true,
                        Message = "Aadhaar blocked, cannot insert record.",
                        BlockedInfo = $"CUSTOMER BLOCKED IN {reader["BranchName"]} ({reader["companyCode"]}) A/C No : {reader["HPEntry_Id"]}, Date : {Convert.ToDateTime(reader["Date"]).ToShortDateString()}, Description : {reader["Description"]}"
                    });
                }

                return Ok(new { IsBlocked = false, Message = "Aadhaar is allowed." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Server Error", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("DeleteHpEntry/{hpEntryId}")]
        public IActionResult DeleteHpEntry(int hpEntryId)
        {
            decimal receiptAmount = _hpEntryService.GetReceiptAmount(hpEntryId);
            decimal partyDebitAmount = _hpEntryService.GetPartyDebitAmount(hpEntryId);

            if (partyDebitAmount < receiptAmount)
                return BadRequest("Can't delete. Party debit amount is less than receipt amount.");

            using var con = GetConnection();
            con.Open();

            using var transaction = con.BeginTransaction();

            try
            {
                // 1) Delete PartyDebit related to this HPEntry
                using (var cmd = new SqlCommand(
                    "DELETE FROM PartyDebit WHERE HPEntryId = @HPEntryId AND FromHpEntry = 1", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@HPEntryId", hpEntryId);
                    cmd.ExecuteNonQuery();
                }

                // 2) Delete HPEntry using stored procedure
                using (var cmdDelete = new SqlCommand("sp_HPEntry_Delete", con, transaction))
                {
                    cmdDelete.CommandType = CommandType.StoredProcedure;
                    cmdDelete.Parameters.AddWithValue("@HPEntryId", hpEntryId);
                    cmdDelete.ExecuteNonQuery();
                }

                transaction.Commit();
                return Ok("HP Entry deleted successfully.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return BadRequest("Data in use. Can't delete.");
            }
        }



        #endregion
    }
}
