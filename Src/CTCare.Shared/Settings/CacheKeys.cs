namespace CTCare.Shared.Settings
{
    public class CacheKeys
    {
        public static string Phone_Ip_FailKey(string phone, string ip) => $"otp_fail:|phone:{phone}|ip:{ip}";
        public static string Phone_Ip_LockKey(string phone, string ip) => $"otp_lock:|phone:{phone}|ip:{ip}";
        public static string Phone_OtpKey(string phone) => $"otp:|phone:{phone}";
        public static string Email_Ip_FailKey(string phone, string ip) => $"otp_fail:|phone:{phone}|ip:{ip}";
        public static string Email_Ip_LockKey(string phone, string ip) => $"otp_lock:|phone:{phone}|ip:{ip}";
        public static string Email_OtpKey(string phone) => $"otp:|phone:{phone}";

        public const string LockValue = "LOCKED";
    }
}
