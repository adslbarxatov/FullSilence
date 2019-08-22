using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace ESHQSetupStub
	{
	/// <summary>
	/// Класс предоставляет интерфейс визуализации прогресса установки/удаления программы
	/// </summary>
	public partial class HardWorkExecutor:Form
		{
#if !SIMPLE_HWE
		// Переменные
		private bool allowClose = false;
#endif

		/// <summary>
		/// Возвращает объект-обвязку исполняемого процесса
		/// </summary>
		public BackgroundWorker Worker
			{
			get
				{
				return bw;
				}
			}
		private BackgroundWorker bw = new BackgroundWorker ();

		/// <summary>
		/// Возвращает результат установки/удаления
		/// </summary>
		public int ExecutionResult
			{
			get
				{
				return executionResult;
				}
			}
		private int executionResult = 0;

		// Переменные
		private List<string> argument = new List<string> ();

#if !SIMPLE_HWE
		/// <summary>
		/// Конструктор. Выполняет настройку и запуск процесса установки/удаления
		/// </summary>
		/// <param name="HardWorkProcess">Процесс, выполняющий установку/удаление</param>
		/// <param name="Mode">Режим установки/удаления файлов</param>
		/// <param name="SetupPath">Путь установки/удаления</param>
		/// <param name="Uninstall">Флаг удаления ранее установленных файлов</param>
		public HardWorkExecutor (DoWorkEventHandler HardWorkProcess, string SetupPath, ArchiveOperator.SetupModes Mode, bool Uninstall)
			{
			// Инициализация
			InitializeComponent ();
			argument.Add (SetupPath);
			argument.Add (((int)Mode).ToString ());
			argument.Add (Uninstall.ToString ());

			// Настройка контролов
			this.BackColor = MainProgress.BackColor = ProgramDescription.MasterBackColor;
			StateLabel.ForeColor = AbortButton.ForeColor = MainProgress.ForeColor = ProgramDescription.MasterTextColor;
			AbortButton.BackColor = ProgramDescription.MasterButtonColor;

			// Настройка BackgroundWorker
			bw.WorkerReportsProgress = true;		// Разрешает возвраты изнутри процесса
			bw.WorkerSupportsCancellation = true;	// Разрешает завершение процесса

			bw.DoWork += ((HardWorkProcess != null) ? HardWorkProcess : DoWork);
			bw.ProgressChanged += ProgressChanged;
			bw.RunWorkerCompleted += RunWorkerCompleted;

			// Запуск
			this.ShowDialog ();
			}

		/// <summary>
		/// Конструктор. Выполняет проверку доступной версии обновления в скрытом режиме
		/// </summary>
		/// <param name="HardWorkProcess">Выполняемый процесс</param>
		/// <param name="PackageVersion">Версия пакета развёртки для сравнения</param>
		public HardWorkExecutor (DoWorkEventHandler HardWorkProcess, string PackageVersion)
			{
			// Настройка BackgroundWorker
			bw.WorkerReportsProgress = true;		// Разрешает возвраты изнутри процесса
			bw.WorkerSupportsCancellation = true;	// Разрешает завершение процесса

			bw.DoWork += ((HardWorkProcess != null) ? HardWorkProcess : DoWork);
			bw.RunWorkerCompleted += RunWorkerCompleted;

			// Запуск
			bw.RunWorkerAsync (PackageVersion);
			}
#endif

		/// <summary>
		/// Конструктор. Выполняет указанное действие с указанными параметрами
		/// </summary>
		/// <param name="HardWorkProcess">Выполняемый процесс</param>
		/// <param name="Parameters">Передаваемые параметры выполнения</param>
		public HardWorkExecutor (DoWorkEventHandler HardWorkProcess, object Parameters)
			{
			// Настройка BackgroundWorker
			bw.WorkerReportsProgress = true;		// Разрешает возвраты изнутри процесса
			bw.WorkerSupportsCancellation = true;	// Разрешает завершение процесса

			bw.DoWork += ((HardWorkProcess != null) ? HardWorkProcess : DoWork);
			bw.RunWorkerCompleted += RunWorkerCompleted;

			// Запуск
			bw.RunWorkerAsync (Parameters);
			}

		// Метод запускает выполнение процесса
		private void HardWorkExecutor_Shown (object sender, System.EventArgs e)
			{
			bw.RunWorkerAsync (argument);
			}

#if !SIMPLE_HWE
		// Метод обрабатывает изменение состояния процесса
		private void ProgressChanged (object sender, ProgressChangedEventArgs e)
			{
			// Обновление ProgressBar
			if ((e.ProgressPercentage < 0) || (e.ProgressPercentage > 100))
				{
				MainProgress.Style = ProgressBarStyle.Marquee;
				MainProgress.Value = 0;
				}
			else
				{
				MainProgress.Style = ProgressBarStyle.Blocks;
				MainProgress.Value = e.ProgressPercentage;
				}

			StateLabel.Text = (string)e.UserState;
			}
#endif

		// Метод обрабатывает завершение процесса
		private void RunWorkerCompleted (object sender, RunWorkerCompletedEventArgs e)
			{
			// Завершение работы исполнителя
			try
				{
				executionResult = int.Parse (e.Result.ToString ());
				}
			catch
				{
				executionResult = -100;
				}
			bw.Dispose ();

			// Закрытие окна
#if !SIMPLE_HWE
			allowClose = true;
			this.Close ();
#endif
			}

		// Кнопка инициирует остановку процесса
		private void AbortButton_Click (object sender, System.EventArgs e)
			{
			bw.CancelAsync ();
			}

		// Образец метода, выполняющего длительные вычисления
		private void DoWork (object sender, DoWorkEventArgs e)
			{
			// Собственно, выполняемый процесс
			for (int i = 0; i < 100; i++)
				{
				System.Threading.Thread.Sleep (50);
				((BackgroundWorker)sender).ReportProgress (i);	// Возврат прогресса

				// Завершение работы, если получено требование от диалога
				if (((BackgroundWorker)sender).CancellationPending)
					{
					e.Cancel = true;
					return;
					}
				}

			// Завершено
			e.Result = null;
			}

		// Закрытие формы
		private void HardWorkExecutor_FormClosing (object sender, FormClosingEventArgs e)
			{
#if !SIMPLE_HWE
			e.Cancel = !allowClose;
#endif
			}
		}
	}
