using CefSharp.WinForms;
using StealthBrowser.Controls.BrowserTabStrip;
using System;
using System.Drawing;

namespace StealthBrowser.Model {
	/// <summary>
	/// POCO created for holding data per tab
	/// </summary>
	internal class BrowserTab {

		public bool IsOpen;

		public string OrigURL;
		public string CurURL;
		public string Title;

		public string RefererURL;

		public DateTime DateCreated;

		public BrowserTabPage Tab;
		public ChromiumWebBrowser Browser;

		public Bitmap FavIcon;

	}
}
