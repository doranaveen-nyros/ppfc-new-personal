using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ppfc.DTO
{
    public class LoginRequestDto
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public class PrivilegeDto
    {
        public string ScreenName { get; set; }
        public string ViewPrivilege { get; set; }
    }

    public class LoginPrivilegeDto
    {
        public int UserId { get; set; }
        public string RoleName { get; set; }
        public int CompanyId { get; set; }
        public int BranchId { get; set; }
        public int CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public string UserName { get; set; }
        public List<PrivilegeDto> Privileges { get; set; }
    }

    public class LoginResponseDto
    {
        // JWT token string
        public string Token { get; set; }

        // Existing login info + privileges
        public LoginPrivilegeDto LoginData { get; set; }
    }

    public class NewsDto
    {
        public string Message { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
    }

}
