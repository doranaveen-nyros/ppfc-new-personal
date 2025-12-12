using System.Data.SqlClient;

namespace ppfc.API.Services
{
    public class OpeningBalanceService
    {
        private readonly IConfiguration _configuration;

        public OpeningBalanceService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        private SqlConnection GetConnection1()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        public decimal GetClosingBalance(int branchId, int companyId, DateTime presentDate)
        {
            decimal closingBalance = 0;

            using var con = GetConnection();
            using var con1 = GetConnection1();

            string appendQry = branchId > 0 ? " AND BranchId=@BranchId " : $" AND CompanyId={companyId}";
            string appendTransQry = branchId > 0 ? " AND TransBranchId=@BranchId " : $" AND CompanyId={companyId}";

            string query = BuildQuery(appendQry, appendTransQry);

            using var cmd = new SqlCommand(query, con);
            cmd.CommandTimeout = 1200;
            cmd.Parameters.AddWithValue("@Date", presentDate.ToShortDateString());

            if (branchId > 0)
                cmd.Parameters.AddWithValue("@BranchId", branchId);

            con.Open();
            using var rdr = cmd.ExecuteReader();

            if (rdr.Read())
            {
                decimal HPEntryFinance = Safe(rdr["HPEntryFinance"]);
                decimal Refinance = Safe(rdr["Refinance"]);
                decimal HPEntryPDR = Safe(rdr["HPEntryPDR"]);
                decimal Expense = Safe(rdr["Expense"]);
                decimal CapitalCredit = Safe(rdr["CapitalCredit"]);
                decimal CapitalDebit = Safe(rdr["CapitalDebit"]);
                decimal PartyCredits = Safe(rdr["totPartyCredit"]);
                decimal PartyDebit = Safe(rdr["totPartyDebit"]);
                decimal PendingCredit = Safe(rdr["totPendingCredit"]);
                decimal PendingDebit = Safe(rdr["totPendingDebit"]);
                decimal ReceiptAmount = Safe(rdr["ReceiptAmount"]);
                decimal ODIntReceipt = Safe(rdr["ODIntReceipt"]);
                decimal ReceiptPDR = Safe(rdr["ReceiptPDR"]);
                decimal PDRIntReceipt = Safe(rdr["PDRIntReceipt"]);
                decimal CampCharge = Safe(rdr["CampCharge"]);
                decimal DOCPaid = Safe(rdr["DOCPaid"]);
                decimal RTOPaid = Safe(rdr["RTOPaid"]);
                decimal LoanDebit = Safe(rdr["totLoanDebit"]);
                decimal LoanCredit = Safe(rdr["totLoanCredit"]);
                decimal SACredit = Safe(rdr["totSACredit"]);
                decimal SADebit = Safe(rdr["totSADebit"]);
                decimal Salary = Safe(rdr["totSalary"]);
                decimal totBankReceipt = Safe(rdr["totBankReceipt"]);

                decimal totalCredit = CapitalCredit + PartyCredits + PendingCredit + ReceiptAmount + ODIntReceipt + ReceiptPDR + PDRIntReceipt + CampCharge + LoanCredit + SACredit;
                decimal totalDebit = HPEntryFinance + Refinance + HPEntryPDR + Expense + CapitalDebit + PartyDebit + PendingDebit + RTOPaid + DOCPaid + LoanDebit + SADebit + Salary + totBankReceipt;

                closingBalance = totalCredit - totalDebit;
            }

            // Previous day's closing balance
            using var prevCmd = new SqlCommand(
                "SELECT TOP 1 ClosingAmount FROM ClosingBalance WHERE BranchId=@B AND Date=@PrevDate ORDER BY Date DESC",
                con1);

            prevCmd.Parameters.AddWithValue("@B", branchId);
            prevCmd.Parameters.AddWithValue("@PrevDate", presentDate.AddDays(-1).ToShortDateString());

            con1.Open();
            var prev = prevCmd.ExecuteScalar();
            if (prev != null)
                closingBalance += Convert.ToDecimal(prev);

            return closingBalance;
        }

        private decimal Safe(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private string BuildQuery(string appendQry, string appendTransQry)
        {
            return $@"
SELECT 
    (SELECT SUM(FinanceValue) FROM HPEntry WHERE Date=@Date {appendQry}) AS HPEntryFinance,
    (SELECT SUM(COALESCE(RefinanceAmount,0)) FROM HPEntry WHERE RefDate=@Date {appendQry}) AS Refinance,
    (SELECT SUM(Amount) FROM PartyDebit WHERE Date=@Date {appendQry}) AS HPEntryPDR,
    (SELECT SUM(Amount) FROM FinanceVoucher WHERE Date=@Date {appendQry}) AS Expense,
    (SELECT SUM(Amount) FROM Capital WHERE AmountType='Credit' AND Date=@Date {appendQry}) AS CapitalCredit,
    (SELECT SUM(Amount) FROM Capital WHERE AmountType='Debit' AND Date=@Date {appendQry}) AS CapitalDebit,
    (SELECT SUM(Amount) FROM PartyCredits WHERE [Type]='Credit' AND Date=@Date {appendQry}) AS totPartyCredit,
    (SELECT SUM(Amount) FROM PartyCredits WHERE [Type]='Debit' AND Date=@Date {appendQry}) AS totPartyDebit,
    (SELECT SUM(Amount) FROM PendingAccount WHERE [Type]='Credit' AND Date=@Date {appendQry}) AS totPendingCredit,
    (SELECT SUM(Amount) FROM PendingAccount WHERE [Type]='Debit' AND Date=@Date {appendQry}) AS totPendingDebit,
    (SELECT SUM(ReceiptAmount) FROM Receipt WHERE Date=@Date {appendQry}) AS ReceiptAmount,
    (SELECT SUM(ODIntPaid) FROM Receipt WHERE Date=@Date {appendQry}) AS ODIntReceipt,
    (SELECT SUM(PartyDebitAmount) FROM Receipt WHERE Date=@Date {appendQry}) AS ReceiptPDR,
    (SELECT SUM(PDRIntPaid) FROM Receipt WHERE Date=@Date {appendQry}) AS PDRIntReceipt,
    (SELECT SUM(Amount) FROM vw_OpenBal_CampCharges WHERE ReceiptDate=@Date {appendQry}) AS CampCharge,
    (SELECT SUM(DOCPaid) FROM DOC WHERE Date=@Date {appendQry}) AS DOCPaid,
    (SELECT SUM(RTOPaid) FROM vw_TransferredRecord WHERE Date=@Date {appendTransQry}) AS RTOPaid,
    (SELECT SUM(Amount) FROM Loan WHERE AmountType='Debit' AND Date=@Date {appendQry}) AS totLoanDebit,
    (SELECT SUM(Amount) FROM Loan WHERE AmountType='Credit' AND Date=@Date {appendQry}) AS totLoanCredit,
    (SELECT SUM(Amount) FROM SalaryAdvance WHERE AmountType='Credit' AND Date=@Date {appendQry}) AS totSACredit,
    (SELECT SUM(Amount) FROM SalaryAdvance WHERE AmountType='Debit' AND Date=@Date {appendQry}) AS totSADebit,
    (SELECT SUM(Payment)+SUM(Others) FROM Salary WHERE Date=@Date {appendQry}) AS totSalary,
    (SELECT SUM(COALESCE(amount,0)) FROM vw_SaveTransactions WHERE ReceiptDate=@Date {appendQry}) AS totBankReceipt";
        }
    }

}
