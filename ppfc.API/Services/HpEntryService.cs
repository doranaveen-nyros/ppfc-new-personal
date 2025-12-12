using System.Data;
using System.Data.SqlClient;
using ppfc.DTO;

namespace ppfc.API.Services
{
    public class HpEntryService
    {
        private readonly IConfiguration _configuration;

        public HpEntryService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        #region HP Entry Insert

        public int CheckFundingRestriction(int branchId, int vehicleId, int? vehicleYear, decimal financeValue, decimal docCharges, decimal rtoCharges)
        {
            using var con = GetConnection();
            var cmd = new SqlCommand(@"
        SELECT [" + vehicleYear + @"] 
        FROM OverFunding 
        WHERE BranchId=@BranchId AND VehicleId=@VehicleId", con);

            cmd.Parameters.AddWithValue("@BranchId", branchId);
            cmd.Parameters.AddWithValue("@VehicleId", vehicleId);

            con.Open();
            var fund = cmd.ExecuteScalar();

            if (fund == null) return 1;

            decimal total = financeValue + docCharges + rtoCharges;
            return total > Convert.ToDecimal(fund) ? 0 : 1;
        }

        public int InsertHpEntry(HpEntryDetailsDto dto)
        {
            using var con = GetConnection();
            var cmd = new SqlCommand("sp_HPEntry_Insert", con);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@HPEntry_Id", dto.CompanyId);
            cmd.Parameters.AddWithValue("@SurName", dto.SurName);
            cmd.Parameters.AddWithValue("@Name", dto.Name);
            cmd.Parameters.AddWithValue("@FatherName", dto.FatherName);
            cmd.Parameters.AddWithValue("@Age", dto.Age ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DOB", dto.DOB ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Town", dto.Town);
            cmd.Parameters.AddWithValue("@District", dto.DistrictId);
            cmd.Parameters.AddWithValue("@Occupation", dto.Occupation);
            cmd.Parameters.AddWithValue("@MobileNumber", dto.MobileNumber);
            cmd.Parameters.AddWithValue("@LandNumber", dto.LandNumber);
            cmd.Parameters.AddWithValue("@Address", dto.Address);
            cmd.Parameters.AddWithValue("@ResidenceProof", dto.ResidenceProof);
            cmd.Parameters.AddWithValue("@IncomeProof", dto.IncomeProof);

            cmd.Parameters.AddWithValue("@VehicleId", dto.VehicleId);
            cmd.Parameters.AddWithValue("@VehicleNumber", dto.VehicleNumber);
            cmd.Parameters.AddWithValue("@VehicleModel", dto.VehicleModel);
            cmd.Parameters.AddWithValue("@InsuranceDate", dto.InsuranceDate);
            cmd.Parameters.AddWithValue("@EngineNumber", dto.EngineNumber);
            cmd.Parameters.AddWithValue("@ChasisNumber", dto.ChasisNumber);
            cmd.Parameters.AddWithValue("@MarketValue", dto.MarketValue);
            cmd.Parameters.AddWithValue("@CBook", dto.CBook);
            cmd.Parameters.AddWithValue("@SaleLetter", dto.SaleLetter);

            cmd.Parameters.AddWithValue("@FinanceValue", dto.FinanceValue);
            cmd.Parameters.AddWithValue("@RTOCharge", dto.RTOCharge);
            cmd.Parameters.AddWithValue("@HPCharge", dto.HPCharge);
            cmd.Parameters.AddWithValue("@Installments", dto.Installments);
            cmd.Parameters.AddWithValue("@FundingPercentage", dto.FundingPercentage);
            cmd.Parameters.AddWithValue("@EmployeeId", dto.EmployeeId);
            cmd.Parameters.AddWithValue("@VerifiedBy", dto.VerifiedBy);
            cmd.Parameters.AddWithValue("@AutoConsultantId", dto.AutoConsultantId);
            cmd.Parameters.AddWithValue("@FinanceClearletter", dto.FinanceClearLetter);
            cmd.Parameters.AddWithValue("@FinanceName", string.IsNullOrEmpty(dto.FinanceName) ? (object)DBNull.Value : dto.FinanceName);
            cmd.Parameters.AddWithValue("@DOCCharges", dto.DocCharges);
            cmd.Parameters.AddWithValue("@BankName", dto.BankName);
            cmd.Parameters.AddWithValue("@AccountNo", dto.AccountNo);
            cmd.Parameters.AddWithValue("@NoTR", dto.NoTR);
            cmd.Parameters.AddWithValue("@PartyDebitAmount", dto.PartyDebitAmount);

            cmd.Parameters.AddWithValue("@CompanyId", dto.CompanyId);
            cmd.Parameters.AddWithValue("@BranchId", dto.BranchId);
            cmd.Parameters.AddWithValue("@AreaId", dto.AreaId);
            cmd.Parameters.AddWithValue("@MandalId", dto.MandalId);

            cmd.Parameters.AddWithValue("@UsersId", dto.UserId);
            cmd.Parameters.AddWithValue("@Date", DateTime.Now);
            cmd.Parameters.AddWithValue("@AdjustDate", dto.AdjustDate);
            cmd.Parameters.AddWithValue("@Time", DateTime.Now);
            cmd.Parameters.AddWithValue("@Description", dto.Description);

            bool free = false;
            cmd.Parameters.AddWithValue("@Free", free);
            cmd.Parameters.AddWithValue("@WhatsappNumber", free);
            cmd.Parameters.AddWithValue("@AdharNumber", free);
            cmd.Parameters.AddWithValue("@FaceBookId", free);

            decimal finValue = dto.FinanceValue;
            decimal rtoCharges = dto.RTOCharge;
            decimal docCharges = dto.DocCharges;
            decimal hpCharges = dto.HPCharge;

            decimal denominator = finValue + rtoCharges + docCharges + hpCharges;
            if (denominator == 0)
            {
                cmd.Parameters.AddWithValue("@HPFinPer", 0);
                cmd.Parameters.AddWithValue("@HPChargePer", 0);
            }
            else
            {
                decimal HPFinPer = (finValue + rtoCharges + docCharges) / denominator;
                decimal HPChargePer = hpCharges / denominator;

                cmd.Parameters.AddWithValue("@HPFinPer", Convert.ToDecimal(string.Format("{0:0.0000}", HPFinPer)));
                cmd.Parameters.AddWithValue("@HPChargePer", Convert.ToDecimal(string.Format("{0:0.0000}", HPChargePer)));
            }

            int pending = 0;
            if(dto.AutoConsultantPending == 2)
                pending = 1;

            cmd.Parameters.AddWithValue("@AutoConsultantPending", pending);

            // Output
            var output = new SqlParameter("@OutPut", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(output);

            con.Open();
            cmd.ExecuteNonQuery();

            return Convert.ToInt32(output.Value);
        }

        public void InsertPartyDebitEntry(int hpentryId, decimal amount, int branchId, int companyId, int userId)
        {
            using var con = GetConnection();

            int voucherNo = 1;

            using (var cmdVno = new SqlCommand(
                @"SELECT TOP 1 VoucherNumber 
          FROM PartyDebit 
          WHERE Amount > 0 AND CompanyId=@CompanyId 
          ORDER BY PartyDebitId DESC", con))
            {
                cmdVno.Parameters.AddWithValue("@CompanyId", companyId);

                con.Open();
                var result = cmdVno.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    int lastVoucher = Convert.ToInt32(result);
                    voucherNo = (lastVoucher >= 2000) ? 1 : lastVoucher + 1;
                }

                con.Close();
            }

            var cmd = new SqlCommand("sp_PartyDebit_Insert", con);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@PartyDebit_Id", companyId);
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            cmd.Parameters.AddWithValue("@BranchId", branchId);
            cmd.Parameters.AddWithValue("@HPEntryId", hpentryId);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Narration", DBNull.Value);
            cmd.Parameters.AddWithValue("@Date", DateTime.Now.Date);
            cmd.Parameters.AddWithValue("@Time", DateTime.Now.ToShortTimeString());
            cmd.Parameters.AddWithValue("@AuditFinance", "0");
            cmd.Parameters.AddWithValue("@UsersId", userId);
            cmd.Parameters.AddWithValue("@FromHpEntry", "1");
            cmd.Parameters.AddWithValue("@VoucherNumber", voucherNo);

            var output = new SqlParameter("@OutPut", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(output);

            con.Open();
            cmd.ExecuteNonQuery();
        }



        #endregion

        #region Hp Entry Update

        public int CheckAmounts(int branchId, decimal txtPartyDebitAmount)
        {
            int status = 1;
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("Select LockAmount from PDRLock where branchid=" + branchId, conn);
                var result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    var lockAmt = Convert.ToDecimal(result);
                    if (lockAmt < txtPartyDebitAmount)
                        status = 0;
                }
            }
            return status;
        }

        public decimal GetReceiptAmount(int hpEntryId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand(
                    "select coalesce(sum(PartyDebitAmount),0) as receiptAmount from Receipt where HpEntryId=@HPEntryId",
                    conn);
                cmd.Parameters.AddWithValue("@HPEntryId", hpEntryId);
                return Convert.ToDecimal(cmd.ExecuteScalar());
            }
        }

        public decimal GetPartyDebitAmount(int hpEntryId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand(
                    "select coalesce(sum(Amount),0) as partyDebitAmount from PartyDebit where HpEntryId=@HPEntryId and FromHpEntry!=1",
                    conn);
                cmd.Parameters.AddWithValue("@HPEntryId", hpEntryId);
                return Convert.ToDecimal(cmd.ExecuteScalar());
            }
        }

