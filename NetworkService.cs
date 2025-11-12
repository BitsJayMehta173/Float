using System.Net.NetworkInformation;

namespace FloatingReminder
{
    public static class NetworkService
    {
        // This is a fast, simple check.
        // It's not 100% foolproof (it might say "connected" if you're on WiFi
        // but the router has no internet), but it's the best synchronous
        // check we can do without a slow, complex "ping" test.
        public static bool IsNetworkAvailable()
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }
    }
}