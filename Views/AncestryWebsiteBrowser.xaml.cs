using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace AncestryDnaClustering
{
	public partial class AncestryWebsiteBrowser : Window
	{
        private readonly Uri _baseAddress;

		public AncestryWebsiteBrowser(Uri baseAddress, int width, int height)
		{
            _baseAddress = baseAddress;

			InitializeComponent();

			Width = width;
			Height = height;
            WebBrowser.Navigate(new Uri(_baseAddress, "account/signin"));
		}

        public string Cookie { get; private set; }

		private void WebBrowser_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
		{
            // If successfully navigating back to the home page, capture whatever cookies were set during the login process.
            // This is fragile; depends on Ancestry redirecting to the home page in all cases.
			if (e.Uri.OriginalString == _baseAddress.ToString())
			{
                Cookie = GetCookie(e.Uri.OriginalString);

                // Immediately sign out so that the cookies are not persisted.
                WebBrowser.Navigate(new Uri(_baseAddress, "account/signout"));

                // Close the window so that no further navigation is possible.
                Close();
			}
        }
        private const int INTERNET_COOKIE_HTTPONLY = 0x00002000;

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetGetCookieEx(
           string url,
           string cookieName,
           StringBuilder cookieData,
           ref int size,
           int flags,
           IntPtr pReserved);

        private static string GetCookie(string url)
        {
            var size = 0;

            InternetGetCookieEx(url, null, null, ref size, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero);

            if (size <= 0)
            {
                return null;
            }

            var sb = new StringBuilder(size);

            return InternetGetCookieEx(url, null, sb, ref size, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero) ? sb.ToString() : null;
        }
    }
}
