using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ppfc.API.Services
{
    public class ClosingBalanceService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ClosingBalanceService> _logger;
        private readonly string connectionString;

        public ClosingBalanceService(IConfiguration configuration, IMemoryCache cache, HttpClient httpClient, ILogger<ClosingBalanceService> logger)
        {
            _configuration = configuration;
            _cache = cache;
            _httpClient = httpClient;
            _logger = logger;
            connectionString = GetConnection().ConnectionString;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        public async Task CheckClosingBalanceAsync(int companyId,CancellationToken cancellationToken = default)
        {
            //var connectionString = GetConnection().ConnectionString;

            try
            {
                // Get current India time
                var today = DateTime.UtcNow.AddHours(5.5).Date;

                // Get date to start from
                var closingDate = await GetClosingDateAsync(companyId, cancellationToken);
                int cmpVal = DateTime.Compare(today, closingDate.Date);

                if (cmpVal <= 0)
                    return; // Nothing to close yet

                // Step 1: Get closed branches for that date
                var closedBranchIds = new HashSet<int>();
                const string sqlClosedBranches = @"
            SELECT BranchId
            FROM ClosingBalance
            WHERE Date = @Date AND CompanyId = @CompanyId;";

                await using (var con = new SqlConnection(connectionString))
                await using (var cmd = new SqlCommand(sqlClosedBranches, con))
                {
                    cmd.Parameters.AddWithValue("@Date", closingDate.Date);
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    await con.OpenAsync(cancellationToken);
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                        closedBranchIds.Add(rdr.GetInt32(0));
                }

                // Step 2: Loop through all branches of company
                const string sqlBranches = "SELECT BranchId FROM Branch WHERE CompanyId = @CompanyId;";
                await using (var con1 = new SqlConnection(connectionString))
                await using (var cmdBranches = new SqlCommand(sqlBranches, con1))
                {
                    cmdBranches.Parameters.AddWithValue("@CompanyId", companyId);
                    await con1.OpenAsync(cancellationToken);
                    using var drBranches = await cmdBranches.ExecuteReaderAsync(cancellationToken);

                    while (await drBranches.ReadAsync(cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var branchId = drBranches.GetInt32(0);
                        if (closedBranchIds.Contains(branchId))
                            continue; // already closed

                        // Step 3: Calculate today's closing balance
                        var todaysBalance = await CalcClosingAmountAsync(branchId, closingDate, cancellationToken);

                        // Step 4: Get previous day's closing amount
                        decimal prevClosingAmount = 0;
                        const string sqlPrev = @"
                    SELECT ISNULL(ClosingAmount, 0)
                    FROM ClosingBalance
                    WHERE Date = @Date AND BranchId = @BranchId;";

                        await using (var con2 = new SqlConnection(connectionString))
                        await using (var cmdPrev = new SqlCommand(sqlPrev, con2))
                        {
                            cmdPrev.Parameters.AddWithValue("@Date", closingDate.AddDays(-1).Date);
                            cmdPrev.Parameters.AddWithValue("@BranchId", branchId);
                            await con2.OpenAsync(cancellationToken);
                            var val = await cmdPrev.ExecuteScalarAsync(cancellationToken);
                            if (val != DBNull.Value && val != null)
                                prevClosingAmount = Convert.ToDecimal(val);
                        }

                        // Step 5: Insert new closing amount
                        var finalClosingAmount = prevClosingAmount + todaysBalance;
                        await InsertClosingAmountAsync(companyId, branchId, finalClosingAmount, closingDate, cancellationToken);
                    }
                }

                // Step 6: Recursively check next available closing date
                var nextClosingDate = await GetClosingDateAsync(companyId, cancellationToken);
                int nextCmp = DateTime.Compare(today, nextClosingDate.Date);
                //if (nextCmp > 0) // currently not updating recursively to avoid long chains
                //    await CheckClosingBalanceAsync(companyId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Closing balance check cancelled for company {CompanyId}", companyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CheckClosingBalanceAsync for CompanyId {CompanyId}", companyId);
                throw;
            }
        }

        public async Task<DateTime> GetClosingDateAsync(int companyId,CancellationToken cancellationToken = default)
        {
            DateTime lastDate = new DateTime(2016, 5, 18);

            try
            {
                int branchCount = 0;
                int closedBranchCount = 0;

                // 1️⃣ Get total number of branches for this company
                const string sqlBranchCount = @"
            SELECT COUNT(*) 
            FROM Branch 
            WHERE CompanyId = @CompanyId;";

                await using (var con = new SqlConnection(connectionString))
                await using (var cmd = new SqlCommand(sqlBranchCount, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    await con.OpenAsync(cancellationToken);
                    var result = await cmd.ExecuteScalarAsync(cancellationToken);
                    branchCount = Convert.ToInt32(result);
                }

                // 2️⃣ Get most recent closing record
                const string sqlLastRecord = @"
            SELECT TOP 1 [Date], COUNT(*) AS BranchCnt
            FROM ClosingBalance
            WHERE CompanyId = @CompanyId
            GROUP BY [Date]
            ORDER BY [Date] DESC;";

                await using (var con = new SqlConnection(connectionString))
                await using (var cmd = new SqlCommand(sqlLastRecord, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    await con.OpenAsync(cancellationToken);

                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        lastDate = reader.GetDateTime(0);
                        closedBranchCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    }
                }

                // 3️⃣ If all branches closed for lastDate, move to next day
                if (branchCount <= closedBranchCount)
                {
                    lastDate = lastDate.AddDays(1);
                }

                return lastDate;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("GetClosingDateAsync cancelled for company {CompanyId}", companyId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting closing date for company {CompanyId}", companyId);
                throw;
            }
        }

        public async Task<decimal> CalcClosingAmountAsync(int branchId,DateTime prevDayDate,CancellationToken cancellationToken = default)
        {
            decimal closingBalance = 0m;

            try
            {
                string query = GetQueryString();

                await using (var con = new SqlConnection(connectionString))
                await using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Date", prevDayDate.Date);
                    cmd.Parameters.AddWithValue("@BranchId", branchId);

                    await con.OpenAsync(cancellationToken);

                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        // Helper to read decimal safely
                        decimal GetDecimal(string colName) =>
                            reader[colName] == DBNull.Value ? 0 : Convert.ToDecimal(reader[colName]);

                        decimal hpEntryFinance = GetDecimal("HPEntryFinance");
                        decimal refinance = GetDecimal("Refinance");
                        decimal hpEntryPdr = GetDecimal("HPEntryPDR");
                        decimal expense = GetDecimal("Expense");
                        decimal capitalCredit = GetDecimal("CapitalCredit");
                        decimal capitalDebit = GetDecimal("CapitalDebit");
                        decimal partyCredits = GetDecimal("totPartyCredit");
                        decimal partyDebit = GetDecimal("totPartyDebit");
                        decimal pendingCredit = GetDecimal("totPendingCredit");
                        decimal pendingDebit = GetDecimal("totPendingDebit");
                        decimal receiptAmount = GetDecimal("ReceiptAmount");
                        decimal odIntReceipt = GetDecimal("ODIntReceipt");
                        decimal receiptPdr = GetDecimal("ReceiptPDR");
                        decimal pdrIntReceipt = GetDecimal("PDRIntReceipt");
                        decimal campCharge = GetDecimal("CampCharge");
                        decimal docPaid = GetDecimal("DOCPaid");
                        decimal rtoPaid = GetDecimal("RTOPaid");
                        decimal loanDebit = GetDecimal("totLoanDebit");
                        decimal loanCredit = GetDecimal("totLoanCredit");
                        decimal saCredit = GetDecimal("totSACredit");
                        decimal saDebit = GetDecimal("totSADebit");
                        decimal salary = GetDecimal("totSalary");
                        decimal totBankReceipt = GetDecimal("totBankReceipt");

                        decimal totalCredit = capitalCredit + partyCredits + pendingCredit + receiptAmount + odIntReceipt +
                                              receiptPdr + pdrIntReceipt + campCharge + loanCredit + saCredit;

                        decimal totalDebit = hpEntryFinance + refinance + hpEntryPdr + expense + capitalDebit + partyDebit +
                                             pendingDebit + rtoPaid + docPaid + loanDebit + saDebit + salary + totBankReceipt;

                        closingBalance = totalCredit - totalDebit;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Calculation of closing amount canceled for branch {BranchId}", branchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating closing amount for branch {BranchId}", branchId);
                throw;
            }

            return closingBalance;
        }

        private static string GetQueryString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("SELECT");
            sb.AppendLine("    (SELECT SUM(FinanceValue) FROM HPEntry WHERE Date = @Date AND BranchId = @BranchId) AS HPEntryFinance,");
            sb.AppendLine("    (SELECT SUM(COALESCE(RefinanceAmount, 0)) FROM HPEntry WHERE RefDate = @Date AND BranchId = @BranchId) AS Refinance,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM vw_PartyDebit WHERE Date = @Date AND BranchId = @BranchId) AS HPEntryPDR,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM FinanceVoucher WHERE Date = @Date AND BranchId = @BranchId) AS Expense,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM vw_Capital WHERE AmountType = 'Credit' AND Date = @Date AND BranchId = @BranchId) AS CapitalCredit,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM vw_Capital WHERE AmountType = 'Debit' AND Date = @Date AND BranchId = @BranchId) AS CapitalDebit,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM PartyCredits WHERE [Type] = 'Credit' AND Date = @Date AND BranchId = @BranchId) AS totPartyCredit,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM PartyCredits WHERE [Type] = 'Debit' AND Date = @Date AND BranchId = @BranchId) AS totPartyDebit,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM PendingAccount WHERE [Type] = 'Credit' AND Date = @Date AND BranchId = @BranchId) AS totPendingCredit,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM PendingAccount WHERE [Type] = 'Debit' AND Date = @Date AND BranchId = @BranchId) AS totPendingDebit,");
            sb.AppendLine("    (SELECT SUM(ReceiptAmount) FROM vw_Receipt WHERE Date = @Date AND BranchId = @BranchId) AS ReceiptAmount,");
            sb.AppendLine("    (SELECT SUM(ODIntPaid) FROM vw_Receipt WHERE Date = @Date AND BranchId = @BranchId) AS ODIntReceipt,");
            sb.AppendLine("    (SELECT SUM(PartyDebitAmount) FROM vw_Receipt WHERE Date = @Date AND BranchId = @BranchId) AS ReceiptPDR,");
            sb.AppendLine("    (SELECT SUM(PDRIntPaid) FROM vw_Receipt WHERE Date = @Date AND BranchId = @BranchId) AS PDRIntReceipt,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM vw_CampCharges WHERE ReceiptDate = @Date AND BranchId = @BranchId) AS CampCharge,");
            sb.AppendLine("    (SELECT SUM(DOCPaid) FROM vw_DOC WHERE Date = @Date AND BranchId = @BranchId) AS DOCPaid,");
            sb.AppendLine("    (SELECT SUM(RTOPaid) FROM vw_TransferredRecord WHERE Date = @Date AND TransBranchId = @BranchId) AS RTOPaid,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM Loan WHERE AmountType = 'Debit' AND Date = @Date AND LDelete = 0 AND BranchId = @BranchId) AS totLoanDebit,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM Loan WHERE AmountType = 'Credit' AND Date = @Date AND LDelete = 0 AND BranchId = @BranchId) AS totLoanCredit,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM SalaryAdvance WHERE AmountType = 'Credit' AND Date = @Date AND SalAdvDelete = 0 AND BranchId = @BranchId) AS totSACredit,");
            sb.AppendLine("    (SELECT SUM(Amount) FROM SalaryAdvance WHERE AmountType = 'Debit' AND Date = @Date AND SalAdvDelete = 0 AND BranchId = @BranchId) AS totSADebit,");
            sb.AppendLine("    (SELECT SUM(Payment) + SUM(Others) FROM Salary WHERE Date = @Date AND SalDelete = 0 AND BranchId = @BranchId) AS totSalary,");
            sb.AppendLine("    (SELECT SUM(COALESCE(Amount, 0)) FROM vw_SaveTransactions WHERE ReceiptDate = @Date AND BranchId = @BranchId) AS totBankReceipt;");

            return sb.ToString();
        }

        public async Task InsertClosingAmountAsync(int companyId,int branchId,decimal closingAmount,DateTime closingDate,CancellationToken cancellationToken = default)
        {
            const string query = @"
        INSERT INTO ClosingBalance (CompanyId, BranchId, ClosingAmount, Date)
        VALUES (@CompanyId, @BranchId, @ClosingAmount, @Date);";

            try
            {
                await using (var con = new SqlConnection(connectionString))
                await using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@BranchId", branchId);
                    cmd.Parameters.AddWithValue("@ClosingAmount", closingAmount);
                    cmd.Parameters.AddWithValue("@Date", closingDate.Date);

                    await con.OpenAsync(cancellationToken);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                _logger.LogInformation(
                    "Inserted closing amount for CompanyId {CompanyId}, BranchId {BranchId}, Date {Date}, Amount {Amount}",
                    companyId, branchId, closingDate.ToString("yyyy-MM-dd"), closingAmount);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("InsertClosingAmountAsync cancelled for BranchId {BranchId}", branchId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error inserting closing amount for CompanyId {CompanyId}, BranchId {BranchId}, Date {Date}",
                    companyId, branchId, closingDate);
                throw;
            }
        }

    }
}