        public DateTime GetHpEntryDate(int hpEntryId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("Select [Date] from HPEntry where HPEntryId=@HPEntryId", conn);
                cmd.Parameters.AddWithValue("@HPEntryId", hpEntryId);
                return Convert.ToDateTime(cmd.ExecuteScalar());
            }
        }

        public int GetAdjustDays()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("Select Days from AdjustmentDays", conn);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public int CheckFinanceAmount(int hpEntryId, decimal fin, decimal hp, decimal doc, decimal rto)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var q = "Select (Select coalesce(sum(ReceiptAmount),0) from receipt where HPEntryId=@HpEntryid) as PaidReceiptAmount," +
                        "(Select coalesce(sum(Amount),0) from HPReturn where HPEntryId=@HpEntryid) as HPReturnAmount";

                var cmd = new SqlCommand(q, conn);
                cmd.Parameters.AddWithValue("@HpEntryid", hpEntryId);

                decimal receiptAmount = 0, hpReturnAmount = 0;
                using (var dr = cmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        receiptAmount = Convert.ToDecimal(dr["PaidReceiptAmount"]);
                        hpReturnAmount = Convert.ToDecimal(dr["HPReturnAmount"]);
                    }
                }

                decimal newFin = fin + hp + doc + rto;
                return newFin < (receiptAmount + hpReturnAmount) ? 1 : 0;
            }
        }

        public void UpdateHPFinanceValue(int hpEntryId, decimal finValue, decimal doc, decimal rto)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                decimal totFin = finValue + doc + rto;

                var cmd = new SqlCommand("Update hpentry set HPFinanceValue=@HPFinValue where hpentryid=@Hpentryid", conn);
                cmd.Parameters.AddWithValue("@HPFinValue", totFin);
                cmd.Parameters.AddWithValue("@Hpentryid", hpEntryId);
                cmd.ExecuteNonQuery();
            }
        }

        public void Sp_UpdateHPEntry(HpEntryDetailsDto dto)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                var cmd = new SqlCommand("sp_HPEntry_Update", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@HPEntry_Id", dto.HPEntryId);
                cmd.Parameters.AddWithValue("@SurName", dto.SurName);
                cmd.Parameters.AddWithValue("@Name", dto.Name);
                cmd.Parameters.AddWithValue("@FatherName", dto.FatherName);
                cmd.Parameters.AddWithValue("@Age", dto.Age);
                cmd.Parameters.AddWithValue("@DOB", dto.DOB ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Town", dto.Town);
                cmd.Parameters.AddWithValue("@District", dto.DistrictId);
                cmd.Parameters.AddWithValue("@Occupation", dto.Occupation);
                cmd.Parameters.AddWithValue("@MobileNumber", dto.MobileNumber);
                cmd.Parameters.AddWithValue("@LandNumber", dto.LandNumber);
                cmd.Parameters.AddWithValue("@Address", dto.Address);
                cmd.Parameters.AddWithValue("@ResidenceProof", dto.ResidenceProof ? 1 : 0);
                cmd.Parameters.AddWithValue("@IncomeProof", dto.IncomeProof ? 1 : 0);
                cmd.Parameters.AddWithValue("@VehicleId", dto.VehicleId);
                cmd.Parameters.AddWithValue("@VehicleNumber", dto.VehicleNumber);

                string vehicleModel = dto.VehicleModelMonth + " " + dto.VehicleModelYear;
                cmd.Parameters.AddWithValue("@VehicleModel", vehicleModel);

                cmd.Parameters.AddWithValue("@InsuranceDate", dto.InsuranceDate);
                cmd.Parameters.AddWithValue("@EngineNumber", dto.EngineNumber);
                cmd.Parameters.AddWithValue("@ChasisNumber", dto.ChasisNumber);
                cmd.Parameters.AddWithValue("@MarketValue", dto.MarketValue);
                cmd.Parameters.AddWithValue("@CBook", dto.CBook ? 1 : 0);
                cmd.Parameters.AddWithValue("@SaleLetter", dto.SaleLetter ? 1 : 0);
                cmd.Parameters.AddWithValue("@FinanceValue", dto.FinanceValue);
                cmd.Parameters.AddWithValue("@RTOCharge", dto.RTOCharge);
                cmd.Parameters.AddWithValue("@HPCharge", dto.HPCharge);
                cmd.Parameters.AddWithValue("@Installments", dto.Installments);
                cmd.Parameters.AddWithValue("@FundingPercentage", dto.FundingPercentage);

                cmd.Parameters.AddWithValue("@EmployeeId", dto.EmployeeId);
                cmd.Parameters.AddWithValue("@VerifiedBy", dto.VerifiedBy);

                cmd.Parameters.AddWithValue("@AutoConsultantId", dto.AutoConsultantId);
                cmd.Parameters.AddWithValue("@FinanceClearletter", dto.FinanceClearLetter ? 1 : 0);
                cmd.Parameters.AddWithValue("@FinanceName", string.IsNullOrEmpty(dto.FinanceName) ? (object)DBNull.Value : dto.FinanceName);
                cmd.Parameters.AddWithValue("@DOCCharges", dto.DocCharges);
                cmd.Parameters.AddWithValue("@BankName", string.IsNullOrEmpty(dto.BankName) ? (object)DBNull.Value : dto.BankName);
                cmd.Parameters.AddWithValue("@AccountNo", string.IsNullOrEmpty(dto.AccountNo) ? (object)DBNull.Value : dto.AccountNo);
                cmd.Parameters.AddWithValue("@NoTR", dto.NoTR ? 1 : 0);
                cmd.Parameters.AddWithValue("@PartyDebitAmount", dto.PartyDebitAmount == 0 ? (object)DBNull.Value : dto.PartyDebitAmount);

                cmd.Parameters.AddWithValue("@CompanyId", dto.CompanyId);
                cmd.Parameters.AddWithValue("@BranchId", dto.BranchId);
                cmd.Parameters.AddWithValue("@AreaId", dto.AreaId);
                cmd.Parameters.AddWithValue("@MandalId", dto.MandalId);
                cmd.Parameters.AddWithValue("@AdjustDate", dto.AdjustDate);
                cmd.Parameters.AddWithValue("@Description", dto.Description);

                decimal finValue = dto.FinanceValue;
                decimal rtoCharges = dto.RTOCharge;
                decimal docCharges = dto.DocCharges;
                decimal hpCharges = dto.HPCharge;

                decimal denominator = finValue + rtoCharges + docCharges + hpCharges;
                if (denominator == 0)
                {
                    cmd.Parameters.AddWithValue("@HPFinPer", 0);
                    cmd.Parameters.AddWithValue("@HPChargePer", 0);
                }
                else
                {
                    decimal HPFinPer = (finValue + rtoCharges + docCharges) / denominator;
                    decimal HPChargePer = hpCharges / denominator;

                    cmd.Parameters.AddWithValue("@HPFinPer", Convert.ToDecimal(string.Format("{0:0.0000}", HPFinPer)));
                    cmd.Parameters.AddWithValue("@HPChargePer", Convert.ToDecimal(string.Format("{0:0.0000}", HPChargePer)));
                }

                cmd.Parameters.AddWithValue("@WhatsappNumber", string.IsNullOrEmpty(dto.WhatsappNumber) ? (object)DBNull.Value : dto.WhatsappNumber);
                cmd.Parameters.AddWithValue("@AdharNumber", string.IsNullOrEmpty(dto.AdharNumber) ? (object)DBNull.Value : dto.AdharNumber);
                cmd.Parameters.AddWithValue("@FaceBookId", dto.FaceBookId);

                cmd.ExecuteNonQuery();
            }
        }

        public void UpdatePartyDebitEntry(int hpEntryId, decimal amount)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("sp_HPEntry_PartyDebit_Update", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@HPEntryId", hpEntryId);
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.ExecuteNonQuery();
            }
        }

        public bool PartyDebitExists(int hpEntryId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("select count(*) from PartyDebit where HpEntryId=@HPEntryId and FromHpEntry=1", conn);
                cmd.Parameters.AddWithValue("@HPEntryId", hpEntryId);
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        public void InsertPartyDebitEntry(HpEntryDetailsDto dto)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                int voucherNo = 0;
                if (dto.PartyDebitAmount > 0)
                {
                    var cmdVNO = new SqlCommand(
                        "Select coalesce(max(VoucherNumber),0)+1 from PartyDebit where Amount>0 and CompanyId=" + dto.CompanyId,
                        conn);

                    voucherNo = Convert.ToInt32(cmdVNO.ExecuteScalar());
                }

                var cmd = new SqlCommand("sp_PartyDebit_Insert", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@PartyDebit_Id", dto.CompanyId);
                cmd.Parameters.AddWithValue("@CompanyId", dto.CompanyId);
                cmd.Parameters.AddWithValue("@BranchId", dto.BranchId);
                cmd.Parameters.AddWithValue("@HPEntryId", dto.HPEntryId);
                cmd.Parameters.AddWithValue("@Amount", dto.PartyDebitAmount);
                cmd.Parameters.AddWithValue("@Narration", DBNull.Value);
                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToUniversalTime().AddHours(5.5).ToShortDateString());
                cmd.Parameters.AddWithValue("@Time", DateTime.Now.ToUniversalTime().AddHours(5.5).ToShortTimeString());
                cmd.Parameters.AddWithValue("@AuditFinance", "0");
                cmd.Parameters.AddWithValue("@UsersId", dto.UserId);
                cmd.Parameters.AddWithValue("@FromHpEntry", "1");
                cmd.Parameters.AddWithValue("@VoucherNumber", voucherNo);
                cmd.Parameters.Add("@OutPut", SqlDbType.Int).Direction = ParameterDirection.Output;

                cmd.ExecuteNonQuery();
            }
        }


        public void DeletePartyDebitEntry(int hpEntryId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                var cmd = new SqlCommand("delete from PartyDebit where HpEntryId=@HPEntryId and FromHpEntry=1", conn);
                cmd.Parameters.AddWithValue("@HPEntryId", hpEntryId);
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

    }
}
