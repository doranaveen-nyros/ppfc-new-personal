using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ppfc.DTO;
using System.Data;
using System.Data.SqlClient;

namespace ppfc.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MasterController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public MasterController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        #region Company APIs

        [Authorize]
        [HttpGet("GetCompany/{companyId}")]
        public IActionResult GetCompany(int companyId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var cmd = new SqlCommand("SELECT CompanyId, CompanyName FROM Company WHERE CompanyId = @CompanyId", conn);
                cmd.Parameters.AddWithValue("@CompanyId", companyId);

                var companies = new List<CompaniesDto>();

                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        companies.Add(new CompaniesDto
                        {
                            CompanyId = Convert.ToInt32(dr["CompanyId"]),
                            CompanyName = dr["CompanyName"].ToString()!
                        });
                    }
                }

                if (companies.Count == 0)
                    return NotFound($"No company found with ID {companyId}.");

                return Ok(companies);
            }
        }

        [Authorize]
        [HttpPut("UpdateCompany/{companyId}")]
        public IActionResult UpdateCompany(int companyId, [FromBody] CompaniesDto company)
        {
            if (string.IsNullOrWhiteSpace(company.CompanyName))
            {
                return BadRequest("Company name cannot be empty.");
            }

            using (var conn = GetConnection())
            {
                conn.Open();

                try
                {
                    var cmd = new SqlCommand("UPDATE Company SET CompanyName = @CompanyName WHERE CompanyId = @CompanyId", conn);
                    cmd.Parameters.AddWithValue("@CompanyName", company.CompanyName);
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                        return NotFound($"No company found with ID {companyId}.");

                    return Ok(new { message = "Record updated successfully." });
                }
                catch (SqlException ex)
                {
                    if (ex.Message.Contains("Violation of UNIQUE KEY constraint 'UK_Company'"))
                    {
                        return Conflict(new { message = $"Company name '{company.CompanyName}' already exists. Cannot update." });
                    }
                    else
                    {
                        return StatusCode(500, new { message = "Failed to update the record.", error = ex.Message });
                    }
                }
            }
        }

        #endregion

        #region Branch APIs

        [HttpGet("GetBranches/{companyId}")]
        public IActionResult GetBranches(int companyId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_Branch_Select", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);

                    var branches = new List<BranchesDto>();

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            branches.Add(new BranchesDto
                            {
                                BranchId = Convert.ToInt32(dr["BranchId"]),
                                CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                CompanyName = dr["CompanyName"].ToString(),
                                BranchName = dr["BranchName"].ToString(),
                                LockCapital = dr["LockCapital"] != DBNull.Value && Convert.ToBoolean(dr["LockCapital"])
                            });
                        }
                    }

                    return Ok(branches);
                }
            }
        }

        [HttpPost("AddBranch")]
        public IActionResult AddBranch([FromBody] BranchesDto branch)
        {
            if (branch == null || string.IsNullOrWhiteSpace(branch.BranchName))
                return BadRequest("Branch name cannot be empty.");

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_Branch_Insert", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@CompanyId", branch.CompanyId);
                    cmd.Parameters.AddWithValue("@BranchName", branch.BranchName);

                    try
                    {
                        cmd.ExecuteNonQuery();
                        return Ok(new { message = "Record inserted successfully." });
                    }
                    catch (SqlException ex)
                    {
                        if (ex.Message.Contains("UNIQUE KEY constraint 'UK_BranchName'"))
                        {
                            return Conflict(new
                            {
                                message = $"Branch name '{branch.BranchName}' already exists for this company."
                            });
                        }

                        return StatusCode(500, new { message = "Failed to insert the record." });
                    }
                }
            }
        }

        [HttpPut("UpdateBranch")]
        public IActionResult UpdateBranch([FromBody] BranchesDto branch)
        {
            if (branch == null || string.IsNullOrWhiteSpace(branch.BranchName))
                return BadRequest("Branch name cannot be empty.");

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_Branch_Update", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@BranchId", branch.BranchId);
                    cmd.Parameters.AddWithValue("@CompanyId", branch.CompanyId);
                    cmd.Parameters.AddWithValue("@BranchName", branch.BranchName);

                    try
                    {
                        cmd.ExecuteNonQuery();
                        return Ok(new { message = "Record updated successfully." });
                    }
                    catch (SqlException ex)
                    {
                        if (ex.Message.Contains("UNIQUE KEY constraint 'UK_BranchName'"))
                        {
                            return Conflict(new
                            {
                                message = $"Branch name '{branch.BranchName}' already exists — cannot update."
                            });
                        }

                        return StatusCode(500, new { message = "Failed to update the record." });
                    }
                }
            }
        }

        [HttpDelete("DeleteBranch/{branchId}")]
        public IActionResult DeleteBranch(int branchId)
        {
            if (branchId <= 0)
                return BadRequest("Invalid BranchId.");

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_Branch_Delete", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@BranchId", branchId);

                    try
                    {
                        cmd.ExecuteNonQuery();
                        return Ok(new { message = "Record deleted successfully." });
                    }
                    catch (SqlException ex)
                    {
                        // "Data in use can't delete" equivalent
                        return Conflict(new
                        {
                            message = "Data in use — cannot delete this branch.",
                            details = ex.Message
                        });
                    }
                }
            }
        }

        [Authorize]
        [HttpPut("ToggleLock/{branchId}")]
        public IActionResult ToggleLock(int branchId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Step 1: Get current lock state
                bool currentLock;
                using (var checkCmd = new SqlCommand("SELECT LockCapital FROM Branch WHERE BranchId = @BranchId", conn))
                {
                    checkCmd.Parameters.AddWithValue("@BranchId", branchId);
                    currentLock = Convert.ToBoolean(checkCmd.ExecuteScalar());
                }

                // Step 2: Flip the lock value
                bool newLock = !currentLock;

                // Step 3: Update lock state
                using (var updateCmd = new SqlCommand("UPDATE Branch SET LockCapital = @LockCapital WHERE BranchId = @BranchId", conn))
                {
                    updateCmd.Parameters.AddWithValue("@LockCapital", newLock);
                    updateCmd.Parameters.AddWithValue("@BranchId", branchId);
                    updateCmd.ExecuteNonQuery();
                }

                return Ok(new
                {
                    branchId,
                    lockCapital = newLock,
                    message = newLock
                        ? "Branch locked successfully."
                        : "Branch unlocked successfully."
                });
            }
        }



        #endregion

        #region Area APIs

        [Authorize]
        [HttpGet("GetAreas/{companyId}")]
        public IActionResult GetAreas(int companyId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT AreaId,
                   AreaName,
                   AreaCode,
                   BranchId,
                   BranchName,
                   CompanyId,
                   CompanyName,
                   Lock
            FROM vw_Area
            WHERE CompanyId = @CompanyId
            ORDER BY AreaName";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);

                    var areas = new List<AreasDto>();

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            areas.Add(new AreasDto
                            {
                                AreaId = Convert.ToInt32(dr["AreaId"]),
                                AreaName = dr["AreaName"].ToString()!,
                                AreaCode = dr["AreaCode"].ToString()!,
                                BranchId = Convert.ToInt32(dr["BranchId"]),
                                BranchName = dr["BranchName"].ToString()!,
                                CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                CompanyName = dr["CompanyName"].ToString()!,
                                Lock = Convert.ToBoolean(dr["Lock"])
                            });
                        }
                    }

                    return Ok(areas);
                }
            }
        }

        [Authorize]
        [HttpPost("AddArea")]
        public IActionResult AddArea([FromBody] AreasDto area)
        {

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_Area_Insert", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@AreaName", area.AreaName.Trim());
                        cmd.Parameters.AddWithValue("@BranchId", area.BranchId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_AREA_AREANAME"))
                {
                    return Conflict(new
                    {
                        Message = $"Area name '{area.AreaName}' already exists for the company.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateArea")]
        public IActionResult UpdateArea([FromBody] AreasDto area)
        {

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_Area_Update", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@AreaId", area.AreaId);
                        cmd.Parameters.AddWithValue("@AreaName", area.AreaName.Trim());
                        cmd.Parameters.AddWithValue("@BranchId", area.BranchId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_AREA_AREANAME"))
                {
                    return Conflict(new
                    {
                        Message = $"Area name '{area.AreaName}' already exists.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteArea/{areaId}")]
        public IActionResult DeleteArea(int areaId)
        {
            if (areaId <= 0)
                return BadRequest(new { Message = "Invalid Area ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_Area_Delete", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@AreaId", areaId);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Area not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                // 💡 "Data in use can't delete" → likely a foreign key constraint error
                if (ex.Message.Contains("REFERENCE constraint") || ex.Message.Contains("foreign key"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — cannot delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("ToggleAreaLock/{areaId}")]
        public IActionResult ToggleAreaLock(int areaId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Step 1: Get current lock status
                bool currentLock;
                using (var checkCmd = new SqlCommand("SELECT [Lock] FROM Area WHERE AreaId = @AreaId", conn))
                {
                    checkCmd.Parameters.AddWithValue("@AreaId", areaId);

                    var result = checkCmd.ExecuteScalar();
                    if (result == null)
                    {
                        return NotFound(new
                        {
                            Message = "Area not found.",
                            Success = false
                        });
                    }

                    currentLock = Convert.ToBoolean(result);
                }

                // Step 2: Flip the lock
                bool newLock = !currentLock;

                // Step 3: Update DB
                using (var updateCmd = new SqlCommand("UPDATE Area SET [Lock] = @Lock WHERE AreaId = @AreaId", conn))
                {
                    updateCmd.Parameters.AddWithValue("@Lock", newLock);
                    updateCmd.Parameters.AddWithValue("@AreaId", areaId);
                    updateCmd.ExecuteNonQuery();
                }

                return Ok(new
                {
                    AreaId = areaId,
                    Lock = newLock,
                    Message = newLock
                        ? "Area locked successfully."
                        : "Area unlocked successfully.",
                    Success = true
                });
            }
        }

        #endregion

        #region Credit Master APIs

        [Authorize]
        [HttpGet("GetCredits/{companyId}")]
        public IActionResult GetCredits(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT CompanyId,
                       CreditId,
                       CreditAccountName
                FROM vw_Credit
                WHERE CompanyId = @CompanyId
                ORDER BY CreditAccountName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var credits = new List<CreditDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                credits.Add(new CreditDto
                                {
                                    CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                    CreditId = Convert.ToInt32(dr["CreditId"]),
                                    CreditAccountName = dr["CreditAccountName"].ToString()!
                                });
                            }
                        }

                        return Ok(credits);
                    }
                }
            }
            catch (SqlException ex)
            {
                // Database related errors (connectivity, syntax, etc.)
                return StatusCode(500, new
                {
                    Message = "Database error occurred while fetching Credit Accounts.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddCredit")]
        public IActionResult AddCredit([FromBody] CreditDto credit)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"INSERT INTO Credit (CreditAccountName, CompanyId) 
                          VALUES (@CreditAccountName, @CompanyId)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CreditAccountName", credit.CreditAccountName.Trim());
                        cmd.Parameters.AddWithValue("@CompanyId", credit.CompanyId);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Credit_CreditAccountName"))
                {
                    return Conflict(new
                    {
                        Message = $"Credit account name '{credit.CreditAccountName}' already exists! Can't insert.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        [Authorize]
        [HttpPut("UpdateCredit")]
        public IActionResult UpdateCredit([FromBody] CreditDto credit)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    var query = @"UPDATE Credit 
                          SET CreditAccountName = @CreditAccountName,
                              CompanyId = @CompanyId
                          WHERE CreditId = @CreditId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CreditId", credit.CreditId);
                        cmd.Parameters.AddWithValue("@CreditAccountName", credit.CreditAccountName.Trim());
                        cmd.Parameters.AddWithValue("@CompanyId", credit.CompanyId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Credit_CreditAccountName"))
                {
                    return Conflict(new
                    {
                        Message = $"Credit account name '{credit.CreditAccountName}' already exists! Can't update.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteCredit/{creditId}")]
        public IActionResult DeleteCredit(int creditId)
        {
            if (creditId <= 0)
                return BadRequest(new { Message = "Invalid Credit ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    var query = "DELETE FROM Credit WHERE CreditId = @CreditId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CreditId", creditId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Credit record not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                // Same message as WebForms: “Data in use can’t delete”
                if (ex.Message.Contains("REFERENCE") || ex.Message.Contains("FOREIGN KEY"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }



        #endregion

        #region Debit Master APIs

        [Authorize]
        [HttpGet("GetDebits/{companyId}")]
        public IActionResult GetDebits(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT CompanyId,
                       DebitId,
                       DebitAccountName
                FROM vw_Debit
                WHERE CompanyId = @CompanyId
                ORDER BY DebitAccountName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var debits = new List<DebitDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                debits.Add(new DebitDto
                                {
                                    CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                    DebitId = Convert.ToInt32(dr["DebitId"]),
                                    DebitAccountName = dr["DebitAccountName"].ToString()!
                                });
                            }
                        }

                        return Ok(debits);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Debit Account data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddDebit")]
        public IActionResult AddDebit([FromBody] DebitDto debit)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"INSERT INTO Debit (DebitAccountName, CompanyId)
                          VALUES (@DebitAccountName, @CompanyId)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DebitAccountName", debit.DebitAccountName.Trim());
                        cmd.Parameters.AddWithValue("@CompanyId", debit.CompanyId);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Debit_DebitAccountName"))
                {
                    return Conflict(new
                    {
                        Message = $"Debit account name '{debit.DebitAccountName}' already exists! Can't insert.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateDebit")]
        public IActionResult UpdateDebit([FromBody] DebitDto debit)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"UPDATE Debit
                          SET DebitAccountName = @DebitAccountName,
                              CompanyId = @CompanyId
                          WHERE DebitId = @DebitId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DebitId", debit.DebitId);
                        cmd.Parameters.AddWithValue("@DebitAccountName", debit.DebitAccountName.Trim());
                        cmd.Parameters.AddWithValue("@CompanyId", debit.CompanyId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Debit_DebitAccountName"))
                {
                    return Conflict(new
                    {
                        Message = $"Debit account name '{debit.DebitAccountName}' already exists! Can't update.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteDebit/{debitId}")]
        public IActionResult DeleteDebit(int debitId)
        {
            if (debitId <= 0)
                return BadRequest(new { Message = "Invalid Debit ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE FROM Debit WHERE DebitId = @DebitId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@DebitId", debitId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Debit record not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("REFERENCE") || ex.Message.Contains("FOREIGN KEY"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }



        #endregion

        #region Revenue Head APIs

        [Authorize]
        [HttpGet("GetRevenueHeads/{companyId}")]
        public IActionResult GetRevenueHeads(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT RevenueHeadId,
                       RevenueHeadName,
                       CompanyId,
                       CompanyName,
                       BranchId,
                       BranchName
                FROM vw_Revenue
                WHERE CompanyId = @CompanyId
                ORDER BY RevenueHeadName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var revenueHeads = new List<RevenueHeadDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                revenueHeads.Add(new RevenueHeadDto
                                {
                                    RevenueHeadId = Convert.ToInt32(dr["RevenueHeadId"]),
                                    RevenueHeadName = dr["RevenueHeadName"].ToString()!,
                                    CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                    CompanyName = dr["CompanyName"].ToString()!,
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    BranchName = dr["BranchName"].ToString()!
                                });
                            }
                        }

                        return Ok(revenueHeads);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Revenue Head data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddRevenueHead")]
        public IActionResult AddRevenueHead([FromBody] RevenueHeadDto revenue)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_RevenueHead_Insert", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@RevenueHeadName", revenue.RevenueHeadName.Trim());
                        cmd.Parameters.AddWithValue("@CompanyId", revenue.CompanyId);
                        cmd.Parameters.AddWithValue("@BranchId", revenue.BranchId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_RevenueHead_RevenueHeadName"))
                {
                    return Conflict(new
                    {
                        Message = $"Revenue head name '{revenue.RevenueHeadName}' already exists for this company.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateRevenueHead")]
        public IActionResult UpdateRevenueHead([FromBody] RevenueHeadDto revenue)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_RevenueHead_Update", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@RevenueHeadId", revenue.RevenueHeadId);
                        cmd.Parameters.AddWithValue("@RevenueHeadName", revenue.RevenueHeadName.Trim());
                        cmd.Parameters.AddWithValue("@CompanyId", revenue.CompanyId);
                        cmd.Parameters.AddWithValue("@BranchId", revenue.BranchId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_RevenueHead_RevenueHeadName"))
                {
                    return Conflict(new
                    {
                        Message = $"Revenue head name '{revenue.RevenueHeadName}' already exists for this company.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteRevenueHead/{revenueHeadId}")]
        public IActionResult DeleteRevenueHead(int revenueHeadId)
        {
            if (revenueHeadId <= 0)
                return BadRequest(new { Message = "Invalid RevenueHead ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = "DELETE FROM RevenueHead WHERE RevenueHeadId = @RevenueHeadId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@RevenueHeadId", revenueHeadId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Revenue head not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                // same WebForms behavior: "Data in use can't delete"
                if (ex.Message.Contains("REFERENCE") || ex.Message.Contains("FOREIGN KEY"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion

        #region Expense Master APIs

        [Authorize]
        [HttpGet("GetExpenses")]
        public IActionResult GetExpenses()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT ExpenseId,
                       ExpenseName
                FROM Expense
                ORDER BY ExpenseName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var expenses = new List<ExpenseDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                expenses.Add(new ExpenseDto
                                {
                                    ExpenseId = Convert.ToInt32(dr["ExpenseId"]),
                                    ExpenseName = dr["ExpenseName"].ToString()!
                                });
                            }
                        }

                        return Ok(expenses);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Expense data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddExpense")]
        public IActionResult AddExpense([FromBody] ExpenseDto expense)
        {
            if (string.IsNullOrWhiteSpace(expense.ExpenseName))
                return BadRequest(new { Message = "Please enter expense name." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"INSERT INTO Expense (ExpenseName)
                          VALUES (@ExpenseName)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ExpenseName", expense.ExpenseName.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Expenses_ExpenseName"))
                {
                    return Conflict(new
                    {
                        Message = $"Expense name '{expense.ExpenseName}' already exists! Can't insert.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateExpense")]
        public IActionResult UpdateExpense([FromBody] ExpenseDto expense)
        {
            if (expense.ExpenseId <= 0)
                return BadRequest(new { Message = "Invalid Expense ID." });

            if (string.IsNullOrWhiteSpace(expense.ExpenseName))
                return BadRequest(new { Message = "Please enter expense name." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"UPDATE Expense 
                          SET ExpenseName = @ExpenseName
                          WHERE ExpenseId = @ExpenseId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ExpenseId", expense.ExpenseId);
                        cmd.Parameters.AddWithValue("@ExpenseName", expense.ExpenseName.Trim());

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Expenses_ExpenseName"))
                {
                    return Conflict(new
                    {
                        Message = $"Expense name '{expense.ExpenseName}' already exists! Can't update.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteExpense/{expenseId}")]
        public IActionResult DeleteExpense(int expenseId)
        {
            if (expenseId <= 0)
                return BadRequest(new { Message = "Invalid Expense ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE FROM Expense WHERE ExpenseId = @ExpenseId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ExpenseId", expenseId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Expense record not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                // Matches WebForms: "Data in use can't delete"
                if (ex.Message.Contains("REFERENCE") || ex.Message.Contains("FOREIGN KEY"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion

        #region Vehicle Master APIs

        [Authorize]
        [HttpGet("GetVehicles/{companyId}")]
        public IActionResult GetVehicles(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Same logic as WebForms
                    int finalCompanyId = (companyId == 1 || companyId == 2) ? 1 :
                                         (companyId == 3) ? 3 : 4;

                    var query = @"
                SELECT VehicleId, VehicleName, VCode, CompanyId
                FROM Vehicle
                WHERE CompanyId = @CompanyId
                ORDER BY VehicleName ASC";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", finalCompanyId);

                        var vehicles = new List<VehicleDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                vehicles.Add(new VehicleDto
                                {
                                    VehicleId = Convert.ToInt32(dr["VehicleId"]),
                                    VehicleName = dr["VehicleName"].ToString()!,
                                    VCode = dr["VCode"].ToString()!,
                                    CompanyId = Convert.ToInt32(dr["CompanyId"])
                                });
                            }
                        }

                        return Ok(vehicles);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Vehicle data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddVehicle")]
        public IActionResult AddVehicle([FromBody] VehicleDto vehicle)
        {
            // CompanyId selection logic
            int finalCompanyId = (vehicle.CompanyId == 1 || vehicle.CompanyId == 2) ? 1 :
                                 (vehicle.CompanyId == 3) ? 3 : 4;

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                INSERT INTO Vehicle (VehicleName, VCode, CompanyId)
                VALUES (@VehicleName, @VCode, @CompanyId)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@VehicleName", vehicle.VehicleName.Trim());
                        cmd.Parameters.AddWithValue("@VCode", vehicle.VCode.Trim());
                        cmd.Parameters.AddWithValue("@CompanyId", finalCompanyId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Vehicle_VehicleName"))
                {
                    return Conflict(new
                    {
                        Message = $"Vehicle name '{vehicle.VehicleName}' already exists! Can't insert.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateVehicle")]
        public IActionResult UpdateVehicle([FromBody] VehicleDto vehicle)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                UPDATE Vehicle
                SET VehicleName = @VehicleName,
                    VCode = @VCode
                WHERE VehicleId = @VehicleId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@VehicleId", vehicle.VehicleId);
                        cmd.Parameters.AddWithValue("@VehicleName", vehicle.VehicleName.Trim());
                        cmd.Parameters.AddWithValue("@VCode", vehicle.VCode.Trim());

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Vehicle_VehicleName"))
                {
                    return Conflict(new
                    {
                        Message = $"Vehicle name '{vehicle.VehicleName}' already exists! Can't update.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteVehicle/{vehicleId}")]
        public IActionResult DeleteVehicle(int vehicleId)
        {
            if (vehicleId <= 0)
                return BadRequest(new { Message = "Invalid Vehicle ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE Vehicle WHERE VehicleId = @VehicleId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@VehicleId", vehicleId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Vehicle record not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                // Same behavior as WebForms: Data in use can't delete
                if (ex.Message.Contains("REFERENCE") || ex.Message.Contains("FOREIGN KEY"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion

        #region Interest APIs

        [Authorize]
        [HttpGet("GetInterest/{companyId}")]
        public IActionResult GetInterest(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT InterestId,
                       Interest,
                       Type,
                       CompanyId
                FROM Interest
                WHERE CompanyId = @CompanyId
                ORDER BY Type";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var interestList = new List<InterestDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                interestList.Add(new InterestDto
                                {
                                    InterestId = Convert.ToInt32(dr["InterestId"]),
                                    Interest = Convert.ToDecimal(dr["Interest"]),
                                    Type = dr["Type"].ToString()!,
                                    CompanyId = Convert.ToInt32(dr["CompanyId"])
                                });
                            }
                        }

                        return Ok(interestList);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Interest data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateInterest")]
        public IActionResult UpdateInterest([FromBody] InterestDto interest)
        {
            if (interest.InterestId <= 0)
                return BadRequest(new { Message = "Invalid Interest ID." });

            if (interest.Interest < 0)
                return BadRequest(new { Message = "Interest value cannot be negative." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                UPDATE Interest
                SET Interest = @Interest
                WHERE InterestId = @InterestId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@InterestId", interest.InterestId);
                        cmd.Parameters.AddWithValue("@Interest", interest.Interest);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteInterest/{interestId}")]
        public IActionResult DeleteInterest(int interestId)
        {
            if (interestId <= 0)
                return BadRequest(new { Message = "Invalid Interest ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE FROM Interest WHERE InterestId = @InterestId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@InterestId", interestId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Interest record not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                // Same WebForms behavior: Data in use cannot delete
                if (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("REFERENCE"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        #endregion

        #region Auto Consultant APIs

        [Authorize]
        [HttpGet("GetAutoConsultants/{companyId}")]
        public IActionResult GetAutoConsultants(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT AutoConsultantId,
                       AutoConsultantName,
                       PhoneNo,
                       Limit,
                       BranchId,
                       BranchName,
                       CompanyId,
                       UPIType,
                       UPIName,
                       Bank,
                       AccountNumber,
                       AccountName,
                       IFSCCode,
                       Lock
                FROM vw_AutoConsultant
                WHERE CompanyId = @CompanyId
                ORDER BY AutoConsultantName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var list = new List<AutoConsultantDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new AutoConsultantDto
                                {
                                    AutoConsultantId = Convert.ToInt32(dr["AutoConsultantId"]),
                                    AutoConsultantName = dr["AutoConsultantName"].ToString()!,
                                    PhoneNo = dr["PhoneNo"].ToString()!,
                                    Limit = Convert.ToDecimal(dr["Limit"]),
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    BranchName = dr["BranchName"].ToString()!,
                                    CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                    UPIType = dr["UPIType"].ToString()!,
                                    UPIName = dr["UPIName"].ToString()!,
                                    Bank = dr["Bank"].ToString()!,
                                    AccountNumber = dr["AccountNumber"].ToString()!,
                                    AccountName = dr["AccountName"].ToString()!,
                                    IFSCCode = dr["IFSCCode"].ToString()!,
                                    Lock = dr["Lock"] != DBNull.Value && Convert.ToBoolean(dr["Lock"])
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Auto Consultant data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddAutoConsultant")]
        public IActionResult AddAutoConsultant([FromBody] AutoConsultantDto ac)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_AutoConsultant_Insert", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@AutoConsultantName", ac.AutoConsultantName.Trim());
                        cmd.Parameters.AddWithValue("@PhoneNo", ac.PhoneNo.Trim());
                        cmd.Parameters.AddWithValue("@Limit", ac.Limit);
                        cmd.Parameters.AddWithValue("@BranchId", ac.BranchId);
                        cmd.Parameters.AddWithValue("@UPIType", ac.UPIType.Trim());
                        cmd.Parameters.AddWithValue("@UPIName", ac.UPIName.Trim());
                        cmd.Parameters.AddWithValue("@Bank", ac.Bank.Trim());
                        cmd.Parameters.AddWithValue("@AccountNumber", ac.AccountNumber.Trim());
                        cmd.Parameters.AddWithValue("@AccountName", ac.AccountName.Trim());
                        cmd.Parameters.AddWithValue("@IFSCCode", ac.IFSCCode.Trim());

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_AutoConsultant_AutoConsultantName"))
                {
                    return Conflict(new
                    {
                        Message = $"Auto Consultant name '{ac.AutoConsultantName}' already exists!",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateAutoConsultant")]
        public IActionResult UpdateAutoConsultant([FromBody] AutoConsultantDto ac)
        {
            if (ac.AutoConsultantId <= 0)
                return BadRequest(new { Message = "Invalid Auto Consultant ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_AutoConsultant_Update", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@AutoConsultantId", ac.AutoConsultantId);
                        cmd.Parameters.AddWithValue("@AutoConsultantName", ac.AutoConsultantName.Trim());
                        cmd.Parameters.AddWithValue("@PhoneNo", ac.PhoneNo.Trim());
                        cmd.Parameters.AddWithValue("@Limit", ac.Limit);
                        cmd.Parameters.AddWithValue("@BranchId", ac.BranchId);
                        cmd.Parameters.AddWithValue("@UPIType", ac.UPIType.Trim());
                        cmd.Parameters.AddWithValue("@UPIName", ac.UPIName.Trim());
                        cmd.Parameters.AddWithValue("@Bank", ac.Bank.Trim());
                        cmd.Parameters.AddWithValue("@AccountNumber", ac.AccountNumber.Trim());
                        cmd.Parameters.AddWithValue("@AccountName", ac.AccountName.Trim());
                        cmd.Parameters.AddWithValue("@IFSCCode", ac.IFSCCode.Trim());

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_AutoConsultant_AutoConsultantName"))
                {
                    return Conflict(new
                    {
                        Message = $"Auto Consultant name '{ac.AutoConsultantName}' already exists!",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteAutoConsultant/{autoConsultantId}")]
        public IActionResult DeleteAutoConsultant(int autoConsultantId)
        {
            if (autoConsultantId <= 0)
                return BadRequest(new { Message = "Invalid Auto Consultant ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = "DELETE FROM AutoConsultant WHERE AutoConsultantId = @AutoConsultantId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@AutoConsultantId", autoConsultantId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Auto Consultant not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("REFERENCE"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — cannot delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("ToggleAutoConsultantLock/{autoConsultantId}")]
        public IActionResult ToggleAutoConsultantLock(int autoConsultantId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Step 1: Get current lock status
                    bool currentLock;
                    using (var checkCmd = new SqlCommand(
                        "SELECT [Lock] FROM AutoConsultant WHERE AutoConsultantId = @AutoConsultantId", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@AutoConsultantId", autoConsultantId);

                        var result = checkCmd.ExecuteScalar();
                        if (result == null)
                        {
                            return NotFound(new
                            {
                                Message = "Auto Consultant not found.",
                                Success = false
                            });
                        }

                        // ✅ Null-safe lock handling
                        currentLock = result != DBNull.Value && Convert.ToBoolean(result);
                    }

                    // Step 2: Flip the lock
                    bool newLock = !currentLock;

                    // Step 3: Update DB
                    using (var updateCmd = new SqlCommand(
                        "UPDATE AutoConsultant SET [Lock] = @Lock WHERE AutoConsultantId = @AutoConsultantId", conn))
                    {
                        updateCmd.Parameters.AddWithValue("@Lock", newLock);
                        updateCmd.Parameters.AddWithValue("@AutoConsultantId", autoConsultantId);
                        updateCmd.ExecuteNonQuery();
                    }

                    return Ok(new
                    {
                        AutoConsultantId = autoConsultantId,
                        Lock = newLock,
                        Message = newLock
                            ? "Auto Consultant locked successfully."
                            : "Auto Consultant unlocked successfully.",
                        Success = true
                    });
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to toggle lock.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion

        #region RTO Agent APIs

        [Authorize]
        [HttpGet("GetRTOAgents/{companyId}")]
        public IActionResult GetRTOAgents(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT RTOAgentId,
                       RTOAgentName,
                       PhoneNo,
                       CompanyId,
                       BranchId,
                       BranchName,
                       LockAgent
                FROM vw_RTOAgent
                WHERE CompanyId = @CompanyId
                ORDER BY RTOAgentName ASC";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var list = new List<RTOAgentDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new RTOAgentDto
                                {
                                    RTOAgentId = Convert.ToInt32(dr["RTOAgentId"]),
                                    RTOAgentName = dr["RTOAgentName"].ToString()!,
                                    PhoneNo = dr["PhoneNo"].ToString()!,
                                    CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    BranchName = dr["BranchName"].ToString()!,

                                    // ✅ NULL-safe lock handling
                                    LockAgent = dr["LockAgent"] != DBNull.Value &&
                                                Convert.ToBoolean(dr["LockAgent"])
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load RTO Agent data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddRTOAgent")]
        public IActionResult AddRTOAgent([FromBody] RTOAgentDto agent)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_RTOAgent_Insert", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@RTOAgentName", agent.RTOAgentName.Trim());
                        cmd.Parameters.AddWithValue("@PhoneNo", agent.PhoneNo.Trim());
                        cmd.Parameters.AddWithValue("@BranchId", agent.BranchId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_RTOAgent_RTOAgentName"))
                {
                    return Conflict(new
                    {
                        Message = $"RTO Agent name '{agent.RTOAgentName}' already exists!",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateRTOAgent")]
        public IActionResult UpdateRTOAgent([FromBody] RTOAgentDto agent)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("sp_RTOAgent_Update", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@RTOAgentId", agent.RTOAgentId);
                        cmd.Parameters.AddWithValue("@RTOAgentName", agent.RTOAgentName.Trim());
                        cmd.Parameters.AddWithValue("@PhoneNo", agent.PhoneNo.Trim());
                        cmd.Parameters.AddWithValue("@BranchId", agent.BranchId);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_RTOAgent_RTOAgentName"))
                {
                    return Conflict(new
                    {
                        Message = $"RTO Agent name '{agent.RTOAgentName}' already exists!",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteRTOAgent/{rtoAgentId}")]
        public IActionResult DeleteRTOAgent(int rtoAgentId)
        {
            if (rtoAgentId <= 0)
                return BadRequest(new { Message = "Invalid RTO Agent ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand(
                        "DELETE FROM RTOAgent WHERE RTOAgentId = @RTOAgentId", conn))
                    {
                        cmd.Parameters.AddWithValue("@RTOAgentId", rtoAgentId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "RTO Agent not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("REFERENCE"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        [Authorize]
        [HttpPut("ToggleRTOAgentLock/{rtoAgentId}")]
        public IActionResult ToggleRTOAgentLock(int rtoAgentId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Step 1: Read current lock value
                    bool currentLock;
                    using (var checkCmd = new SqlCommand(
                        "SELECT LockAgent FROM RTOAgent WHERE RTOAgentId = @RTOAgentId", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@RTOAgentId", rtoAgentId);

                        var result = checkCmd.ExecuteScalar();
                        if (result == null)
                        {
                            return NotFound(new
                            {
                                Message = "RTO Agent not found.",
                                Success = false
                            });
                        }

                        currentLock = result != DBNull.Value && Convert.ToBoolean(result);
                    }

                    // Step 2: Toggle value
                    bool newLock = !currentLock;

                    // Step 3: Update DB
                    using (var updateCmd = new SqlCommand(
                        "UPDATE RTOAgent SET LockAgent = @LockAgent WHERE RTOAgentId = @RTOAgentId", conn))
                    {
                        updateCmd.Parameters.AddWithValue("@LockAgent", newLock);
                        updateCmd.Parameters.AddWithValue("@RTOAgentId", rtoAgentId);
                        updateCmd.ExecuteNonQuery();
                    }

                    return Ok(new
                    {
                        RTOAgentId = rtoAgentId,
                        LockAgent = newLock,
                        Message = newLock
                            ? "RTO Agent locked successfully."
                            : "RTO Agent unlocked successfully.",
                        Success = true
                    });
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to toggle lock.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion

        #region Notice Type APIs

        [Authorize]
        [HttpGet("GetNoticeTypes")]
        public IActionResult GetNoticeTypes()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT NoticeTypeId,
                       NoticeType,
                       Notice
                FROM NoticeType
                ORDER BY NoticeType ASC";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var list = new List<NoticeTypeDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new NoticeTypeDto
                                {
                                    NoticeTypeId = Convert.ToInt32(dr["NoticeTypeId"]),
                                    NoticeType = dr["NoticeType"].ToString()!,
                                    Notice = dr["Notice"].ToString()!
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Notice Type data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddNoticeType")]
        public IActionResult AddNoticeType([FromBody] NoticeTypeDto notice)
        {
            if (string.IsNullOrWhiteSpace(notice.NoticeType))
                return BadRequest(new { Message = "Please enter Notice Type." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"INSERT INTO NoticeType (NoticeType, Notice)
                          VALUES (@NoticeType, @Notice)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NoticeType", notice.NoticeType.Trim());
                        cmd.Parameters.AddWithValue("@Notice", notice.Notice?.Trim() ?? "");

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_NoticeType"))
                {
                    return Conflict(new
                    {
                        Message = $"Notice Type '{notice.NoticeType}' already exists.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateNoticeType")]
        public IActionResult UpdateNoticeType([FromBody] NoticeTypeDto notice)
        {
            if (notice.NoticeTypeId <= 0)
                return BadRequest(new { Message = "Invalid Notice Type ID." });

            if (string.IsNullOrWhiteSpace(notice.NoticeType))
                return BadRequest(new { Message = "Please enter Notice Type." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                UPDATE NoticeType
                SET NoticeType = @NoticeType,
                    Notice = @Notice
                WHERE NoticeTypeId = @NoticeTypeId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NoticeTypeId", notice.NoticeTypeId);
                        cmd.Parameters.AddWithValue("@NoticeType", notice.NoticeType.Trim());
                        cmd.Parameters.AddWithValue("@Notice", notice.Notice?.Trim() ?? "");

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_NoticeType"))
                {
                    return Conflict(new
                    {
                        Message = $"Notice Type '{notice.NoticeType}' already exists!",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteNoticeType/{noticeTypeId}")]
        public IActionResult DeleteNoticeType(int noticeTypeId)
        {
            if (noticeTypeId <= 0)
                return BadRequest(new { Message = "Invalid Notice Type ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE FROM NoticeType WHERE NoticeTypeId = @NoticeTypeId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NoticeTypeId", noticeTypeId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Notice Type not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("REFERENCE"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete this record.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion

        #region Contact PhNo APIs

        [Authorize]
        [HttpGet("GetContactPhoneNumbers")]
        public IActionResult GetContactPhoneNumbers()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"SELECT PhoneNumber FROM ContactPhoneNo";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var list = new List<ContactPhoneDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new ContactPhoneDto
                                {
                                    PhoneNumber = dr["PhoneNumber"].ToString()!
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Contact Phone data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateContactPhoneNumber")]
        public IActionResult UpdateContactPhoneNumber([FromBody] ContactPhoneDto phone)
        {
            if (string.IsNullOrWhiteSpace(phone.PhoneNumber))
                return BadRequest(new { Message = "Please enter phone number." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"UPDATE ContactPhoneNo SET PhoneNumber = @PhoneNumber";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@PhoneNumber", phone.PhoneNumber.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteContactPhoneNumbers")]
        public IActionResult DeleteContactPhoneNumbers()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE FROM ContactPhoneNo";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("REFERENCE"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        #endregion

        #region Expense Limit APIs

        [Authorize]
        [HttpGet("GetBranchesForExpenseLimit/{companyId}")]
        public IActionResult GetBranchesForExpenseLimit(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT BranchId, BranchName
                FROM Branch
                WHERE CompanyId = @CompanyId
                AND BranchId NOT IN (SELECT BranchId FROM ExpenseLimit)
                ORDER BY BranchName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var list = new List<object>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new
                                {
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    BranchName = dr["BranchName"].ToString()!
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load branch list for Expense Limit.",
                    Error = ex.Message
                });
            }
        }


        [Authorize]
        [HttpGet("GetExpenseLimits/{companyId}")]
        public IActionResult GetExpenseLimits(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT ExpenseLimitId,
                       ExpenseLimitValue,
                       BranchId,
                       BranchName,
                       CompanyId
                FROM vw_ExpenseLimit
                WHERE CompanyId = @CompanyId
                ORDER BY BranchName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var list = new List<ExpenseLimitDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new ExpenseLimitDto
                                {
                                    ExpenseLimitId = Convert.ToInt32(dr["ExpenseLimitId"]),
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                    BranchName = dr["BranchName"].ToString()!,
                                    ExpenseLimitValue = Convert.ToInt32(dr["ExpenseLimitValue"])
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Expense Limit data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddExpenseLimit")]
        public IActionResult AddExpenseLimit([FromBody] ExpenseLimitDto exp)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                INSERT INTO ExpenseLimit (BranchId, ExpenseLimitValue)
                VALUES (@BranchId, @ExpenseLimitValue)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@BranchId", exp.BranchId);
                        cmd.Parameters.AddWithValue("@ExpenseLimitValue", exp.ExpenseLimitValue);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_ExpenseLimit_BranchId"))
                {
                    return Conflict(new
                    {
                        Message = "Expense limit already entered for this Branch.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateExpenseLimit")]
        public IActionResult UpdateExpenseLimit([FromBody] ExpenseLimitDto exp)
        {
            if (exp.ExpenseLimitId <= 0)
                return BadRequest(new { Message = "Invalid Expense Limit ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                UPDATE ExpenseLimit
                SET BranchId = @BranchId,
                    ExpenseLimitValue = @ExpenseLimitValue
                WHERE ExpenseLimitId = @ExpenseLimitId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ExpenseLimitId", exp.ExpenseLimitId);
                        cmd.Parameters.AddWithValue("@BranchId", exp.BranchId);
                        cmd.Parameters.AddWithValue("@ExpenseLimitValue", exp.ExpenseLimitValue);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_ExpenseLimit_BranchId"))
                {
                    return Conflict(new
                    {
                        Message = "Expense limit already entered for this Branch.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteExpenseLimit/{expenseLimitId}")]
        public IActionResult DeleteExpenseLimit(int expenseLimitId)
        {
            if (expenseLimitId <= 0)
                return BadRequest(new { Message = "Invalid Expense Limit ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE FROM ExpenseLimit WHERE ExpenseLimitId = @ExpenseLimitId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ExpenseLimitId", expenseLimitId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Record not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("REFERENCE"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion

        #region Pending Limit APIs

        [Authorize]
        [HttpGet("GetBranchesForPendingLimit/{companyId}")]
        public IActionResult GetBranchesForPendingLimit(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT BranchId, BranchName
                FROM Branch
                WHERE CompanyId = @CompanyId
                AND BranchId NOT IN (SELECT BranchId FROM PendingLimit)
                ORDER BY BranchName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var list = new List<object>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new
                                {
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    BranchName = dr["BranchName"].ToString()!
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load branch list for Pending Limit.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpGet("GetPendingLimits/{companyId}")]
        public IActionResult GetPendingLimits(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT PendingLimitId,
                       PendingLimitValue,
                       BranchId,
                       BranchName,
                       CompanyId
                FROM vw_PendingLimit
                WHERE CompanyId = @CompanyId
                ORDER BY BranchName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var list = new List<PendingLimitDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new PendingLimitDto
                                {
                                    PendingLimitId = Convert.ToInt32(dr["PendingLimitId"]),
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                    BranchName = dr["BranchName"].ToString()!,
                                    PendingLimitValue = Convert.ToInt32(dr["PendingLimitValue"])
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load Pending Limit data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddPendingLimit")]
        public IActionResult AddPendingLimit([FromBody] PendingLimitDto pl)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                INSERT INTO PendingLimit (BranchId, PendingLimitValue)
                VALUES (@BranchId, @PendingLimitValue)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@BranchId", pl.BranchId);
                        cmd.Parameters.AddWithValue("@PendingLimitValue", pl.PendingLimitValue);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_PendingLimit_BranchId"))
                {
                    return Conflict(new
                    {
                        Message = "Pending limit already entered for this Branch.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdatePendingLimit")]
        public IActionResult UpdatePendingLimit([FromBody] PendingLimitDto pl)
        {
            if (pl.PendingLimitId <= 0)
                return BadRequest(new { Message = "Invalid Pending Limit ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                UPDATE PendingLimit
                SET BranchId = @BranchId,
                    PendingLimitValue = @PendingLimitValue
                WHERE PendingLimitId = @PendingLimitId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@PendingLimitId", pl.PendingLimitId);
                        cmd.Parameters.AddWithValue("@BranchId", pl.BranchId);
                        cmd.Parameters.AddWithValue("@PendingLimitValue", pl.PendingLimitValue);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_PendingLimit_BranchId"))
                {
                    return Conflict(new
                    {
                        Message = "Pending limit already entered for this Branch.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeletePendingLimit/{pendingLimitId}")]
        public IActionResult DeletePendingLimit(int pendingLimitId)
        {
            if (pendingLimitId <= 0)
                return BadRequest(new { Message = "Invalid Pending Limit ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE FROM PendingLimit WHERE PendingLimitId = @PendingLimitId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@PendingLimitId", pendingLimitId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Record not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("REFERENCE"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion

        #region PDR Lock APIs

        [Authorize]
        [HttpGet("GetBranchesForPDRLock/{companyId}")]
        public IActionResult GetBranchesForPDRLock(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT BranchId, BranchName
                FROM Branch
                WHERE CompanyId = @CompanyId
                AND BranchId NOT IN (SELECT BranchId FROM PDRLock)
                ORDER BY BranchName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var list = new List<object>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new
                                {
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    BranchName = dr["BranchName"].ToString()!
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load branch list for PDR Lock.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpGet("GetPDRLocks/{companyId}")]
        public IActionResult GetPDRLocks(int companyId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT PDRLockId,
                       LockAmount,
                       PDRLock.BranchId,
                       BranchName,
                       CompanyId
                FROM PDRLock
                INNER JOIN Branch ON PDRLock.BranchId = Branch.BranchId
                WHERE CompanyId = @CompanyId
                ORDER BY BranchName";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyId", companyId);

                        var list = new List<PDRLockDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new PDRLockDto
                                {
                                    PDRLockId = Convert.ToInt32(dr["PDRLockId"]),
                                    BranchId = Convert.ToInt32(dr["BranchId"]),
                                    BranchName = dr["BranchName"].ToString()!,
                                    CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                    LockAmount = Convert.ToInt32(dr["LockAmount"])
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load PDR Lock data.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("AddPDRLock")]
        public IActionResult AddPDRLock([FromBody] PDRLockDto pdr)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                INSERT INTO PDRLock (BranchId, LockAmount)
                VALUES (@BranchId, @LockAmount)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@BranchId", pdr.BranchId);
                        cmd.Parameters.AddWithValue("@LockAmount", pdr.LockAmount);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record inserted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_PDRLock_BranchId"))
                {
                    return Conflict(new
                    {
                        Message = "PDR Lock already exists for this Branch.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to insert the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdatePDRLock")]
        public IActionResult UpdatePDRLock([FromBody] PDRLockDto pdr)
        {
            if (pdr.PDRLockId <= 0)
                return BadRequest(new { Message = "Invalid PDR Lock ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                UPDATE PDRLock
                SET BranchId = @BranchId,
                    LockAmount = @LockAmount
                WHERE PDRLockId = @PDRLockId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@PDRLockId", pdr.PDRLockId);
                        cmd.Parameters.AddWithValue("@BranchId", pdr.BranchId);
                        cmd.Parameters.AddWithValue("@LockAmount", pdr.LockAmount);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Record updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_PDRLock_BranchId"))
                {
                    return Conflict(new
                    {
                        Message = "PDR Lock already exists for this Branch.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to update the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpDelete("DeletePDRLock/{pdrLockId}")]
        public IActionResult DeletePDRLock(int pdrLockId)
        {
            if (pdrLockId <= 0)
                return BadRequest(new { Message = "Invalid PDR Lock ID." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"DELETE FROM PDRLock WHERE PDRLockId = @PDRLockId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@PDRLockId", pdrLockId);

                        var affected = cmd.ExecuteNonQuery();
                        if (affected == 0)
                        {
                            return NotFound(new
                            {
                                Message = "Record not found or already deleted.",
                                Success = false
                            });
                        }
                    }
                }

                return Ok(new
                {
                    Message = "Record deleted successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("REFERENCE"))
                {
                    return Conflict(new
                    {
                        Message = "Data in use — can't delete.",
                        Success = false
                    });
                }

                return StatusCode(500, new
                {
                    Message = "Failed to delete the record.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        #endregion

        #region Birthday Message APIs

        [Authorize]
        [HttpGet("GetBirthMessages")]
        public IActionResult GetBirthMessages()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT MessageId, Message, MessagePart, Status, Signature
                FROM Birth_Messages
                ORDER BY MessageId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var list = new List<BirthMessageDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new BirthMessageDto
                                {
                                    MessageId = Convert.ToInt32(dr["MessageId"]),
                                    Message = dr["Message"].ToString()!,
                                    MessagePart = dr["MessagePart"].ToString()!,
                                    Status = Convert.ToBoolean(dr["Status"]),
                                    Signature = dr["Signature"].ToString()!
                                });
                            }
                        }

                        return Ok(list);
                    }
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to load birthday messages.",
                    Error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateBirthMessage")]
        public IActionResult UpdateBirthMessage([FromBody] BirthMessageDto dto)
        {
            if (dto.MessageId <= 0 || dto.MessageId > 4)
                return BadRequest(new { Message = "Invalid MessageId." });

            // Check 110-char length rule
            string combined = (dto.Message ?? "") + " " + (dto.MessagePart ?? "");
            if (combined.Length > 110)
                return BadRequest(new { Message = "Message length must be less than 110 characters." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                UPDATE Birth_Messages
                SET Message = @Message,
                    MessagePart = @MessagePart
                WHERE MessageId = @MessageId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@MessageId", dto.MessageId);
                        cmd.Parameters.AddWithValue("@Message", dto.Message ?? "");
                        cmd.Parameters.AddWithValue("@MessagePart", dto.MessagePart ?? "");

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Message updated successfully.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to update birthday message.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("SetBirthMessageActive/{messageId}")]
        public IActionResult SetBirthMessageActive(int messageId)
        {
            if (messageId <= 0 || messageId > 4)
                return BadRequest(new { Message = "Invalid MessageId." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Activate only this message
                    var query1 = @"UPDATE Birth_Messages SET Status = 1 WHERE MessageId = @MessageId";
                    var query2 = @"UPDATE Birth_Messages SET Status = 0 WHERE MessageId != @MessageId";

                    using (var cmd = new SqlCommand(query1, conn))
                    {
                        cmd.Parameters.AddWithValue("@MessageId", messageId);
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd2 = new SqlCommand(query2, conn))
                    {
                        cmd2.Parameters.AddWithValue("@MessageId", messageId);
                        cmd2.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = $"Message {messageId} set as active.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to set active message.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }

        [Authorize]
        [HttpPut("UpdateBirthSignature")]
        public IActionResult UpdateBirthSignature([FromBody] BirthMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Signature))
                return BadRequest(new { Message = "Signature cannot be empty." });

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"UPDATE Birth_Messages SET Signature = @Signature";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Signature", dto.Signature.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    Message = "Signature updated successfully for all messages.",
                    Success = true
                });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    Message = "Failed to update signature.",
                    Error = ex.Message,
                    Success = false
                });
            }
        }


        #endregion
    }
}