﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples
{
	class Program
	{
		static void Main(string[] args)
		{
			using (ExampleWindow ex = new ExampleWindow())
			{
				ex.Run();
			}
		}
	}
}