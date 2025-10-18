using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ppfc.DTO
{

    #region Role Settings

    public class RoleDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class ScreenDto
    {
        public string ScreenName { get; set; } = string.Empty;
    }

    public class RoleSettingsDto
    {
        public int RoleId { get; set; }
        public string ScreenName { get; set; } = string.Empty;
        public bool AdditionPrivileges { get; set; }
        public bool EditPrivileges { get; set; }
        public bool DeletePrivileges { get; set; }
        public bool ViewPrivileges { get; set; }
        public int? Priority { get; set; }
    }

    public class RoleSettingsUpdateRequest
    {
        public int RoleId { get; set; }
        public string DefaultPage { get; set; } = string.Empty;
        public List<RoleSettingsDto> Items { get; set; } = new();
    }

    #endregion

}
