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

    #region User Creation

    public class EmployeeDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public int UserId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; }
    }


    #endregion

    #region User Settings

    public class UserSettingsDto
    {
        public int UsersId { get; set; }
        public string ScreenName { get; set; }
        public bool AdditionPrivileges { get; set; }
        public bool EditPrivileges { get; set; }
        public bool DeletePrivileges { get; set; }
        public bool ViewPrivileges { get; set; }
        public int Priority { get; set; }
    }

    public class UserSettingsRequest
    {
        public int UserId { get; set; }
        public List<UserSettingsDto> Items { get; set; } = new();
    }

    #endregion

    #region Message Board

    public class MessageDTO
    {
        public int MessageId { get; set; }
        public string Message { get; set; }
        public DateTime DateTime { get; set; }
    }

    #endregion

    #region SMS

    public class CompanyDto
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
    }

    public class BranchDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int CompanyId { get; set; }
    }

    public class MessageReceiverDto
    {
        public string ReceiverName { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class SmsRecipientDto
    {
        public string ReceiverName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class SmsRequestDto
    {
        public string MessageType { get; set; } = "Promo";
        public string Message { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public int BranchId { get; set; }
        public List<SmsRecipientDto> Recipients { get; set; } = new();
    }

    public class SmsDto
    {
        public int SMSId { get; set; }
        public string MessageType { get; set; }
        public string Receiver { get; set; }
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public string Date { get; set; }
        public string Time { get; set; }
        public int CompanyId { get; set; }
        public int? BranchId { get; set; }
        public string? SenderId { get; set; }
        public string UserName { get; set; }
        public bool IsSelected { get; set; }
    }

    #endregion

    #region SMS Settings

    public class BusinessSMSDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    #endregion

    #region HP Charge Duration

    public class HPChargeDurationDto
    {
        public int HPChargeDurationId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? HPChargeInstallments { get; set; }
    }

    public class HPChargeDurationRequest
    {
        public int FromDateDay { get; set; }
        public int FromDateMonth { get; set; }
        public int FromDateYear { get; set; }
        public int ToDateDay { get; set; }
        public int ToDateMonth { get; set; }
        public int ToDateYear { get; set; }
        public int HPChargeInstallments { get; set; }
    }

    #endregion

    #region Camp Charge Amount

    public class CampChargeAmountDto
    {
        public int CCAmountId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int InstallmentNo { get; set; }
        public decimal Amount { get; set; }
        public bool IsNew { get; set; } = false;
    }


    #endregion

    #region Receipt Late Payment

    public class ReceiptLatePaymentDto
    {
        public int RLPId { get; set; }
        public int Installment { get; set; }
        public int Percentage { get; set; }
        public bool IsNew { get; set; } = false;
    }

    #endregion

}
