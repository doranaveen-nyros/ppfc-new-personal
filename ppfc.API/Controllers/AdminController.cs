using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ppfc.API.Services;
using ppfc.DTO;
using System.Data;
using System.Data.SqlClient;
using System.Net;

namespace ppfc.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly SmsService _smsService;

        public AdminController(IConfiguration configuration, SmsService smsService)
        {
            _configuration = configuration;
            _smsService = smsService;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        #region Role Settings APIs

        [Authorize]
        [HttpGet("GetRoles")]
        public IActionResult GetRoles()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT RoleId, RoleName FROM Role WHERE (RoleName <> 'DBAdmin')", conn);
                var roles = new List<RoleDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        roles.Add(new RoleDto
                        {
                            RoleId = Convert.ToInt32(dr["RoleId"]),
                            RoleName = dr["RoleName"].ToString()!
                        });
                    }
                }
                return Ok(roles);
            }
        }

        [Authorize]
        [HttpGet("GetDefaultPageByRole/{roleId}")]
        public async Task<IActionResult> GetDefaultPageByRole(int roleId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT ScreenName FROM Screen WHERE RoleId = @RoleId", conn);
                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                        return NotFound("No screen found for this role.");
                    string screenName = result.ToString();
                    return Ok(new ScreenDto { ScreenName = result.ToString()! });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to fetch screen name.", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("GetRoleSettings/{roleId}")]
        public async Task<IActionResult> GetRoleSettings(int roleId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    var cmd = new SqlCommand("SELECT * FROM RoleSettings WHERE RoleId = @RoleId ORDER BY Priority, ScreenName", conn);
                    cmd.Parameters.AddWithValue("@RoleId", roleId);

                    await conn.OpenAsync();

                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    var roleSettings = new List<RoleSettingsDto>();

                    while (await reader.ReadAsync())
                    {
                        roleSettings.Add(new RoleSettingsDto
                        {
                            RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                            ScreenName = reader.GetString(reader.GetOrdinal("ScreenName")),
                            AdditionPrivileges = reader.GetBoolean(reader.GetOrdinal("AdditionPrivileges")),
                            EditPrivileges = reader.GetBoolean(reader.GetOrdinal("EditPrivileges")),
                            DeletePrivileges = reader.GetBoolean(reader.GetOrdinal("DeletePrivileges")),
                            ViewPrivileges = reader.GetBoolean(reader.GetOrdinal("ViewPrivileges")),
                            Priority = reader.IsDBNull(reader.GetOrdinal("Priority"))
                                ? (int?)null
                                : reader.GetInt32(reader.GetOrdinal("Priority"))
                        });
                    }

                    return Ok(roleSettings);
                }
                
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to get role settings", Error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("UpdateRoleSettings")]
        public async Task<IActionResult> UpdateRoleSettings([FromBody] RoleSettingsUpdateRequest request)
        {
            if (request == null || request.RoleId <= 0)
                return BadRequest("Invalid role data.");

            string connectionString = _configuration.GetConnectionString("conn");

            try
            {
                using SqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                // 1️⃣  Check if role already has a screen entry
                int existingCount = 0;
                using (SqlCommand checkCmd = new("SELECT COUNT(*) FROM Screen WHERE RoleId = @RoleId", conn))
                {
                    checkCmd.Parameters.AddWithValue("@RoleId", request.RoleId);
                    existingCount = (int)await checkCmd.ExecuteScalarAsync();
                }

                // 2️⃣  Insert or Update the Screen table
                string screenQuery = existingCount == 0
                    ? "INSERT INTO Screen (ScreenName, RoleId) VALUES (@ScreenName, @RoleId)"
                    : "UPDATE Screen SET ScreenName = @ScreenName WHERE RoleId = @RoleId";

                using (SqlCommand cmd = new(screenQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@ScreenName", request.DefaultPage);
                    cmd.Parameters.AddWithValue("@RoleId", request.RoleId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 3️⃣  Loop through each role setting and update
                foreach (var item in request.Items)
                {
                    using SqlCommand updateCmd = new(
                        @"UPDATE RoleSettings 
                      SET AdditionPrivileges = @Add,
                          EditPrivileges = @Edit,
                          DeletePrivileges = @Delete,
                          ViewPrivileges = @View
                      WHERE ScreenName = @ScreenName AND RoleId = @RoleId", conn);

                    updateCmd.Parameters.AddWithValue("@Add", item.AdditionPrivileges);
                    updateCmd.Parameters.AddWithValue("@Edit", item.EditPrivileges);
                    updateCmd.Parameters.AddWithValue("@Delete", item.DeletePrivileges);
                    updateCmd.Parameters.AddWithValue("@View", item.ViewPrivileges);
                    updateCmd.Parameters.AddWithValue("@ScreenName", item.ScreenName);
                    updateCmd.Parameters.AddWithValue("@RoleId", request.RoleId);

                    await updateCmd.ExecuteNonQueryAsync();
                }

                return Ok(new { Message = "Role settings updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to update role settings", Error = ex.Message });
            }
        }

        #endregion

        #region User Creation APIs

        [Authorize]
        [HttpGet("GetEmployees/{companyId}")]
        public IActionResult GetEmployees(int companyId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var query = @"
            SELECT EmployeeId,
                   (EmployeeName + ' ( ' + BranchName + ' ) ') AS EmployeeName
            FROM vw_Employee
            WHERE CompanyId = @CompanyId
              AND status = 'working'
              AND (EmployeeId NOT IN (SELECT EmployeeId FROM Users))
            ORDER BY EmployeeName";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@CompanyId", companyId);

                var employees = new List<EmployeeDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        employees.Add(new EmployeeDto
                        {
                            EmployeeId = Convert.ToInt32(dr["EmployeeId"]),
                            EmployeeName = dr["EmployeeName"].ToString()!
                        });
                    }
                }
                return Ok(employees);
            }
        }

        [Authorize]
        [HttpGet("GetAvailableEmployees/{employeeName}")]
        public IActionResult GetAvailableEmployees(string employeeName)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT EmployeeId, EmployeeName
            FROM Employee
            WHERE (@EmployeeName IS NULL OR EmployeeName = @EmployeeName)
               OR EmployeeId NOT IN (SELECT EmployeeId FROM [Users])
            ORDER BY EmployeeName";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@EmployeeName", (object?)employeeName ?? DBNull.Value);

                var employees = new List<EmployeeDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        employees.Add(new EmployeeDto
                        {
                            EmployeeId = Convert.ToInt32(dr["EmployeeId"]),
                            EmployeeName = dr["EmployeeName"].ToString()!
                        });
                    }
                }

                return Ok(employees);
            }
        }


        [Authorize]
        [HttpGet("GetUsers/{companyId}")]
        public IActionResult GetUsers(int companyId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT * 
            FROM vw_UserCreation
            WHERE CompanyId = @CompanyId
              AND RoleName != 'DBAdmin'";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);

                    var users = new List<UserDto>();
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            users.Add(new UserDto
                            {
                                UserId = Convert.ToInt32(dr["UsersId"]),
                                EmployeeId = Convert.ToInt32(dr["EmployeeId"]),
                                EmployeeName = dr["EmployeeName"].ToString()!,
                                UserName = dr["UserName"].ToString()!,
                                RoleId = Convert.ToInt32(dr["RoleId"]),
                                RoleName = dr["RoleName"].ToString()!,
                                Password = dr["Password"].ToString()!,
                            });
                        }
                    }

                    return Ok(users);
                }
            }
        }

        [Authorize]
        [HttpPost("CreateUser")]
        public IActionResult CreateUser([FromBody] UserDto dto)
        {
            if (dto == null)
                return BadRequest(new { Message = "Invalid user data." });

            if (dto.Password != dto.ConfirmPassword)
                return BadRequest(new { Message = "Password and confirm password do not match." });

            try
            {
                using var conn = GetConnection();
                conn.Open();

                // 🔹 Step 1: Insert user via stored procedure
                using (var cmd = new SqlCommand("sp_Users_Insert", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@EmployeeId", dto.EmployeeId);
                    cmd.Parameters.AddWithValue("@UserName", dto.UserName);
                    cmd.Parameters.AddWithValue("@Password", dto.Password);
                    cmd.Parameters.AddWithValue("@RoleId", dto.RoleId);
                    cmd.ExecuteNonQuery();
                }

                // 🔹 Step 2: Check whether role has screen configuration
                int roleCount = CheckRoleExists(dto.RoleId, conn);

                // 🔹 Step 3: Fetch the newly created user's ID
                int userId = GetUserId(dto.RoleId, dto.EmployeeId, conn);

                // 🔹 Step 4: Insert user settings based on role
                if (roleCount == 0)
                {
                    // Copy permissions from DBAdmin
                    var ds = GetScreens(
                        "SELECT * FROM UserSettings WHERE UsersId = (SELECT UsersId FROM vw_UserRoles WHERE RoleName = 'DBAdmin')",
                        conn
                    );

                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        InsertUserSettings(userId, dr["ScreenName"].ToString(),
                            add: false, edit: false, delete: false, view: false, priority: 0, conn);
                    }
                }
                else
                {
                    // Copy permissions from RoleSettings
                    var ds = GetScreens($"SELECT * FROM RoleSettings WHERE RoleId = {dto.RoleId}", conn);

                    foreach (DataRow dr in ds.Tables[0].Rows)
                    {
                        InsertUserSettings(userId,
                            dr["ScreenName"].ToString(),
                            Convert.ToBoolean(dr["AdditionPrivileges"]),
                            Convert.ToBoolean(dr["EditPrivileges"]),
                            Convert.ToBoolean(dr["DeletePrivileges"]),
                            Convert.ToBoolean(dr["ViewPrivileges"]),
                            Convert.ToInt32(dr["Priority"]),
                            conn);
                    }
                }

                return Ok(new { Message = "User created successfully." });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UK_Users_UserName"))
                    return Conflict(new { Message = $"User name '{dto.UserName}' already exists." });

                return BadRequest(new { Message = $"SQL error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Unexpected error: {ex.Message}" });
            }
        }

        // ======================================
        //  Helper: CheckRoleExists
        // ======================================
        private int CheckRoleExists(int roleId, SqlConnection conn)
        {
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM Screen WHERE RoleId = @RoleId", conn);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // ======================================
        //  Helper: GetUserId
        // ======================================
        private int GetUserId(int roleId, int employeeId, SqlConnection conn)
        {
            using var cmd = new SqlCommand("SELECT UsersId FROM [Users] WHERE RoleId=@RoleId AND EmployeeId=@EmployeeId", conn);
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : -1;
        }

        // ======================================
        //  Helper: GetScreens
        // ======================================
        private DataSet GetScreens(string query, SqlConnection conn)
        {
            using var cmd = new SqlCommand(query, conn);
            using var adapter = new SqlDataAdapter(cmd);
            var ds = new DataSet();
            adapter.Fill(ds);
            return ds;
        }

        // ======================================
        //  Helper: InsertUserSettings
        // ======================================
        private void InsertUserSettings(int userId, string screenName, bool add, bool edit, bool delete, bool view, int priority, SqlConnection conn)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO UserSettings (UsersId, ScreenName, AdditionPrivileges, EditPrivileges, DeletePrivileges, ViewPrivileges, Priority)
                VALUES (@UserId, @ScreenName, @Add, @Edit, @Delete, @View, @Priority)", conn);

            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ScreenName", screenName);
            cmd.Parameters.AddWithValue("@Add", add);
            cmd.Parameters.AddWithValue("@Edit", edit);
            cmd.Parameters.AddWithValue("@Delete", delete);
            cmd.Parameters.AddWithValue("@View", view);
            cmd.Parameters.AddWithValue("@Priority", priority);

            cmd.ExecuteNonQuery();
        }

        [Authorize]
        [HttpPut("UpdateUser")]
        public IActionResult UpdateUser([FromBody] UserDto user)
        {
            if (user == null)
                return BadRequest("Invalid request data.");

            try
            {
                using (var conn = GetConnection())
                {
                    using (var cmd = new SqlCommand("sp_Users_Update", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@UsersId", user.UserId);
                        cmd.Parameters.AddWithValue("@UserName", user.UserName);
                        cmd.Parameters.AddWithValue("@EmployeeId", user.EmployeeId);
                        cmd.Parameters.AddWithValue("@Password", user.Password);
                        cmd.Parameters.AddWithValue("@RoleId", user.RoleId);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = "User updated successfully."
                });
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("Violation of UNIQUE KEY constraint 'UK_Users_UserName'"))
                {
                    return Conflict(new
                    {
                        success = false,
                        message = $"User name '{user.UserName}' already exists."
                    });
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to update the record.",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpDelete("DeleteUser/{userId}")]
        public IActionResult DeleteUser(int userId)
        {
            using var conn = GetConnection();
            conn.Open();

            SqlTransaction transaction = conn.BeginTransaction();

            try
            {
                // Step 1: Delete from UserSettings first
                using (var cmd = new SqlCommand("DELETE FROM UserSettings WHERE UsersId = @UserId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.ExecuteNonQuery();
                }

                // Step 2: Delete from Users table
                using (var cmd = new SqlCommand("DELETE FROM [Users] WHERE UsersId = @UserId", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return Ok(new { Message = "User deleted successfully." });
            }
            catch (SqlException ex)
            {
                transaction.Rollback();

                // SQL Error 547 = Foreign key constraint violation (data in use)
                if (ex.Number == 547)
                    return BadRequest(new { Message = "Data in use — cannot delete this record." });

                return BadRequest(new { Message = $"SQL Error: {ex.Message}" });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return BadRequest(new { Message = $"Unexpected error: {ex.Message}" });
            }
        }




        #endregion

        #region User Settings APIs

        [Authorize]
        [HttpGet("GetUsersForSettings/{companyId}")]
        public IActionResult GetUsersForSettings(int companyId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var query = @"
            SELECT UsersId, UserName
            FROM vw_UserCreation
            WHERE CompanyId = @CompanyId
              AND RoleId <> (SELECT RoleId FROM Role WHERE RoleName = 'DBAdmin')
            ORDER BY UserName";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@CompanyId", companyId);

                var users = new List<UserDto>();
                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        users.Add(new UserDto
                        {
                            UserId = Convert.ToInt32(dr["UsersId"]),
                            UserName = dr["UserName"].ToString()!
                        });
                    }
                }

                return Ok(users);
            }
        }

        [Authorize]
        [HttpGet("GetUserSettings/{userId}")]
        public async Task<IActionResult> GetUserSettings(int userId)
        {
            var userSettings = new List<UserSettingsDto>();

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Step 1: Check if user has any user settings
                    int screenCount = 0;
                    using (SqlCommand cmdCount = new SqlCommand(
                        "SELECT COUNT(*) FROM UserSettings WHERE UsersId = @UserId", conn))
                    {
                        cmdCount.Parameters.AddWithValue("@UserId", userId);
                        screenCount = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());
                    }

                    // Step 2: Build query
                    string query = screenCount > 0
                        ? @"SELECT UsersId, ScreenName, AdditionPrivileges, EditPrivileges, 
                                  DeletePrivileges, ViewPrivileges, Priority 
                             FROM UserSettings 
                             WHERE UsersId = @UserId 
                             ORDER BY Priority, ScreenName"
                        : @"SELECT UsersId, ScreenName, AdditionPrivileges, EditPrivileges, 
                                  DeletePrivileges, ViewPrivileges, Priority 
                             FROM UserSettings 
                             WHERE UsersId = (SELECT UsersId FROM vw_UserRoles WHERE RoleName = 'DBAdmin') 
                             ORDER BY Priority, ScreenName";

                    // Step 3: Fetch data
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (screenCount > 0)
                            cmd.Parameters.AddWithValue("@UserId", userId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                userSettings.Add(new UserSettingsDto
                                {
                                    UsersId = Convert.ToInt32(reader["UsersId"]),
                                    ScreenName = reader["ScreenName"]?.ToString() ?? "",
                                    AdditionPrivileges = reader["AdditionPrivileges"] != DBNull.Value && (bool)reader["AdditionPrivileges"],
                                    EditPrivileges = reader["EditPrivileges"] != DBNull.Value && (bool)reader["EditPrivileges"],
                                    DeletePrivileges = reader["DeletePrivileges"] != DBNull.Value && (bool)reader["DeletePrivileges"],
                                    ViewPrivileges = reader["ViewPrivileges"] != DBNull.Value && (bool)reader["ViewPrivileges"],
                                    Priority = reader["Priority"] != DBNull.Value ? Convert.ToInt32(reader["Priority"]) : 0
                                });
                            }
                        }
                    }
                }

                return Ok(userSettings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        [Authorize]
        [HttpPut("UpdateUserSettings")]
        public async Task<IActionResult> UpdateUserSettings([FromBody] UserSettingsRequest request)
        {
            if (request == null || request.UserId <= 0)
                return BadRequest("Invalid request.");

            try
            {
                using (var conn = GetConnection())
                {
                    await conn.OpenAsync();

                    // Step 1: Check if user already has user settings
                    int screenCount = 0;
                    using (SqlCommand cmdCount = new SqlCommand(
                        "SELECT COUNT(*) FROM UserSettings WHERE UsersId = @UserId", conn))
                    {
                        cmdCount.Parameters.AddWithValue("@UserId", request.UserId);
                        screenCount = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());
                    }

                    // Step 2: Update or Insert based on presence
                    foreach (var item in request.Items)
                    {
                        if (screenCount > 0)
                        {
                            // Update existing settings
                            using (SqlCommand cmdUpdate = new SqlCommand(@"
                                UPDATE UserSettings
                                SET AdditionPrivileges = @Add,
                                    EditPrivileges = @Edit,
                                    DeletePrivileges = @Delete,
                                    ViewPrivileges = @View
                                WHERE ScreenName = @ScreenName AND UsersId = @UserId", conn))
                            {
                                cmdUpdate.Parameters.AddWithValue("@UserId", request.UserId);
                                cmdUpdate.Parameters.AddWithValue("@ScreenName", item.ScreenName ?? "");
                                cmdUpdate.Parameters.AddWithValue("@Add", item.AdditionPrivileges);
                                cmdUpdate.Parameters.AddWithValue("@Edit", item.EditPrivileges);
                                cmdUpdate.Parameters.AddWithValue("@Delete", item.DeletePrivileges);
                                cmdUpdate.Parameters.AddWithValue("@View", item.ViewPrivileges);
                                await cmdUpdate.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            // Insert new settings (with priority)
                            int priority = await GetPriorityAsync(conn, item.ScreenName);

                            using (SqlCommand cmdInsert = new SqlCommand(@"
                                INSERT INTO UserSettings 
                                (UsersId, ScreenName, AdditionPrivileges, EditPrivileges, DeletePrivileges, ViewPrivileges, Priority)
                                VALUES (@UserId, @ScreenName, @Add, @Edit, @Delete, @View, @Priority)", conn))
                            {
                                cmdInsert.Parameters.AddWithValue("@UserId", request.UserId);
                                cmdInsert.Parameters.AddWithValue("@ScreenName", item.ScreenName ?? "");
                                cmdInsert.Parameters.AddWithValue("@Add", item.AdditionPrivileges);
                                cmdInsert.Parameters.AddWithValue("@Edit", item.EditPrivileges);
                                cmdInsert.Parameters.AddWithValue("@Delete", item.DeletePrivileges);
                                cmdInsert.Parameters.AddWithValue("@View", item.ViewPrivileges);
                                cmdInsert.Parameters.AddWithValue("@Priority", priority);
                                await cmdInsert.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                return Ok(new { Message = "User settings updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        private async Task<int> GetPriorityAsync(SqlConnection conn, string screenName)
        {
            using (SqlCommand cmd = new SqlCommand(
                "SELECT TOP 1 Priority FROM RoleSettings WHERE ScreenName = @ScreenName", conn))
            {
                cmd.Parameters.AddWithValue("@ScreenName", screenName ?? "");
                object result = await cmd.ExecuteScalarAsync();
                return result == DBNull.Value || result == null ? 0 : Convert.ToInt32(result);
            }
        }

        #endregion

        #region Message Board APIs

        [Authorize]
        [HttpGet("GetMessages")]
        public async Task<IActionResult> GetMessages()
        {
            var messages = new List<MessageDTO>();

            try
            {
                using (var conn = GetConnection())
                {
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand("sp_Message_Select", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var message = new MessageDTO
                                {
                                    MessageId = reader["MessageId"] != DBNull.Value ? Convert.ToInt32(reader["MessageId"]) : 0,
                                    Message = reader["Message"]?.ToString() ?? string.Empty,
                                    DateTime = reader["DateTime"] != DBNull.Value
                                        ? Convert.ToDateTime(reader["DateTime"])
                                        : DateTime.MinValue
                                };

                                messages.Add(message);
                            }
                        }
                    }
                }

                return Ok(messages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to load messages: {ex.Message}");
            }
        }

        [Authorize]
        [HttpPost("AddMessage")]
        public async Task<IActionResult> AddMessage([FromBody] MessageDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message text cannot be empty.");

            try
            {
                using (var conn = GetConnection())
                {
                    await conn.OpenAsync();

                    // Convert UTC to IST (UTC +5:30)
                    DateTime istTime = DateTime.UtcNow.AddHours(5.5);

                    using (var cmd = new SqlCommand("INSERT INTO Message (Message, DateTime) VALUES (@Message, @DateTime)", conn))
                    {
                        cmd.Parameters.AddWithValue("@Message", request.Message);
                        cmd.Parameters.AddWithValue("@DateTime", istTime);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new { Message = "Record inserted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to insert the record: {ex.Message}");
            }
        }

        [Authorize]
        [HttpPut("UpdateMessage/{messageId}")]
        public async Task<IActionResult> UpdateMessage(int messageId, [FromBody] MessageDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message text cannot be empty.");

            try
            {
                using (var conn = GetConnection())
                {
                    await conn.OpenAsync();

                    // Convert UTC → IST (UTC + 5:30)
                    DateTime istTime = DateTime.UtcNow.AddHours(5.5);

                    using (var cmd = new SqlCommand(
                        "UPDATE Message SET Message = @Message, DateTime = @DateTime WHERE MessageId = @MessageId", conn))
                    {
                        cmd.Parameters.AddWithValue("@MessageId", messageId);
                        cmd.Parameters.AddWithValue("@Message", request.Message);
                        cmd.Parameters.AddWithValue("@DateTime", istTime);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                            return NotFound($"No message found with ID {messageId}.");
                    }
                }

                return Ok(new { Message = "Record updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to update the record: {ex.Message}");
            }
        }

        [Authorize]
        [HttpDelete("DeleteMessage/{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand("DELETE FROM Message WHERE MessageId = @MessageId", conn))
                    {
                        cmd.Parameters.AddWithValue("@MessageId", messageId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                            return NotFound($"No message found with ID {messageId}.");
                    }
                }

                return Ok(new { Message = "Record deleted successfully." });
            }
            catch (SqlException ex)
            {
                // If delete fails due to foreign key constraint or data in use
                return StatusCode(500, $"Data in use — can't delete the record. SQL error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to delete the record: {ex.Message}");
            }
        }


        #endregion

        #region Adjustment Days APIs

        [Authorize]
        [HttpGet("GetAdjustmentDays")]
        public IActionResult GetAdjustmentDays()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Days FROM AdjustmentDays", conn);
                var result = cmd.ExecuteScalar();

                if (result != null)
                {
                    return Ok(Convert.ToInt32(result));
                }

                return NotFound("No adjustment days record found.");
            }
        }

        [Authorize]
        [HttpGet("GetOldDataLimit")]
        public IActionResult GetOldDataLimit()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Days FROM OldDataDays", conn);
                var result = cmd.ExecuteScalar();

                if (result != null)
                {
                    return Ok(Convert.ToInt32(result));
                }

                return NotFound("No old data days record found.");
            }
        }

        [Authorize]
        [HttpPost("UpdateAdjustmentDays")]
        public IActionResult UpdateAdjustmentDays([FromBody] int days)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("UPDATE AdjustmentDays SET Days = @Days", conn);
                cmd.Parameters.AddWithValue("@Days", days);

                var rows = cmd.ExecuteNonQuery();
                if (rows > 0)
                    return Ok("Adjustment Days updated successfully.");

                return BadRequest("Failed to update Adjustment Days.");
            }
        }

        [Authorize]
        [HttpPost("UpdateOldDataLimit")]
        public IActionResult UpdateOldDataLimit([FromBody] int days)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("UPDATE OldDataDays SET Days = @Days", conn);
                cmd.Parameters.AddWithValue("@Days", days);

                var rows = cmd.ExecuteNonQuery();
                if (rows > 0)
                    return Ok("Old Data Limit updated successfully.");

                return BadRequest("Failed to update Old Data Limit.");
            }
        }


        #endregion

        #region SMS APIs

        [Authorize]
        [HttpGet("GetCompanies/{companyId:int}")]
        public IActionResult GetCompanies(int companyId)
        {
            var companies = new List<CompanyDto>();

            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT CompanyId, CompanyName FROM Company WHERE CompanyId = @CompanyId", conn);
                cmd.Parameters.AddWithValue("@CompanyId", companyId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        companies.Add(new CompanyDto
                        {
                            CompanyId = reader.GetInt32(reader.GetOrdinal("CompanyId")),
                            CompanyName = reader.GetString(reader.GetOrdinal("CompanyName"))
                        });
                    }
                }
            }

            if (companies.Count == 0)
                return NotFound($"No company found for ID {companyId}.");

            return Ok(companies);
        }

        [Authorize]
        [HttpGet("GetBranches/{companyId:int}")]
        public IActionResult GetBranches(int companyId)
        {
            var branches = new List<BranchDto>();

            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT BranchId, BranchName, CompanyId FROM Branch WHERE CompanyId = @CompanyId", conn);
                cmd.Parameters.AddWithValue("@CompanyId", companyId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        branches.Add(new BranchDto
                        {
                            BranchId = reader.GetInt32(reader.GetOrdinal("BranchId")),
                            BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                            CompanyId = reader.GetInt32(reader.GetOrdinal("CompanyId"))
                        });
                    }
                }
            }

            if (branches.Count == 0)
                return NotFound($"No branches found for CompanyId {companyId}.");

            return Ok(branches);
        }

        [Authorize]
        [HttpGet("GetMessageReceivers")]
        public IActionResult GetMessageReceivers(string type, int companyId, int branchId)
        {
            var receivers = new List<MessageReceiverDto>();

            using (var conn = GetConnection())
            {
                conn.Open();

                string query;
                if (type.Equals("Employers", StringComparison.OrdinalIgnoreCase))
                {
                    query = @"
                SELECT DISTINCT PhoneNumber AS PhoneNumber,
                                (SurName + ' ' + EmployeeName) AS ReceiverName
                FROM Employee
                WHERE CompanyId = @CompanyId
                  AND LEN(PhoneNumber) = 10
                  AND Status = 'Working'";

                    // Filter by branch if not 'All'
                    if (branchId != 0)
                        query += " AND BranchId = @BranchId";
                }
                else if (type.Equals("Customers", StringComparison.OrdinalIgnoreCase))
                {
                    query = @"
                SELECT DISTINCT MobileNumber AS PhoneNumber,
                                (SurName + ' ' + [Name]) AS ReceiverName
                FROM HpEntry
                WHERE CompanyId = @CompanyId
                  AND LEN(MobileNumber) = 10";

                    if (branchId != 0)
                        query += " AND BranchId = @BranchId";
                }
                else
                {
                    return BadRequest("Invalid message type. Must be 'Employers' or 'Customers'.");
                }

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    if (branchId != 0)
                        cmd.Parameters.AddWithValue("@BranchId", branchId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            receivers.Add(new MessageReceiverDto
                            {
                                PhoneNumber = reader["PhoneNumber"].ToString(),
                                ReceiverName = reader["ReceiverName"].ToString()
                            });
                        }
                    }
                }
            }

            if (receivers.Count == 0)
                return NotFound("No receivers found for the selected type and filters.");

            return Ok(receivers);
        }

        [Authorize]
        [HttpPost("QueueSms")]
        public async Task<IActionResult> QueueSms([FromBody] SmsRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message cannot be empty.");

            if (request.Recipients == null || request.Recipients.Count == 0)
                return BadRequest("No recipients provided.");

            int receiverCount = request.Recipients.Count;

            using var conn = GetConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                var presentDate = DateTime.UtcNow.AddHours(5.5); // same as original WebForms
                var dateStr = presentDate.ToShortDateString();
                var timeStr = presentDate.ToShortTimeString();

                foreach (var r in request.Recipients)
                {
                    using var cmd = new SqlCommand(@"
                        INSERT INTO SMS
                            (MessageType, Receiver, PhoneNumber, Message, Status, [Date], UserName, [Time], CompanyId, BranchId)
                        VALUES
                            (@Type, @Receiver, @PhoneNumber, @Message, 'UnDelivered', @Date, @UserName, @Time, @CompanyId, @BranchId)",
                        conn, tran);

                    cmd.Parameters.AddWithValue("@Type", request.MessageType ?? "Promo");
                    cmd.Parameters.AddWithValue("@Receiver", r.ReceiverName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@PhoneNumber", r.PhoneNumber ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Message", request.Message ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Date", dateStr);
                    cmd.Parameters.AddWithValue("@UserName", request.UserName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Time", timeStr);
                    cmd.Parameters.AddWithValue("@CompanyId", request.CompanyId);
                    if (request.BranchId != 0)
                        cmd.Parameters.AddWithValue("@BranchId", request.BranchId);
                    else
                        cmd.Parameters.AddWithValue("@BranchId", DBNull.Value);

                    cmd.ExecuteNonQuery();
                }

                tran.Commit();

                await _smsService.SendSMSAdminCustomAsync(receiverCount);

                return Ok(new { message = $"{request.Recipients.Count} SMS queued successfully." });
            }
            catch (Exception ex)
            {
                tran.Rollback();
                return StatusCode(500, $"Error inserting SMS records: {ex.Message}");
            }
        }

        [Authorize]
        [HttpGet("GetUndeliveredSMS")]
        public IActionResult GetUndeliveredSMS(int companyId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Parameterized SQL for safety
                var query = @"SELECT SMSId, MessageType, Receiver, PhoneNumber, Message, Status, Date, Time, CompanyId, BranchId, UserName
                      FROM SMS
                      WHERE Status = 'Undelivered' AND CompanyId = @CompanyId
                      ORDER BY SMSId DESC";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        var smsList = new List<SmsDto>();

                        while (reader.Read())
                        {
                            smsList.Add(new SmsDto
                            {
                                SMSId = reader.GetInt32(reader.GetOrdinal("SMSId")),
                                MessageType = reader["MessageType"].ToString(),
                                Receiver = reader["Receiver"].ToString(),
                                PhoneNumber = reader["PhoneNumber"].ToString(),
                                Message = reader["Message"].ToString(),
                                Status = reader["Status"].ToString(),
                                Date = reader["Date"].ToString(),
                                Time = reader["Time"].ToString(),
                                CompanyId = reader.GetInt32(reader.GetOrdinal("CompanyId")),
                                BranchId = reader["BranchId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BranchId"]),
                                UserName = reader["UserName"].ToString()
                            });
                        }

                        return Ok(smsList);
                    }
                }
            }
        }

        [Authorize]
        [HttpPost("ResendSmsById/{smsId}")]
        public IActionResult ResendSmsById(int smsId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                // Step 1: Get SMS record by ID
                var cmd = new SqlCommand("SELECT * FROM SMS WHERE SMSId = @SMSId", conn);
                cmd.Parameters.AddWithValue("@SMSId", smsId);

                SmsDto sms = null!;
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        sms = new SmsDto
                        {
                            SMSId = Convert.ToInt32(dr["SMSId"]),
                            PhoneNumber = dr["PhoneNumber"].ToString()!,
                            Message = dr["Message"].ToString()!,
                            Status = dr["Status"].ToString()!,
                            Receiver = dr["Receiver"].ToString()!
                        };
                    }
                }

                if (sms == null)
                {
                    return NotFound($"SMS with ID {smsId} not found.");
                }

                // Step 2: Send SMS via BusinessSMS gateway
                string strMobileNo = "91" + sms.PhoneNumber;
                string strTextMsg = sms.Message;
                string strGatewayResponse = "";

                try
                {
                    if (strMobileNo.Length == 12)
                    {
                        if (strTextMsg.Length <= 160)
                        {
                            strGatewayResponse = _smsService.SendSMSUsingBS(strMobileNo,strTextMsg,"");
                        }
                        else
                        {
                            int msgLength = strTextMsg.Length;
                            int msgCount = (int)Math.Ceiling(msgLength / 160.0);
                            for (int index = 0; index < msgCount; index++)
                            {
                                string partMsg = (index + 1 == msgCount)
                                    ? strTextMsg.Substring(index * 160, msgLength - index * 160)
                                    : strTextMsg.Substring(index * 160, 160);

                                strGatewayResponse =  _smsService.SendSMSUsingBS(strMobileNo,partMsg,"");
                            }
                        }

                        // Step 3: Update status if successful
                        if (strGatewayResponse.Contains("Message Submitted", StringComparison.OrdinalIgnoreCase))
                        {
                            var updateCmd = new SqlCommand("UPDATE SMS SET Status = 'Delivered' WHERE SMSId = @SMSId", conn);
                            updateCmd.Parameters.AddWithValue("@SMSId", smsId);
                            updateCmd.ExecuteNonQuery();
                        }
                        else
                        {
                            var updateCmd = new SqlCommand("UPDATE SMS SET Status = 'UnSent' WHERE SMSId = @SMSId", conn);
                            updateCmd.Parameters.AddWithValue("@SMSId", smsId);
                            updateCmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        var updateCmd = new SqlCommand("UPDATE SMS SET Status = 'UnSent' WHERE SMSId = @SMSId", conn);
                        updateCmd.Parameters.AddWithValue("@SMSId", smsId);
                        updateCmd.ExecuteNonQuery();
                    }

                    return Ok(new { Message = "Message resent successfully", GatewayResponse = strGatewayResponse });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { Message = "Failed to resend the message", Error = ex.Message });
                }
            }
        }

        [Authorize]
        [HttpPost("ResendMultipleSms")]
        public IActionResult ResendMultipleSms([FromBody] List<int> smsIds)
        {
            if (smsIds == null || smsIds.Count == 0)
                return BadRequest("No SMS IDs provided.");

            var results = new List<object>();

            using (var conn = GetConnection())
            {
                conn.Open();

                foreach (var smsId in smsIds)
                {
                    try
                    {
                        // 1️ Fetch SMS record
                        var cmd = new SqlCommand("SELECT * FROM SMS WHERE SMSId = @SMSId", conn);
                        cmd.Parameters.AddWithValue("@SMSId", smsId);

                        SmsDto? sms = null;
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                sms = new SmsDto
                                {
                                    SMSId = Convert.ToInt32(dr["SMSId"]),
                                    PhoneNumber = dr["PhoneNumber"].ToString()!,
                                    Message = dr["Message"].ToString()!,
                                    Receiver = dr["Receiver"].ToString()!
                                };
                            }
                        }

                        if (sms == null)
                        {
                            results.Add(new { SmsId = smsId, Status = "NotFound" });
                            continue;
                        }

                        // 2️ Send SMS
                        string mobileNo = "91" + sms.PhoneNumber;
                        string messageText = sms.Message;
                        string responseText = "";

                        if (mobileNo.Length == 12)
                        {
                            if (messageText.Length <= 160)
                            {
                                responseText = _smsService.SendSMSUsingBS(mobileNo,messageText,"");
                            }
                            else
                            {
                                int msgLength = messageText.Length;
                                int msgCount = (int)Math.Ceiling(msgLength / 160.0);
                                for (int index = 0; index < msgCount; index++)
                                {
                                    string shortPart = (index + 1 == msgCount)
                                        ? messageText.Substring(index * 160, msgLength - index * 160)
                                        : messageText.Substring(index * 160, 160);

                                    responseText = _smsService.SendSMSUsingBS(mobileNo,shortPart,"");
                                }
                            }

                            // 3️ Update delivery status
                            string newStatus = responseText.Contains("Message Submitted", StringComparison.OrdinalIgnoreCase)
                                ? "Delivered"
                                : "UnSent";

                            var updateCmd = new SqlCommand("UPDATE SMS SET Status = @Status WHERE SMSId = @SMSId", conn);
                            updateCmd.Parameters.AddWithValue("@Status", newStatus);
                            updateCmd.Parameters.AddWithValue("@SMSId", smsId);
                            updateCmd.ExecuteNonQuery();

                            results.Add(new
                            {
                                SmsId = smsId,
                                PhoneNumber = sms.PhoneNumber,
                                Receiver = sms.Receiver,
                                GatewayResponse = responseText,
                                Status = newStatus
                            });
                        }
                        else
                        {
                            var updateCmd = new SqlCommand("UPDATE SMS SET Status = 'UnSent' WHERE SMSId = @SMSId", conn);
                            updateCmd.Parameters.AddWithValue("@SMSId", smsId);
                            updateCmd.ExecuteNonQuery();
                            results.Add(new { SmsId = smsId, Status = "InvalidPhoneNumber" });
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { SmsId = smsId, Status = "Error", Error = ex.Message });
                    }
                }
            }

            return Ok(new { Message = "Resend operation completed.", Results = results });
        }

        [Authorize]
        [HttpPost("DeleteMultipleSms")]
        public IActionResult DeleteMultipleSms([FromBody] List<int> smsIds)
        {
            if (smsIds == null || smsIds.Count == 0)
                return BadRequest("No SMS IDs provided.");

            var deletedResults = new List<object>();

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var smsId in smsIds)
                        {
                            try
                            {
                                // 1️ Fetch record from SMS table
                                SmsDto? sms = null;
                                using (var fetchCmd = new SqlCommand("SELECT * FROM SMS WHERE SMSId = @SMSId", conn, tran))
                                {
                                    fetchCmd.Parameters.AddWithValue("@SMSId", smsId);
                                    using (var dr = fetchCmd.ExecuteReader())
                                    {
                                        if (dr.Read())
                                        {
                                            sms = new SmsDto
                                            {
                                                SMSId = Convert.ToInt32(dr["SMSId"]),
                                                MessageType = dr["MessageType"].ToString(),
                                                Receiver = dr["Receiver"].ToString(),
                                                PhoneNumber = dr["PhoneNumber"].ToString(),
                                                Message = dr["Message"].ToString(),
                                                Status = dr["Status"].ToString(),
                                                Date = Convert.ToDateTime(dr["Date"]).ToString("yyyy-MM-dd HH:mm:ss"),
                                                UserName = dr["UserName"].ToString(),
                                                Time = dr["Time"].ToString(),
                                                CompanyId = Convert.ToInt32(dr["CompanyId"]),
                                                BranchId = dr["BranchId"] == DBNull.Value ? (int?)null : Convert.ToInt32(dr["BranchId"]),
                                                SenderId = dr["SenderId"] == DBNull.Value ? null : dr["SenderId"].ToString()
                                            };
                                        }
                                    }
                                }

                                if (sms == null)
                                {
                                    deletedResults.Add(new { SmsId = smsId, Status = "NotFound" });
                                    continue;
                                }

                                // 2️ Insert into SMS_DUMP (backup)
                                using (var insertCmd = new SqlCommand(@"
                                INSERT INTO SMS_DUMP 
                                ([SMSId], [Message Type], [Receiver], [PhoneNumber], [Message], [Status], [Date], [UserName], [Time], [CompanyId], [BranchId], [SenderId])
                                VALUES
                                (@SMSId, @MessageType, @Receiver, @PhoneNumber, @Message, @Status, @Date, @UserName, @Time, @CompanyId, @BranchId, @SenderId)", conn, tran))
                                {
                                    insertCmd.Parameters.AddWithValue("@SMSId", sms.SMSId);
                                    insertCmd.Parameters.AddWithValue("@MessageType", sms.MessageType ?? (object)DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@Receiver", sms.Receiver ?? (object)DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@PhoneNumber", sms.PhoneNumber ?? (object)DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@Message", sms.Message ?? (object)DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@Status", sms.Status ?? (object)DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@Date", sms.Date ?? (object)DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@UserName", sms.UserName ?? (object)DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@Time", sms.Time ?? (object)DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@CompanyId", sms.CompanyId);
                                    insertCmd.Parameters.AddWithValue("@BranchId", (object?)sms.BranchId ?? DBNull.Value);
                                    insertCmd.Parameters.AddWithValue("@SenderId", sms.SenderId ?? (object)DBNull.Value);

                                    insertCmd.ExecuteNonQuery();
                                }


                                // 3️ Delete from SMS
                                using (var deleteCmd = new SqlCommand("DELETE FROM SMS WHERE SMSId = @SMSId", conn, tran))
                                {
                                    deleteCmd.Parameters.AddWithValue("@SMSId", smsId);
                                    deleteCmd.ExecuteNonQuery();
                                }

                                deletedResults.Add(new { SmsId = smsId, Status = "Deleted" });
                            }
                            catch (Exception ex)
                            {
                                deletedResults.Add(new { SmsId = smsId, Status = "Error", Error = ex.Message });
                            }
                        }

                        tran.Commit(); // Commit after all successful deletions
                    }
                    catch (Exception ex)
                    {
                        tran.Rollback(); // Rollback if any fatal exception
                        return StatusCode(500, new { Message = "Failed to delete records", Error = ex.Message });
                    }
                }
            }

            return Ok(new
            {
                Message = "Delete operation completed.",
                Results = deletedResults
            });
        }

        #endregion

        #region SMS Settings APIs

        [Authorize]
        [HttpGet("GetBusinessSMS")]
        public IActionResult GetBusinessSMS()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM BusinessSMS", conn);
                var businessSMSList = new List<BusinessSMSDto>();

                using (var dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        businessSMSList.Add(new BusinessSMSDto
                        {
                            Id = Convert.ToInt32(dr["Id"]),
                            UserId = dr["UserId"].ToString()!,
                            Password = dr["Password"].ToString()!
                        });
                    }
                }

                return Ok(businessSMSList);
            }
        }

        [Authorize]
        [HttpPost("UpdateBusinessSMSSettings")]
        public IActionResult UpdateBusinessSMSSettings([FromBody] BusinessSMSDto request)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand(
                        "UPDATE BusinessSMS SET UserId = @UserId, Password = @Password WHERE Id = @Id",
                        conn);

                    cmd.Parameters.AddWithValue("@Id", request.Id);
                    cmd.Parameters.AddWithValue("@UserId", request.UserId);
                    cmd.Parameters.AddWithValue("@Password", request.Password);

                    cmd.ExecuteNonQuery();

                    return Ok(new { message = "SMS details updated successfully." });
                }
            }
            catch
            {
                return StatusCode(500);
            }
        }

        #endregion

        #region HP Charge Duration APIs

        [Authorize]
        [HttpGet("GetHPChargeDuration/{companyId}")]
        public IActionResult GetHPChargeDuration(int companyId)
        {
            try
            {

                using (var conn = GetConnection())
                {
                    conn.Open();
                    var cmd = new SqlCommand(
                        "SELECT HPChargeDurationId, FromDate, ToDate, HPChargeInstallments " +
                        "FROM HPChargeDuration " +
                        "WHERE CompanyId = @CompanyId",
                        conn);

                    cmd.Parameters.AddWithValue("@CompanyId", companyId);

                    var hpChargeDurations = new List<HPChargeDurationDto>();

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            hpChargeDurations.Add(new HPChargeDurationDto
                            {
                                HPChargeDurationId = Convert.ToInt32(dr["HPChargeDurationId"]),
                                FromDate = Convert.ToDateTime(dr["FromDate"]),
                                ToDate = Convert.ToDateTime(dr["ToDate"]),
                                HPChargeInstallments = Convert.ToInt32(dr["HPChargeInstallments"])
                            });
                        }
                    }

                    return Ok(hpChargeDurations);
                }
            }
            catch (Exception ex)
            {
                // Log the exception (use ILogger)
                return StatusCode(500, new { message = "Failed to bind HP Charge Duration data." });
            }
        }

        [Authorize]
        [HttpPost("CreateHPChargeDuration/{companyId}")]
        public IActionResult CreateHPChargeDuration(int companyId, [FromBody] HPChargeDurationRequest request)
        {
            try
            {
                // Manual validations
                if (request == null)
                {
                    return BadRequest(new { message = "Invalid request data." });
                }

                if (companyId <= 0)
                {
                    return BadRequest(new { message = "Company Id is required." });
                }

                if (request.HPChargeInstallments <= 0)
                {
                    return BadRequest(new { message = "Please enter HP Charge Inst's." });
                }

                // Parse dates
                DateTime fromDate;
                DateTime toDate;

                try
                {
                    fromDate = new DateTime(request.FromDateYear, request.FromDateMonth, request.FromDateDay);
                    toDate = new DateTime(request.ToDateYear, request.ToDateMonth, request.ToDateDay);
                }
                catch (FormatException)
                {
                    return BadRequest(new { message = "Not a valid date." });
                }

                // Validate ToDate >= FromDate
                if (DateTime.Compare(toDate, fromDate) < 0)
                {
                    return BadRequest(new { message = "To Date should be Equal or Greater than From date" });
                }

                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Check for overlapping date ranges
                    var checkCmd = new SqlCommand(
                        "SELECT FromDate, ToDate FROM HPChargeDuration WHERE CompanyId = @CompanyId",
                        conn);
                    checkCmd.Parameters.AddWithValue("@CompanyId", companyId);

                    int count = 0;
                    using (var dr = checkCmd.ExecuteReader())
                    {
                        var existingRanges = new List<(DateTime FromDate, DateTime ToDate)>();
                        while (dr.Read())
                        {
                            existingRanges.Add((
                                Convert.ToDateTime(dr["FromDate"]),
                                Convert.ToDateTime(dr["ToDate"])
                            ));
                        }
                        dr.Close();

                        // Check for overlaps
                        foreach (var range in existingRanges)
                        {
                            var overlapCmd = new SqlCommand(
                                "SELECT COUNT(*) FROM HPChargeDuration " +
                                "WHERE ((@FromDate BETWEEN FromDate AND ToDate) OR (@ToDate BETWEEN FromDate AND ToDate)) " +
                                "AND CompanyId = @CompanyId",
                                conn);
                            overlapCmd.Parameters.AddWithValue("@FromDate", fromDate);
                            overlapCmd.Parameters.AddWithValue("@ToDate", toDate);
                            overlapCmd.Parameters.AddWithValue("@CompanyId", companyId);

                            if (Convert.ToInt32(overlapCmd.ExecuteScalar()) > 0)
                            {
                                count++;
                                break;
                            }
                        }
                    }

                    // Insert if no overlap found
                    if (count == 0)
                    {
                        var insertCmd = new SqlCommand(
                            "INSERT INTO HPChargeDuration (FromDate, ToDate, HPChargeInstallments, CompanyId) " +
                            "VALUES (@FromDate, @ToDate, @HPChargeInstallments, @CompanyId)",
                            conn);

                        insertCmd.Parameters.AddWithValue("@FromDate", fromDate);
                        insertCmd.Parameters.AddWithValue("@ToDate", toDate);
                        insertCmd.Parameters.AddWithValue("@HPChargeInstallments", request.HPChargeInstallments);
                        insertCmd.Parameters.AddWithValue("@CompanyId", companyId);

                        insertCmd.ExecuteNonQuery();

                        return Ok(new { message = "HP Charge Inst's saved successfully." });
                    }
                    else
                    {
                        return BadRequest(new { message = "Already HP Charge Inst's entered for this duration." });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception here (use ILogger)
                return StatusCode(500, new { message = "Failed to insert the record." });
            }
        }

        [Authorize]
        [HttpPut("UpdateHPChargeDuration/{hpChargeDurationId}")]
        public IActionResult UpdateHPChargeDuration(int hpChargeDurationId, int companyId, [FromBody] HPChargeDurationRequest request)
        {
            try
            {
                // Manual validations
                if (request == null)
                {
                    return BadRequest(new { message = "Invalid request data." });
                }

                if (companyId <= 0)
                {
                    return BadRequest(new { message = "Company Id is required." });
                }

                if (hpChargeDurationId <= 0)
                {
                    return BadRequest(new { message = "HP Charge Duration Id is required." });
                }

                if (request.HPChargeInstallments <= 0)
                {
                    return BadRequest(new { message = "Please enter HP Charge Inst's." });
                }

                // Parse dates
                DateTime upDateFromDate;
                DateTime upDateToDate;

                try
                {
                    upDateFromDate = new DateTime(request.FromDateYear, request.FromDateMonth, request.FromDateDay);
                    upDateToDate = new DateTime(request.ToDateYear, request.ToDateMonth, request.ToDateDay);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return BadRequest(new { message = "Not a valid date." });
                }
                catch (Exception)
                {
                    return BadRequest(new { message = "Not a valid date." });
                }

                // Validate ToDate >= FromDate
                if (DateTime.Compare(upDateToDate, upDateFromDate) < 0)
                {
                    return BadRequest(new { message = "To Date should be Equal or Greater than From date" });
                }

                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Check for overlapping date ranges (excluding current record)
                    var checkCmd = new SqlCommand(
                        "SELECT FromDate, ToDate FROM HPChargeDuration " +
                        "WHERE CompanyId = @CompanyId AND HPChargeDurationId != @HPChargeDurationId",
                        conn);
                    checkCmd.Parameters.AddWithValue("@CompanyId", companyId);
                    checkCmd.Parameters.AddWithValue("@HPChargeDurationId", hpChargeDurationId);

                    int count = 0;
                    using (var dr = checkCmd.ExecuteReader())
                    {
                        var existingRanges = new List<(DateTime FromDate, DateTime ToDate)>();
                        while (dr.Read())
                        {
                            existingRanges.Add((
                                Convert.ToDateTime(dr["FromDate"]),
                                Convert.ToDateTime(dr["ToDate"])
                            ));
                        }
                        dr.Close();

                        // Check for overlaps
                        foreach (var range in existingRanges)
                        {
                            var overlapCmd = new SqlCommand(
                                "SELECT COUNT(*) FROM HPChargeDuration " +
                                "WHERE ((@FromDate BETWEEN FromDate AND ToDate) OR (@ToDate BETWEEN FromDate AND ToDate)) " +
                                "AND HPChargeDurationId != @HPChargeDurationId " +
                                "AND CompanyId = @CompanyId",
                                conn);
                            overlapCmd.Parameters.AddWithValue("@FromDate", upDateFromDate);
                            overlapCmd.Parameters.AddWithValue("@ToDate", upDateToDate);
                            overlapCmd.Parameters.AddWithValue("@HPChargeDurationId", hpChargeDurationId);
                            overlapCmd.Parameters.AddWithValue("@CompanyId", companyId);

                            if (Convert.ToInt32(overlapCmd.ExecuteScalar()) > 0)
                            {
                                count++;
                                break;
                            }
                        }
                    }

                    // Update if no overlap found
                    if (count == 0)
                    {
                        var updateCmd = new SqlCommand(
                            "UPDATE HPChargeDuration " +
                            "SET FromDate = @FromDate, ToDate = @ToDate, HPChargeInstallments = @HPChargeInstallments " +
                            "WHERE HPChargeDurationId = @HPChargeDurationId",
                            conn);

                        updateCmd.Parameters.AddWithValue("@FromDate", upDateFromDate);
                        updateCmd.Parameters.AddWithValue("@ToDate", upDateToDate);
                        updateCmd.Parameters.AddWithValue("@HPChargeInstallments", request.HPChargeInstallments);
                        updateCmd.Parameters.AddWithValue("@HPChargeDurationId", hpChargeDurationId);

                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { message = "HP Charge Inst's updated successfully." });
                        }
                        else
                        {
                            return NotFound(new { message = "HP Charge Duration not found." });
                        }
                    }
                    else
                    {
                        return BadRequest(new { message = "Already HP Charge Inst's entered for this duration." });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception here (use ILogger)
                return StatusCode(500, new { message = "Failed to update the record." });
            }
        }

        [Authorize]
        [HttpDelete("DeleteHPChargeDuration/{hpChargeDurationId}")]
        public IActionResult DeleteHPChargeDuration(int hpChargeDurationId)
        {
            try
            {
                if (hpChargeDurationId <= 0)
                {
                    return BadRequest(new { message = "Invalid HP Charge Duration Id." });
                }

                using (var conn = GetConnection())
                {
                    conn.Open();

                    var deleteCmd = new SqlCommand(
                        "DELETE FROM HPChargeDuration WHERE HPChargeDurationId = @HPChargeDurationId",
                        conn);

                    deleteCmd.Parameters.AddWithValue("@HPChargeDurationId", hpChargeDurationId);

                    int rowsAffected = deleteCmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        return Ok(new { message = "Record deleted successfully." });
                    }
                    else
                    {
                        return NotFound(new { message = "HP Charge Duration not found." });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception here (use ILogger)
                return StatusCode(500, new { message = "Failed to delete the Record." });
            }
        }

        #endregion

        #region Camp Charge Amount APIs        

        [Authorize]
        [HttpGet("GetCampChargesAmounts/{companyId}/{branchId}")]
        public IActionResult GetCampChargesAmounts(int companyId, int branchId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    var cmd = new SqlCommand(
                        "SELECT BranchId, BranchName, CCAmountId, InstallmentNo, Amount " +
                        "FROM vw_CampChargesAmount " +
                        "WHERE companyId = @CompanyId AND BranchId = @BranchId " +
                        "ORDER BY InstallmentNo",
                        conn);

                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@BranchId", branchId);

                    var campCharges = new List<CampChargeAmountDto>();

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            campCharges.Add(new CampChargeAmountDto
                            {
                                CCAmountId = Convert.ToInt32(dr["CCAmountId"]),
                                BranchId = Convert.ToInt32(dr["BranchId"]),
                                BranchName = dr["BranchName"].ToString()!,
                                InstallmentNo = Convert.ToInt32(dr["InstallmentNo"]),
                                Amount = Convert.ToDecimal(dr["Amount"])
                            });
                        }
                    }

                    return Ok(campCharges);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to bind Camp Charges Amount data.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("CreateCampChargesAmount")]
        public IActionResult CreateCampChargesAmount([FromBody] CampChargeAmountDto request)
        {
            try
            {
                // Validate branch selection
                if (request.BranchId <= 0)
                {
                    return BadRequest(new { message = "Please select branch." });
                }

                using (var conn = GetConnection())
                {
                    conn.Open();

                    // Check if installment already exists for this branch
                    var checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM CampChargesAmount WHERE BranchId = @BranchId AND InstallmentNo = @InstallmentNo",
                        conn);
                    checkCmd.Parameters.AddWithValue("@BranchId", request.BranchId);
                    checkCmd.Parameters.AddWithValue("@InstallmentNo", request.InstallmentNo);

                    int existingCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (existingCount > 0)
                    {
                        return Conflict(new { message = $"Camp Charges Amount already entering for installment no {request.InstallmentNo}" });
                    }

                    // Insert new record
                    var insertCmd = new SqlCommand(
                        "INSERT INTO CampChargesAmount (InstallmentNo, Amount, BranchId) VALUES (@InstallmentNo, @Amount, @BranchId)",
                        conn);
                    insertCmd.Parameters.AddWithValue("@InstallmentNo", request.InstallmentNo);
                    insertCmd.Parameters.AddWithValue("@Amount", request.Amount);
                    insertCmd.Parameters.AddWithValue("@BranchId", request.BranchId);

                    insertCmd.ExecuteNonQuery();

                    return Ok(new { message = "Camp Charges Amount saved successfully." });
                }
            }
            catch (SqlException ex) when (ex.Message.Contains("Violation of UNIQUE KEY constraint 'UK_InstallmentNo'"))
            {
                return Conflict(new { message = "Can't insert, Already amount entered for this installment no." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("UpdateCampChargeAmount/{ccAmountId}")]
        public IActionResult UpdateCampChargeAmount(int ccAmountId, [FromBody] CampChargeAmountDto request)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand(
                        "UPDATE CampChargesAmount SET Amount = @Amount WHERE CCAmountId = @CCAmountId",
                        conn);
                    cmd.Parameters.AddWithValue("@Amount", request.Amount);
                    cmd.Parameters.AddWithValue("@CCAmountId", ccAmountId);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Record not found." });
                    }

                    return Ok(new { message = "Camp Charges Amount updated successfully." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update the record.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("DeleteCampChargeAmount/{ccAmountId}")]
        public IActionResult DeleteCampChargeAmount(int ccAmountId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand(
                        "DELETE FROM CampChargesAmount WHERE CCAmountId = @CCAmountId",
                        conn);
                    cmd.Parameters.AddWithValue("@CCAmountId", ccAmountId);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Record not found." });
                    }

                    return Ok(new { message = "Record deleted successfully." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete the record.", error = ex.Message });
            }
        }


        #endregion

        #region Receipt Late Payment APIs

        [Authorize]
        [HttpGet("GetReceiptLatePayment")]
        public IActionResult GetReceiptLatePayment()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand("SELECT RLPId, Installment, Percentage FROM ReceiptLatePayment", conn);
                    var reader = cmd.ExecuteReader();

                    var result = new List<object>();

                    while (reader.Read())
                    {
                        result.Add(new
                        {
                            RLPId = reader["RLPId"],
                            Installment = reader["Installment"],
                            Percentage = reader["Percentage"]
                        });
                    }

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch Receipt Late Payment data.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("InsertReceiptLatePayment")]
        public IActionResult InsertReceiptLatePayment([FromBody] ReceiptLatePaymentDto request)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand(
                        "INSERT INTO ReceiptLatePayment (Installment, Percentage) VALUES (@Installment, @Percentage)",
                        conn);

                    cmd.Parameters.AddWithValue("@Installment", request.Installment);
                    cmd.Parameters.AddWithValue("@Percentage", request.Percentage);

                    cmd.ExecuteNonQuery();

                    return Ok(new { message = "Percentage for receipt late payment saved successfully." });
                }
            }
            catch (SqlException ex)
            {
                if (ex.Message.Contains("UKRLP_Installment")) // handle unique constraint violation
                {
                    return Conflict(new { message = $"Can't insert, percentage already exists for installment no {request.Installment}." });
                }

                return StatusCode(500, new { message = "Failed to submit the data.", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("UpdateReceiptLatePayment/{rlpId}")]
        public IActionResult UpdateReceiptLatePayment(int rlpId, [FromBody] ReceiptLatePaymentDto request)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand(
                        "UPDATE ReceiptLatePayment SET Percentage = @Percentage WHERE RLPId = @RLPId",
                        conn);

                    cmd.Parameters.AddWithValue("@Percentage", request.Percentage);
                    cmd.Parameters.AddWithValue("@RLPId", rlpId);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Record not found." });
                    }

                    return Ok(new { message = "Percentage for the installment updated successfully." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update the record.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("DeleteReceiptLatePayment/{rlpId}")]
        public IActionResult DeleteReceiptLatePayment(int rlpId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand(
                        "DELETE FROM ReceiptLatePayment WHERE RLPId = @RLPId",
                        conn);
                    cmd.Parameters.AddWithValue("@RLPId", rlpId);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "Record not found." });
                    }

                    return Ok(new { message = "Record deleted successfully." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to delete the record.", error = ex.Message });
            }
        }

        #endregion

        #region Camp Charge In Month APIs

        [Authorize]
        [HttpGet("GetCampChargeInMonth")]
        public IActionResult GetCampChargeInMonth()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand("SELECT Entries FROM CampChrgInMonth", conn);
                    var result = cmd.ExecuteScalar();

                    if (result == null || result == DBNull.Value)
                    {
                        return NotFound(new { message = "No entries found in CampChrgInMonth." });
                    }

                    return Ok(Convert.ToInt32(result));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch Camp Charges In Month data.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("UpdateCampChargeInMonth")]
        public IActionResult UpdateCampChargeInMonth([FromBody] int entries)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var cmd = new SqlCommand("UPDATE CampChrgInMonth SET Entries = @Entries", conn);
                    cmd.Parameters.AddWithValue("@Entries", entries);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 0)
                    {
                        return NotFound(new { message = "No record found to update in CampChrgInMonth." });
                    }

                    return Ok(new { message = "Camp charges entries in month updated successfully." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to change camp charges entries.", error = ex.Message });
            }
        }


        #endregion
    }
}
