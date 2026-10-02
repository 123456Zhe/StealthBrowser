using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace StealthBrowser.Managers {
	/// <summary>
	/// embedding the resource using the Visual Studio designer results in a blurry icon.
	/// so this is the best way to get a non-blurry icon for Windows 8+ apps.
	/// </summary>
	internal static class IconManager {

		public static Assembly assembly = null;

		public static void Init(Form form) {
			if (assembly == null) assembly = Assembly.GetAssembly(typeof(MainForm));
			form.Icon = new Icon(GetResourceStream("stealthbrowser.ico"), new Size(64, 64));
		}

		public static Stream GetResourceStream(string filename, bool withNamespace = true) {
			try {
				return assembly.GetManifestResourceStream("StealthBrowser.Resources." + filename);
			}
			catch (System.Exception ex) { }
			return null;
		}

	}
}
