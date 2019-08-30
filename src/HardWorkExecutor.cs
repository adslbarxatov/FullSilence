using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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
		private Bitmap progress, frameGreenGrey, frameBack;
		private int currentPercentage = 0;
		private Graphics g, gp;
		private int currentOffset = 0;
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
			this.BackColor = ProgramDescription.MasterBackColor;
			StateLabel.ForeColor = AbortButton.ForeColor = ProgramDescription.MasterTextColor;
			AbortButton.BackColor = ProgramDescription.MasterButtonColor;

			// Настройка BackgroundWorker
			bw.WorkerReportsProgress = true;		// Разрешает возвраты изнутри процесса
			bw.WorkerSupportsCancellation = true;	// Разрешает завершение процесса

			bw.DoWork += ((HardWorkProcess != null) ? HardWorkProcess : DoWork);
			bw.ProgressChanged += ProgressChanged;
			bw.RunWorkerCompleted += RunWorkerCompleted;

			// Инициализация ProgressBar
			progress = new Bitmap (this.Width - 20, 30);
			g = Graphics.FromHwnd (this.Handle);
			gp = Graphics.FromImage (progress);

			// Формирование стрелок
			Point[] frame = new Point[] {
					new Point (0, 0),
					new Point (this.Width / 4, 0),
					new Point (this.Width / 4 + progress .Height / 2, progress .Height / 2),
					new Point (this.Width / 4, progress .Height),
					new Point (0, progress .Height),
					new Point (progress .Height / 2, progress .Height / 2)
					};

			// Подготовка дескрипторов
			SolidBrush green = new SolidBrush (Color.FromArgb (0, 160, 80)),
				grey = new SolidBrush (Color.FromArgb (160, 160, 160)),
				back = new SolidBrush (this.BackColor);

			frameGreenGrey = new Bitmap (10 * this.Width / 4, progress.Height);
			frameBack = new Bitmap (10 * this.Width / 4, progress.Height);
			Graphics g1 = Graphics.FromImage (frameGreenGrey),
				g2 = Graphics.FromImage (frameBack);

			// Сборка
			for (int i = 0; i < 8; i++)
				{
				for (int j = 0; j < frame.Length; j++)
					{
					frame[j].X += this.Width / 4;
					}

				g1.FillPolygon ((i % 2 == 0) ? green : grey, frame);
				g2.FillPolygon (back, frame);
				}

			// Объём
			for (int i = 0; i < frameGreenGrey.Height; i++)
				{
				Pen p = new Pen (Color.FromArgb (200 - (int)(200.0 * LogoDrawerSupport.Sinus (180.0 * (double)i /
					(double)frameGreenGrey.Height)), this.BackColor));
				g1.DrawLine (p, 0, i, frameGreenGrey.Width, i);
				p.Dispose ();
				}

			// Освобождение ресурсов
			g1.Dispose ();
			g2.Dispose ();
			green.Dispose ();
			grey.Dispose ();
			back.Dispose ();

			// Готово. Запуск
			DrawingTimer.Interval = 1;
			DrawingTimer.Enabled = true;
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
		/// Конструктор. Выполняет указанное действие с указанными параметрами в скрытом режиме
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
			/*if ((e.ProgressPercentage < 0) || (e.ProgressPercentage > 100))
				{
				MainProgress.Style = ProgressBarStyle.Marquee;
				MainProgress.Value = 0;
				}
			else
				{
				MainProgress.Style = ProgressBarStyle.Blocks;
				MainProgress.Value = e.ProgressPercentage;
				}*/
			currentPercentage = e.ProgressPercentage;

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
				System.Threading.Thread.Sleep (500);
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
			DrawingTimer.Enabled = false;

			if (g != null)
				g.Dispose ();
			if (gp != null)
				gp.Dispose ();
			if (progress != null)
				progress.Dispose ();
			if (frameGreenGrey != null)
				frameGreenGrey.Dispose ();
			if (frameBack != null)
				frameBack.Dispose ();
#endif
			}

		// Отрисовка прогресс-бара
		private void DrawingTimer_Tick (object sender, System.EventArgs e)
			{
#if !SIMPLE_HWE
			// Отрисовка текущей позиции
			gp.DrawImage (frameGreenGrey, currentOffset, 0);
			gp.DrawImage (frameBack, -9 * this.Width / 4, 0);
			gp.DrawImage (frameBack, (int)((double)(progress.Width - progress.Height / 2) / 100.0 *
				(double)currentPercentage) - this.Width / 4, 0);

			g.DrawImage (progress, 10, StateLabel.Top + StateLabel.Height + 10);

			// Смещение
			if (currentOffset++ >= -2 * this.Width / 4)
				currentOffset = -4 * this.Width / 4;
#endif
			}
		}
	}
