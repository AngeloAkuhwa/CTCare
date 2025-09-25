namespace CTCare.Shared.Settings
{
    /// <summary>
    /// Centralized cache key builder for auth throttling and OTP storage.
    /// </summary>
    public static class CacheKeys
    {
        // Login attempt throttling (per identifier + IP)

        public static string Email_Ip_FailKey(string email, string ip)
            => $"auth:fail:email:{Norm(email)}:ip:{Norm(ip)}";

        public static string Email_Ip_LockKey(string email, string ip)
            => $"auth:lock:email:{Norm(email)}:ip:{Norm(ip)}";

        public static string Phone_Ip_FailKey(string phone, string ip)
            => $"auth:fail:phone:{Norm(phone)}:ip:{Norm(ip)}";

        public static string Phone_Ip_LockKey(string phone, string ip)
            => $"auth:lock:phone:{Norm(phone)}:ip:{Norm(ip)}";

        // Login OTP keys (email/SMS)

        public static string Email_OtpKey(string email)
            => $"otp:login:{Norm(email)}";

        public static string Phone_OtpKey(string phone)
            => $"otp:login:phone:{Norm(phone)}";

        // Tag helper. lets you invalidate all OTPs for an email quickly
        public static string Email_OtpTag(string email)
            => $"otp:login:{Norm(email)}";

        public const string LockValue = "LOCKED";

        public static string ConfirmEmail_Cooldown(string email) => $"confirm_cd:|email:{email}";


        private static string Norm(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();


        public static string BalanceKey(Guid employeeId, int year) => $"leave:balance:{employeeId}:{year}";
        public static string MyListPrefix(Guid employeeId) => $"leave:my:{employeeId}:";

        public static string TeamListPrefix(Guid managerId) => $"leave:team:{managerId:N}";

        public static string RequestDetailsKey(Guid requestId) => $"leave:req:{requestId:N}";

        public static string ActiveLeaveTypes => "leave:types:active";

        public static string MyCountsKey(Guid employeeId) => $"leave:my:{employeeId}:counts";
    }
}
