using System;
<<<<<<< HEAD
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

=======
using System.Windows.Forms;
>>>>>>> 8eaa188f6afe4d917cc36d79961561a0a929aa6d

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
