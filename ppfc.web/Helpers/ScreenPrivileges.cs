using System;
using System.Collections.Generic;


namespace ppfc.web.Helpers
{
    public static class ScreenPrivileges
    {
        // Dictionary mapping screen name to allowed privileges

            public record Restriction(bool DisableAdd, bool DisableEdit, bool DisableDelete, bool DisableView);

            private static readonly Dictionary<string, Restriction> restrictions = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Bank Statement Report"] = new(true, true, true, false),
                ["Save Bank Data To Receipts"] = new(false, true, true, false),
                ["Save Bank Data"] = new(false, false, true, false),
                ["Audit WorkSheet Rectification"] = new(false, true, true, false),
                ["Audit Receipt Edit"] = new(false, true, true, false),
                ["Audit Salary"] = new(false, true, false, false),
                ["Block Save Bank Data"] = new(true, false, false, false),
                ["Interest"] = new(true, false, false, false),
                ["User Settings"] = new(true, false, true, false),
                ["Role Settings"] = new(true, false, true, false),
                ["SMS Settings"] = new(true, false, true, false),
                ["SMS"] = new(false, true, false, false),
                ["Audit Address"] = new(false, true, true, false),
                ["Audit Vehicle"] = new(false, true, true, false),
                ["Audit Finance"] = new(false, true, true, false),
                ["Audit RTO Transferred"] = new(false, true, true, false),
                ["HP Return Audit"] = new(false, true, true, false),
                ["CBook Audit"] = new(false, true, true, false),
                ["Audit Identity"] = new(false, true, true, false),
                ["Audit DOC"] = new(false, true, true, false),
                ["Audit Expense"] = new(false, true, true, false),
                ["Audit Back Logs"] = new(true, true, true, false),
                ["Vehicle Search"] = new(true, true, true, false),
                ["UpLoad Photo"] = new(false, true, true, false),
                ["Report Account Close"] = new(false, true, false, false),
                ["Expense Voucher Report"] = new(true, true, true, false),
                ["Party Credits Report"] = new(true, true, true, false),
                ["Pending Account Report"] = new(true, true, true, false),
                ["Clearance Basket"] = new(false, true, true, false),
                ["AC Close"] = new(false, true, false, false),
                ["Address Change"] = new(true, false, true, false),
                ["RTO Agent Transaction"] = new(false, false, true, false),
                ["Attendence Report"] = new(true, true, true, false),
                ["Employee Salary Report"] = new(true, false, true, false),
                ["Loan Deduction Report"] = new(true, true, true, false),
                ["Loan Pending Report"] = new(true, true, true, false),
                ["Salary Report"] = new(true, true, true, false),
                ["SA Deduction Report"] = new(true, true, true, false),
                ["SA Pending Report"] = new(true, true, true, false),
                ["HP Finance Report"] = new(true, true, true, false),
                ["Party Debit Report"] = new(true, true, true, false),
                ["HP Register Report"] = new(true, true, true, false),
                ["Receipt ODReceipt Report"] = new(true, true, true, false),
                ["ACClose HPReturn Report"] = new(true, true, true, false),
                ["Camp Charges Report"] = new(true, true, true, false),
                ["DOCPaid NOTPaid Report"] = new(true, true, true, false),
                ["OD Calculation Report"] = new(true, true, true, false),
                ["Refinance OD Calculation"] = new(true, true, true, false),
                ["HP Dues Report"] = new(true, true, true, false),
                ["HP Refinance Dues Report"] = new(true, true, true, false),
                ["Collection Target Report"] = new(true, true, true, false),
                ["RTOTrans NONTrans Report"] = new(true, true, true, false),
                ["Vehicle Seize Report"] = new(true, true, true, false),
                ["CBook Folder Report"] = new(true, true, true, false),
                ["Cash Book"] = new(true, true, true, false),
                ["Balance Sheet"] = new(true, true, true, false),
                ["Profit Loss Report"] = new(true, true, true, false),
                ["Main Account Verification"] = new(true, true, true, false),
                ["HPEntry Invoice Report"] = new(true, true, true, false),
                ["Tele Caller Reportt"] = new(false, false, true, false),
                ["Reprint"] = new(true, true, true, false),
                ["Account Pending Basket"] = new(false, false, true, false),
                ["Request Basket"] = new(false, true, true, false),
                ["UPLoad Documents"] = new(false, true, false, false),
                ["Contact PH No"] = new(true, false, true, false),
                ["Change Area"] = new(true, false, true, false),
                ["Change Vehicle"] = new(true, false, true, false),
                ["Funding Report"] = new(true, true, true, false),
                ["Delete Records"] = new(true, true, false, false),
                ["Funding Restriction"] = new(false, false, true, false),
                ["Camp Charge Entries In Month"] = new(true, false, true, false),
                ["Bank Report"] = new(true, true, true, false),
                ["Overal"] = new(true, true, true, false),
                ["Bank Tally"] = new(true, true, true, false),
                ["Verificationn Report"] = new(true, true, true, false),
                ["Overall Finance Report"] = new(true, true, true, false),
                ["Sales Manager"] = new(false, true, true, false),
                ["Audit Car"] = new(true, true, true, false),
                ["Audit FR"] = new(false, true, true, false),
                ["ATC Audit"] = new(false, true, true, false),
                ["ATC Report"] = new(true, true, true, false),
                ["ATC Delete"] = new(true, true, false, false)
            };

            public static Restriction GetRestrictions(string screenName)
            {
                return restrictions.TryGetValue(screenName.Trim(), out var r)
                    ? r
                    : new Restriction(false, false, false, false);
            }

            public static bool HasRestriction(string screenName)
            {
                return restrictions.ContainsKey(screenName.Trim());
            }

}
}
