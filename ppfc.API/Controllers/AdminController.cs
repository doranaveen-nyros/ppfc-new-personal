using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ppfc.DTO;
using System.Data.SqlClient;

namespace ppfc.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

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

    }
}
