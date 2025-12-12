using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ppfc.DTO
{

    #region HpEntry Search

    public class HpPopupDto
    {
        public long HpEntryId { get; set; }
        //not id Account Number given to custome
        public long HPEntry_Id { get; set; }
        public DateTime AdjustDate { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = "";
        public string SurName { get; set; } = "";
        public string Name { get; set; } = "";
        public string FatherName { get; set; } = "";
        public string MobileNumber { get; set; } = "";
        public string AdharNumber { get; set; } = "";
        public string VehicleName { get; set; } = "";
        public string VehicleNumber { get; set; } = "";
        public string Town { get; set; } = "";
        public string Address { get; set; } = "";
        public int ACCloseId { get; set; }
        public decimal TotalDue { get; set; }
        public string Description { get; set; } = "";
    }

    public class CloseAccountDto
    {
        public long HpEntryId { get; set; }
        public int BranchId { get; set; }
        public string Description { get; set; } = "";
        public int UserId { get; set; }
    }

    #endregion

    #region Hp Entry

    public class HpEntryDropdownDto
    {
        public int HpEntryId { get; set; }
        public string VehicleNo { get; set; } = "";
    }


    public class HpEntryDetailsDto
    {
        //
        public int HPEntryId { get; set; }
        public int HPEntry_Id { get; set; }
        public int CompanyId { get; set; }
        public int UserId { get; set; }

        // Customer Details

        public string SurName { get; set; } = "";
        public string Name { get; set; } = "";
        public string FatherName { get; set; } = "";
        public int? Age { get; set; }
        public string Town { get; set; } = "";
        public string Occupation { get; set; } = "";
        public string MobileNumber { get; set; } = "";
        public string LandNumber { get; set; } = "";
        public string Address { get; set; } = "";

        public string AdharNumber { get; set; } = "";
        public string WhatsappNumber { get; set; } = "";
        public string FaceBookId { get; set; } = "";

        public bool ResidenceProof { get; set; }
        public bool IncomeProof { get; set; }
        public int AutoConsultantPending { get; set; }

        public DateTime? DOB { get; set; }

        // Area / Branch
        public int BranchId { get; set; }
        public int? AreaId { get; set; }
        public int? MandalId { get; set; }
        public int DistrictId { get; set; }

        // Vehicle Details
        public int VehicleId { get; set; }
        public string VehicleNumber { get; set; } = "";
        public string VehicleModel { get; set; } = "";
        public int? VehicleModelMonth { get; set; }
        public int? VehicleModelYear { get; set; }

        public DateTime? InsuranceDate { get; set; }
        public string EngineNumber { get; set; } = "";
        public string ChasisNumber { get; set; } = "";
        public decimal MarketValue { get; set; }

        public bool CBook { get; set; }
        public bool SaleLetter { get; set; }
        public bool FinanceClearLetter { get; set; }
        public bool NoTR { get; set; }

        // Finance Section
        public decimal FinanceValue { get; set; }
        public decimal RTOCharge { get; set; }
        public decimal HPCharge { get; set; }
        public decimal DocCharges { get; set; }
        public decimal PartyDebitAmount { get; set; }
        public string FinanceName { get; set; } = "";
        public string BankName { get; set; } = "";
        public string AccountNo { get; set; } = "";
        public decimal FundingPercentage { get; set; }
        public int? Installments { get; set; }

        public string VerifiedBy { get; set; } = "";
        public int? EmployeeId { get; set; }
        public int? AutoConsultantId { get; set; }

        public DateTime? AdjustDate { get; set; }
        public string Description { get; set; } = "";
    }

    public class DropdownItemDto
    {
        public int HPEntryId { get; set; }
        public string VehicleNo { get; set; } = "";
    }

    public class AreaDto
    {
        public int AreaId { get; set; }
        public string AreaName { get; set; } = "";
        public int BranchId { get; set; }
    }

    public class MandalDto
    {
        public int MandalId { get; set; }
        public string MandalName { get; set; } = "";
    }

    public class VehiclesDto
    {
        public int VehicleId { get; set; }
        public string VehicleName { get; set; } = "";
    }

    public class EmployeesDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";
    }

    public class ConsultantStatusDto
    {
        public int Limit { get; set; }
        public int Pending { get; set; }
    }

    public class DropItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class AadharCheckResult
    {
        public bool IsBlocked { get; set; }
        public string Message { get; set; }
        public string BlockedInfo { get; set; }
    }


    #endregion

}
