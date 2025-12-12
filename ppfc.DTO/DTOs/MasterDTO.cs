using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ppfc.DTO
{
    #region Company

    public class CompaniesDto
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
    }

    #endregion

    #region Branch

    public class BranchesDto
    {
        public int BranchId { get; set; }
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string? BranchName { get; set; }
        public bool LockCapital { get; set; }
    }

    #endregion

    #region Area

    public class AreasDto
    {
        public int AreaId { get; set; }
        public string? AreaName { get; set; }
        public string? AreaCode { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public bool Lock { get; set; }
        public bool IsNew { get; set; } = false;

    }

    #endregion

    #region Credit Master

    public class CreditDto
    {
        public int CompanyId { get; set; }
        public int CreditId { get; set; }
        public string CreditAccountName { get; set; } = string.Empty;
    }

    #endregion

    #region Debit Master

    public class DebitDto
    {
        public int CompanyId { get; set; }
        public int DebitId { get; set; }
        public string DebitAccountName { get; set; } = string.Empty;
    }

    #endregion

    #region Revenue Head

    public class RevenueHeadDto
    {
        public int RevenueHeadId { get; set; }
        public string RevenueHeadName { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public int BranchId { get; set; }
        public string? BranchName { get; set; }
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region Expense Master

    public class ExpenseDto
    {
        public int ExpenseId { get; set; }
        public string ExpenseName { get; set; } = string.Empty;
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region Vehicle Master

    public class VehicleDto
    {
        public int VehicleId { get; set; }
        public string VehicleName { get; set; } = string.Empty;
        public string VCode { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region Interest

    public class InterestDto
    {
        public int InterestId { get; set; }
        public decimal Interest { get; set; }
        public string Type { get; set; } = string.Empty;
        public int CompanyId { get; set; }
    }

    #endregion

    #region Auto Consultant

    public class AutoConsultantDto
    {
        public int AutoConsultantId { get; set; }
        public string AutoConsultantName { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public decimal Limit { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string UPIType { get; set; } = string.Empty;
        public string UPIName { get; set; } = string.Empty;
        public string Bank { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string IFSCCode { get; set; } = string.Empty;
        public bool Lock { get; set; }
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region RTO Agent

    public class RTOAgentDto
    {
        public int RTOAgentId { get; set; }
        public string RTOAgentName { get; set; } = string.Empty;
        public string PhoneNo { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool LockAgent { get; set; }
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region Notice Type

    public class NoticeTypeDto
    {
        public int NoticeTypeId { get; set; }
        public string NoticeType { get; set; } = string.Empty;
        public string Notice { get; set; } = string.Empty;
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region Contact Phone No

    public class ContactPhoneDto
    {
        public string PhoneNumber { get; set; } = string.Empty;
    }

    #endregion

    #region Expense Limit

    public class ExpenseLimitDto
    {
        public int ExpenseLimitId { get; set; }
        public int BranchId { get; set; }
        public int ExpenseLimitValue { get; set; }
        public int CompanyId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region Pending Limit

    public class PendingLimitDto
    {
        public int PendingLimitId { get; set; }
        public int BranchId { get; set; }
        public int PendingLimitValue { get; set; }
        public int CompanyId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region PDR Lock

    public class PDRLockDto
    {
        public int PDRLockId { get; set; }
        public int BranchId { get; set; }
        public int LockAmount { get; set; }
        public int CompanyId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool IsNew { get; set; } = false;
    }

    #endregion

    #region Birthday Message

    public class BirthMessageDto
    {
        public int MessageId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string MessagePart { get; set; } = string.Empty;
        public bool Status { get; set; }   // true = Active
        public string Signature { get; set; } = string.Empty;
    }

    #endregion
}
