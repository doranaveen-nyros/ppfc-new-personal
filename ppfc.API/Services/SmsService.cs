using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using static System.Net.Mime.MediaTypeNames;

namespace ppfc.API.Services
{
    public class SmsService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        private readonly string connectionString;

        public SmsService(IConfiguration configuration, IMemoryCache cache, HttpClient httpClient)
        {
            _configuration = configuration;
            _cache = cache;
            _httpClient = httpClient;
            connectionString = GetConnection().ConnectionString;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("conn"));
        }

        public async Task ReceiptUpdateAsync()
        {
            var connectionString = GetConnection().ConnectionString;

            try
            {
                // Fetch top 6 unprocessed receipts
                using var con = new SqlConnection(connectionString);
                using var cmdSelect = new SqlCommand("SELECT TOP 6 receiptid, hpentryid FROM receipt WHERE receiptupdate = 0", con);
                using var adapter = new SqlDataAdapter(cmdSelect);
                var ds = new DataSet();

                await con.OpenAsync();
                adapter.Fill(ds); // SqlDataAdapter still synchronous; for async, use SqlCommand + ExecuteReaderAsync

                if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                    return; // No receipts to process

                // Process each receipt
                using var conUpdate = new SqlConnection(connectionString);
                await conUpdate.OpenAsync();

                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    var hpentryId = Convert.ToInt32(row["hpentryid"]);
                    var receiptId = Convert.ToInt32(row["receiptid"]);

                    using var cmdUpdate = new SqlCommand("sp_Hpentry_ReceiptUpdate", conUpdate)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    cmdUpdate.Parameters.Add(new SqlParameter("@hpentryId", SqlDbType.Int) { Value = hpentryId });
                    cmdUpdate.Parameters.Add(new SqlParameter("@receiptId", SqlDbType.Int) { Value = receiptId });

                    await cmdUpdate.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                // Proper error logging
                Console.WriteLine($"Error updating receipts: {ex.Message}");
                throw; // Optional: rethrow if you want higher-level handling
            }
        }

        public async Task CampChargeInMonthAsync()
        {
            var connectionString = GetConnection().ConnectionString;

            try
            {
                await using var con = new SqlConnection(connectionString);
                await using var cmd = new SqlCommand("SELECT Entries FROM CampChrgInMonth", con);

                await con.OpenAsync();

                var result = await cmd.ExecuteScalarAsync();

                if (result != null)
                {
                    // Store in memory cache instead of Application
                    _cache.Set("CampChrgEntInMonth", result.ToString(), TimeSpan.FromHours(1)); // optional expiration
                }
            }
            catch (Exception ex)
            {
                // Log exception or handle as needed
                Console.WriteLine($"Error in CampChargeInMonthAsync: {ex.Message}");
            }
        }

        public async Task LoadBusinessSMSAsync()
        {
            var connectionString = GetConnection().ConnectionString;

            await using var con = new SqlConnection(connectionString);
            await using var cmd = new SqlCommand("SELECT UserId, Password FROM BusinessSMS WHERE id = 2", con);

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var userId = reader["UserId"].ToString();
                var password = reader["Password"].ToString();

                // Store in memory cache for app-wide usage
                _cache.Set("BusinessSMSID", userId, TimeSpan.FromHours(1));      // optional expiration
                _cache.Set("BusinessSMSPassword", password, TimeSpan.FromHours(1));
            }
        }

        public async Task LoadBusinessSMSSPPFAsync()
        {
            var connectionString = GetConnection().ConnectionString;

            await using var con = new SqlConnection(connectionString);
            await using var cmd = new SqlCommand("SELECT UserId, Password FROM BusinessSMS WHERE id = 3", con);

            await con.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var userId = reader["UserId"].ToString();
                var password = reader["Password"].ToString();

                // Store in memory cache for app-wide usage
                _cache.Set("BusinessSMSIDSPPFin", userId, TimeSpan.FromHours(1));      // optional expiration
                _cache.Set("BusinessSMSPasswordSPPFin", password, TimeSpan.FromHours(1));
            }
        }

        public async Task MoveSentSMSAsync()
        {
            var connectionString = GetConnection().ConnectionString;

            try
            {
                await using var con = new SqlConnection(connectionString);
                await using var cmd = new SqlCommand("sp_Dump_SMSData", con)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Log the exception or handle accordingly
                Console.WriteLine($"Error in MoveSentSMSAsync: {ex.Message}");
            }
        }

        #region SendMessage

        public async Task SendSMSAdminAsync()
        {
            var connectionString = GetConnection().ConnectionString;

            // Retrieve credentials from cache
            if (!_cache.TryGetValue("BusinessSMSID", out string smsUserId) ||
                !_cache.TryGetValue("BusinessSMSPassword", out string smsPassword))
            {
                throw new InvalidOperationException("BusinessSMS credentials not found in cache.");
            }

            await using var con = new SqlConnection(connectionString);
            await con.OpenAsync();

            // Fetch undelivered SMS
            const string selectQuery = @"SELECT TOP 10 SMSId, Message, PhoneNumber, MessageType
                                     FROM SMS
                                     WHERE Status = 'Undelivered' AND SenderId IS NULL";

            await using var cmdSelect = new SqlCommand(selectQuery, con);
            await using var reader = await cmdSelect.ExecuteReaderAsync();

            var messages = new List<SmsMessageDto>();
            while (await reader.ReadAsync())
            {
                messages.Add(new SmsMessageDto
                {
                    SMSId = reader.GetInt32(reader.GetOrdinal("SMSId")),
                    PhoneNumber = reader.GetString(reader.GetOrdinal("PhoneNumber")),
                    Message = reader.GetString(reader.GetOrdinal("Message")),
                    MessageType = reader.GetString(reader.GetOrdinal("MessageType"))
                });
            }

            reader.Close();

            foreach (var msg in messages)
            {
                string gatewayResponse;

                if (msg.PhoneNumber.Length == 10)
                {
                    // Call SMS gateway
                    gatewayResponse = await SendSMSUsingBSAsync(smsUserId, smsPassword, msg.PhoneNumber, msg.Message, "normal");

                    if (gatewayResponse.Contains("S."))
                    {
                        await UpdateSmsStatusAsync(con, msg.SMSId, "Delivered");
                    }
                }
                else
                {
                    await UpdateSmsStatusAsync(con, msg.SMSId, "UnSent");
                }
            }
        }

        public async Task SendSMSAdminSPPFinAsync()
        {
            var connectionString = GetConnection().ConnectionString;

            // Get SPPFIN credentials from cache
            if (!_cache.TryGetValue("BusinessSMSIDSPPFin", out string userId) ||
                !_cache.TryGetValue("BusinessSMSPasswordSPPFin", out string password))
            {
                throw new InvalidOperationException("SPPFIN SMS credentials not found in cache.");
            }

            await using var con = new SqlConnection(connectionString);
            await con.OpenAsync();

            // Fetch top 10 undelivered SPPFIN messages
            const string selectQuery = @"
            SELECT TOP 10 SMSId, Message, PhoneNumber, MessageType
            FROM SMS
            WHERE Status = 'Undelivered' AND SenderId = 'SPPFIN'";

            await using var cmdSelect = new SqlCommand(selectQuery, con);
            await using var reader = await cmdSelect.ExecuteReaderAsync();

            var messages = new List<SmsMessageDto>();
            while (await reader.ReadAsync())
            {
                messages.Add(new SmsMessageDto
                {
                    SMSId = reader.GetInt32(reader.GetOrdinal("SMSId")),
                    PhoneNumber = reader.GetString(reader.GetOrdinal("PhoneNumber")),
                    Message = reader.GetString(reader.GetOrdinal("Message")),
                    MessageType = reader.GetString(reader.GetOrdinal("MessageType"))
                });
            }

            reader.Close();

            foreach (var msg in messages)
            {
                if (msg.PhoneNumber.Length == 10)
                {
                    // Send SMS via SPPFIN gateway
                    var gatewayResponse = await SendSMSUsingBS_SPPFINAsync(userId, password, msg.PhoneNumber, msg.Message, "normal");

                    if (gatewayResponse.Contains("S."))
                    {
                        await UpdateSmsStatusAsync(con, msg.SMSId, "Delivered");
                    }
                }
                else
                {
                    await UpdateSmsStatusAsync(con, msg.SMSId, "UnSent");
                }
            }
        }

        private static async Task UpdateSmsStatusAsync(SqlConnection con, int smsId, string status)
        {
            const string updateQuery = "UPDATE SMS SET Status = @Status WHERE SMSId = @SMSId";
            await using var cmdUpdate = new SqlCommand(updateQuery, con);
            cmdUpdate.Parameters.AddWithValue("@Status", status);
            cmdUpdate.Parameters.AddWithValue("@SMSId", smsId);
            await cmdUpdate.ExecuteNonQueryAsync();
        }

        // DTO for SMS messages
        public class SmsMessageDto
        {
            public int SMSId { get; set; }
            public string PhoneNumber { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string MessageType { get; set; } = string.Empty;
        }

        #endregion

        #region Messaging after Login

        public async Task<string> GetContactNumberAsync()
        {
            try
            {
                await using var con = new SqlConnection(connectionString);
                await using var cmd = new SqlCommand("SELECT TOP 1 PhoneNumber FROM ContactPhoneNo", con);

                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();

                return result?.ToString() ?? string.Empty; // Return empty string if null
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetContactNumberAsync: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task CallInstSMSAsync(int branchId, int companyId, string companyName, string userName)
        {

            try
            {
                // Current time in IST
                DateTime nowIST = DateTime.UtcNow.AddHours(5.5);
                DateTime cronJobTime = new DateTime(nowIST.Year, nowIST.Month, nowIST.Day, 9, 0, 0); // 9:00 AM today

                if (nowIST < cronJobTime)
                    return; // Nothing to do yet

                DateTime cronJobLastDate = nowIST.AddDays(-1);

                await using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();

                    // Get last cron job date
                    await using (var cmdCronDate = new SqlCommand(
                        "SELECT TOP 1 Date FROM CronJob WHERE CompanyId = @CompanyId ORDER BY CronId DESC", con))
                    {
                        cmdCronDate.Parameters.AddWithValue("@CompanyId", companyId);
                        var objOldDate = await cmdCronDate.ExecuteScalarAsync();
                        if (objOldDate != null)
                        {
                            cronJobLastDate = Convert.ToDateTime(objOldDate);
                        }
                    }

                    // If last cron job date is before today, insert new entry
                    if (cronJobLastDate.Date < nowIST.Date)
                    {
                        cronJobLastDate = cronJobLastDate.AddDays(1);

                        await using (var cmdCron = new SqlCommand(
                            "INSERT INTO CronJob (Date, CompanyId) VALUES (@Date, @CompanyId)", con))
                        {
                            cmdCron.Parameters.AddWithValue("@Date", cronJobLastDate.Date);
                            cmdCron.Parameters.AddWithValue("@CompanyId", companyId);

                            await cmdCron.ExecuteNonQueryAsync();
                        }
                    }

                    var contactPhoneNo = await GetContactNumberAsync();

                    // Call your method to process entries / send SMS
                    await GetHPEntryDueDetailsAsync(branchId,companyId,companyName,userName, contactPhoneNo, cronJobLastDate);
                }
            }
            catch (Exception ex)
            {
                // Log the exception or handle accordingly
                Console.WriteLine($"Error in CallInstSMSAsync: {ex.Message}");
            }
        }

        public async Task GetHPEntryDueDetailsAsync(int branchId,int companyId,string companyName,string userName,string contactPhoneNo,DateTime sendDate)
        {
            try
            {
                int status = await CronJobAsync(branchId, companyId, companyName, userName, sendDate, contactPhoneNo);


                if (status == 1)
                {
                    await SendSMS("INSTALMENT"); //changed from Installments to INSTALMENT
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetHPEntryDueDetailsAsync: {ex.Message}");
            }
        }

        public async Task<int> CronJobAsync(int branchId, int companyId, string companyName, string userName, DateTime cronJobDate, string contactPhoneNo)
        {
            int status = 0;

            try
            {
                await using var con = new SqlConnection(connectionString);
                await con.OpenAsync();

                // Delete previous dummy records
                await using (var cmdDelete = new SqlCommand("DELETE FROM Dummy_SMSCronJob_Table", con))
                {
                    await cmdDelete.ExecuteNonQueryAsync();
                }

                // Insert new dummy SMS cron job records
                string dumpQuery = @"
            INSERT INTO Dummy_SMSCronJob_Table
            SELECT 
                HPEntry_Id,
                Name,
                Date,
                DATEDIFF(MONTH, Date, @ReceiptDate) AS CompletedInstal,
                Installments,
                InstalAmount,
                FinancedAmount,
                (
                    (SELECT COALESCE(SUM(ReceiptAmount), 0) FROM vw_Receipt r 
                     WHERE r.HPEntry_Id = HPE.HPEntry_Id AND Date <= @ReceiptDate AND CompanyId = @CompanyId)
                    +
                    (SELECT COALESCE(SUM(Amount), 0) FROM vw_HPReturn r 
                     WHERE r.HPEntry_Id = HPE.HPEntry_Id AND Date <= @ReceiptDate AND CompanyId = @CompanyId)
                ) AS PaidAmount,
                MobileNumber
            FROM vw_CollectionTarget HPE
            WHERE DAY(Date) = @Day
              AND DATEADD(MONTH, Installments, Date) >= @ReceiptDate
              AND CompanyId = @CompanyId";

                await using (var cmdInsert = new SqlCommand(dumpQuery, con))
                {
                    cmdInsert.Parameters.AddWithValue("@ReceiptDate", cronJobDate.Date);
                    cmdInsert.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdInsert.Parameters.AddWithValue("@Day", cronJobDate.Day);

                    await cmdInsert.ExecuteNonQueryAsync();
                }

                // Select pending accounts
                string query = @"
            SELECT 
                (CompletedInstal - FLOOR(PaidAmount / InstalAmount)) AS PendingMonths,
                HPEntryId,
                Name,
                ((CompletedInstal * InstalAmount) - PaidAmount) AS PendingAmount,
                Installments,
                MobileNumber
            FROM Dummy_SMSCronJob_Table
            WHERE CEILING(((CompletedInstal * InstalAmount) - PaidAmount) / InstalAmount) > 0";

                await using (var cmdCronJobSMS = new SqlCommand(query, con))
                {
                    var dsCronJobSMS = new DataSet();
                    await using (var reader = await cmdCronJobSMS.ExecuteReaderAsync())
                    {
                        var dt = new DataTable("PendingAccounts");
                        dt.Load(reader); // DataTable.Load works synchronously, but reader is async
                        dsCronJobSMS.Tables.Add(dt);
                    }

                    if (dsCronJobSMS.Tables["PendingAccounts"].Rows.Count > 0)
                    {
                        // Call your async version of sending SMS
                        await SendCronJobSMSAsync(dsCronJobSMS, branchId, companyId, companyName, userName, contactPhoneNo);
                    }
                }

                status = 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CronJobAsync: {ex.Message}");
            }

            return status;
        }

        public async Task SendCronJobSMSAsync(DataSet dsCronJobSMS, int branchId, int companyId, string companyName, string userName, string contactPhNo)
        {
            try
            {
                await using var con = new SqlConnection(connectionString);
                await con.OpenAsync();

                string teluguCompanyName = getCompanyName(companyId);
                string companyNum = Make_NCPR_Format(contactPhNo);
                DateTime date = DateTime.UtcNow.AddHours(5.5);

                foreach (DataRow drHP in dsCronJobSMS.Tables["PendingAccounts"].Rows)
                {
                    if (Convert.ToDecimal(drHP["PendingAmount"]) > 1)
                    {
                        string teluguMessage = $"మీ ఎకౌంటు నెంబర్ : {drHP["HPEntryId"]}, " +
                            $"{drHP["PendingMonths"]} వాయుదాలు  బాకి ఉన్నది, బాకీ యమౌంట్ {drHP["PendingAmount"]}/-, " +
                            $"మీరు వెంటనే కట్ట గలరు,వివరములు కొరకు సంప్రదించండి : {companyNum} ,ఇట్లు {teluguCompanyName}";

                        await using var cmdInsertSMS = new SqlCommand("sp_Insert_SMS_CronJob", con)
                        {
                            CommandType = CommandType.StoredProcedure
                        };

                        cmdInsertSMS.Parameters.AddWithValue("@Receiver", drHP["Name"].ToString());
                        cmdInsertSMS.Parameters.AddWithValue("@PhoneNumber", drHP["MobileNumber"].ToString());
                        cmdInsertSMS.Parameters.AddWithValue("@Message", teluguMessage);
                        cmdInsertSMS.Parameters.AddWithValue("@Date", date.ToShortDateString());
                        cmdInsertSMS.Parameters.AddWithValue("@UserName", userName);
                        cmdInsertSMS.Parameters.AddWithValue("@Time", date.ToShortTimeString());
                        cmdInsertSMS.Parameters.AddWithValue("@CompanyId", companyId);
                        cmdInsertSMS.Parameters.AddWithValue("@BranchId", branchId);

                        await cmdInsertSMS.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendCronJobSMSAsync: {ex.Message}");
            }
        }

        public string getCompanyName(int companyId)
        {
            string teluguCompanyName = "";
            if (companyId == 1)
            {
                teluguCompanyName = "శ్రీ విష్ణు ప్రియ ఫైనాన్సు.";
            }
            else if (companyId == 2)
            {
                teluguCompanyName = "శ్రీ పద్మ ప్రియ ఫైనాన్సు కార్పొరేషన్.";
            }
            else
            {
                teluguCompanyName = "శ్రీ పద్మ ప్రియ ఫైనాన్సు వి ఎస్  పి.";
            }

            return teluguCompanyName;
        }

        public string Make_NCPR_Format(string CompanyNum)
        {
            string Num = string.Empty;
            Num = CompanyNum.Insert(5, "_");
            return Num;
        }

        public async Task SendSMS(string messageType)
        {
            await using var con = new SqlConnection(connectionString);

            try
            {
                if (con.State == ConnectionState.Closed)
                    con.Open();

                using (SqlCommand cmdMsgs = new SqlCommand(
                    "SELECT TOP 6 SMSId, Message, PhoneNumber FROM SMS WHERE Status='Undelivered' AND MessageType=@MessageType AND SenderId IS NULL", con))
                {
                    cmdMsgs.Parameters.AddWithValue("@MessageType", messageType);

                    DataSet dsMsgs = new DataSet();
                    using (SqlDataAdapter da = new SqlDataAdapter(cmdMsgs))
                    {
                        da.Fill(dsMsgs, "Messages");
                    }

                    // Retrieve credentials from cache
                    var userId = _cache.Get<string>("BusinessSMSID");
                    var password = _cache.Get<string>("BusinessSMSPassword");

                    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
                    {
                        throw new InvalidOperationException("Business SMS credentials are missing in cache.");
                    }

                    foreach (DataRow dr in dsMsgs.Tables["Messages"].Rows)
                    {
                        string mobileNo = dr["PhoneNumber"].ToString();
                        string textMsg = dr["Message"].ToString();
                        string gatewayResponse = "";

                        if (mobileNo.Length == 10)
                        {
                            gatewayResponse = await SendMessage(userId, password, mobileNo, textMsg);

                            if (gatewayResponse.Contains("S."))
                            {
                                await UpdateSmsStatusAsync(con, Convert.ToInt32(dr["SMSId"]), "Delivered");
                            }
                        }
                        else
                        {
                            await UpdateSmsStatusAsync(con, Convert.ToInt32(dr["SMSId"]), "UnSent");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        private async Task<string> SendMessage(string userId, string password, string mobileNo, string textMsg)
        {
            string gatewayResponse = "";
            int msgLength = textMsg.Length;

            if (msgLength <= 160)
            {
                gatewayResponse = await SendSMSUsingBSAsync(userId, password, mobileNo, textMsg, "normal");
            }
            else
            {
                int msgCount = (int)Math.Ceiling(msgLength / 160.0);
                for (int i = 0; i < msgCount; i++)
                {
                    string shortSMS = (i + 1 == msgCount)
                        ? textMsg.Substring(i * 160, msgLength - i * 160)
                        : textMsg.Substring(i * 160, 160);

                    gatewayResponse = await SendSMSUsingBSAsync(userId, password, mobileNo, shortSMS, "normal");
                }
            }

            return gatewayResponse;
        }

        #endregion

        #region Bash Messaging

        public async Task<string> SendSMSUsingBSAsync(string user, string password, string recipient, string messageText, string smsType)
        {
            var baseUrl = "http://bhashsms.com/api/sendmsg.php";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["user"] = user;
            query["pass"] = password;
            query["sender"] = "SVPFIN";
            query["phone"] = recipient;
            query["text"] = messageText;
            query["priority"] = "ndnd";
            query["stype"] = smsType;

            var url = $"{baseUrl}?{query}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode(); // Throws if not 2xx

                var result = await response.Content.ReadAsStringAsync();
                return result;
            }
            catch (HttpRequestException ex)
            {
                // Log the error or handle it accordingly
                Console.WriteLine($"Error sending SMS: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        private async Task<string> SendSMSUsingBS_SPPFINAsync(string user, string password, string recipient, string messageText, string smsType)
        {
            var baseUrl = "http://bhashsms.com/api/sendmsg.php";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["user"] = user;
            query["pass"] = password;
            query["sender"] = "SPPFIN";
            query["phone"] = recipient;
            query["text"] = messageText;
            query["priority"] = "ndnd";
            query["stype"] = smsType;

            var url = $"{baseUrl}?{query}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error sending SPPFIN SMS: {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }

        #endregion

    }
}
