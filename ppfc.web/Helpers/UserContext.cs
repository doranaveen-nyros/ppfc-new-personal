namespace ppfc.web.Helpers
{
    public class UserContext
    {
        public int CompanyId { get; private set; }
        public int UserId { get; private set; }
        public string UserName { get; private set; }

        public void SetUser(int companyId, int userId, string userName)
        {
            CompanyId = companyId;
            UserId = userId;
            UserName = userName;
        }
    }
}
