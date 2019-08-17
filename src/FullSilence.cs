using System;
using System.Threading;
using System.Windows.Forms;

namespace ESHQSetupStub
	{
	/// <summary>
	/// Класс описывает основное приложение
	/// </summary>
	public class FullSilence
		{
		/// <summary>
		/// Точка входа программы
		/// </summary>
		/// <param name="args">Аргументы командной строки</param>
		[STAThread]
		public static void Main (string[] args)
			{
			// Проверка запуска единственной копии
			bool result;
			Mutex instance = new Mutex (true, ProgramDescription.AssemblyTitle, out result);
			if (!result)
				{
				MessageBox.Show (ProgramDescription.AssemblyTitle + " already started",
					ProgramDescription.AssemblyTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
				}

			// Начальная обработка и отображение лого
			Application.EnableVisualStyles ();
			Application.SetCompatibleTextRenderingDefault (false);

			if (args.Length > 0)
				Application.Run (new FSDrawer (args[0]));
			else
				Application.Run (new FSDrawer (""));
			}
		}
	}
