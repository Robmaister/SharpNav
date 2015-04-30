using System;
<<<<<<< HEAD
using System.Windows.Forms;
=======
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

>>>>>>> b823b5591cae9f08e70dee21b1d92fe710755b21

namespace SharpNav.GUI
{
	class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new ConfigurationForm());
		}
	}
}
