using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using ppfc.API.Services;
using ppfc.DTO;
using System.Data;
using System.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;

namespace ppfc.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly SmsService _smsService;
        private readonly ClosingBalanceService _closingSvc;
        private readonly ILogger<LoginController> _logger;
        private readonly IMemoryCache _cache;

        public LoginController(IConfiguration configuration, SmsService smsService, ClosingBalanceService closingSvc, ILogger<LoginController> logger, IMemoryCache cache)
        {
            _configuration = configuration;
            _smsService = smsService;
            _closingSvc = closingSvc;
            _logger = logger;
            _cache = cache;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        [HttpPost("GetLogin")]
        public async Task<ActionResult<LoginPrivilegeDto>> GetLogin([FromBody] LoginRequestDto loginDto)
        {
            var privileges = new List<PrivilegeDto>();
            LoginPrivilegeDto response = null;

            using (var con = GetConnection())
            {
                var cmd = new SqlCommand("sp_Select_PPFC_LoginDetails", con)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("UserName", loginDto.UserName);
                cmd.Parameters.AddWithValue("Password", loginDto.Password);

                if (con.State == ConnectionState.Closed)
                    await con.OpenAsync();

                using (var dr = await cmd.ExecuteReaderAsync())
                {
                    if (dr.Read())
                    {
                        response = new LoginPrivilegeDto
                        {
                            UserId = Convert.ToInt32(dr["UsersId"]),
                            RoleName = dr["RoleName"].ToString(),
                            CompanyId = Convert.ToInt32(dr["CompanyId"]),
                            BranchId = Convert.ToInt32(dr["BranchId"]),
                            CompanyCode = Convert.ToInt32(dr["Code"]),
                            CompanyName = dr["CompanyName"].ToString().Trim(),
                            UserName = dr["UserName"].ToString().Trim()
                        };
                    }
                }

                if (response != null)
                {
                    // Store in memory cache for app-wide usage
                    _cache.Set("CompanyId", response.CompanyId, TimeSpan.FromHours(2));      // optional expiration
                    _cache.Set("UserName", response.UserName, TimeSpan.FromHours(2));

                    // Now get privileges for this user
                    var cmdPrivileges = new SqlCommand("Select_sp_ScreenPrivileges", con)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmdPrivileges.Parameters.Add(new SqlParameter("Usersid", response.UserId));

                    using (var drPrivileges = await cmdPrivileges.ExecuteReaderAsync())
                    {
                        while (drPrivileges.Read())
                        {
                            privileges.Add(new PrivilegeDto
                            {
                                ScreenName = drPrivileges["ScreenName"].ToString(),
                                ViewPrivilege = drPrivileges["ViewPrivileges"].ToString()
                            });
                        }
                    }
                    response.Privileges = privileges;
                }
            }

            if (response == null)
                return NotFound();

            // Run closing balance check for the user's company
            try
            {
                // Run non-critical tasks in background — don't block login
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _closingSvc.CheckClosingBalanceAsync(response.CompanyId);
                        //await _smsService.CallInstSMSAsync(response.BranchId, response.CompanyId, response.CompanyName, response.UserName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background task failed after login for CompanyId {CompanyId}", response.CompanyId);
                    }
                });

            }
            catch (Exception ex)
            {
                // Log and continue — don't block login if closing balance fails
                _logger.LogError(ex, "Error during CheckClosingBalance for CompanyId {CompanyId}", response.CompanyId);
            }

            // Generate JWT token
            var token = GenerateJwtToken(response);

            // Return token + login data
            var loginResponse = new LoginResponseDto
            {
                Token = token,
                LoginData = response
            };

            return Ok(loginResponse);
        }

        // Method to generate JWT

        private string GenerateJwtToken(LoginPrivilegeDto user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.RoleName),
                new Claim("CompanyId", user.CompanyId.ToString()),
                new Claim("BranchId", user.BranchId.ToString()),
                new Claim("CompanyName", user.CompanyName)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpireMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("LoadMethods")]
        public async Task<IActionResult> LoadMethods()
        {
            await _smsService.ReceiptUpdateAsync();
            await _smsService.LoadBusinessSMSAsync();
            await _smsService.LoadBusinessSMSSPPFAsync();
            await _smsService.CampChargeInMonthAsync();
            await _smsService.MoveSentSMSAsync();
            //await _smsService.SendSMSAdminAsync();
            //await _smsService.SendSMSAdminSPPFinAsync();

            return Ok("All methods executed successfully.");
        }

        [HttpGet("GetNews")]
        public IActionResult GetNews()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();

                    var query = @"
                SELECT TOP 5 Message, DateTime
                FROM Message
                ORDER BY DateTime DESC";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var list = new List<NewsDto>();

                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                list.Add(new NewsDto
                                {
                                    Message = dr["Message"].ToString()!,
                                    DateTime = Convert.ToDateTime(dr["DateTime"])
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
                    Message = "Failed to load news.",
                    Error = ex.Message
                });
            }
        }

        #region Helper Methods






        #endregion

    }
}
