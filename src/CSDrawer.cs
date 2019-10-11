using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

// Классы
namespace ESHQSetupStub
	{
	/// <summary>
	/// Класс обеспечивает отображение визуализации проекта
	/// </summary>
	public partial class CSDrawer:Form
		{
		// Переменные и константы
		// Главные
		private Phases currentPhase = Phases.LayersPrecache;	// Текущая фаза отрисовки
		private uint steps = 0;									// Счётчик шагов отрисовки
		private const uint generalStep = 3;						// Длительность главного шага отображения
		private string commandLine;								// Параметры командной строки
		private bool debugMode = false;							// Режим отладки скрипта
		private const uint frameTypesCount = 6;					// Количество доступных типов субокон

		// Текст
		private List<List<LogoDrawerString>> mainStringsSet = new List<List<LogoDrawerString>> ();	// Тексты для отображения
		private Point drawPoint;								// Текущая позиция отрисовки текста
		private const int lineLeft = 20,						// Начало и конец строки текста расширенного режима
			lineRight = 20,
			lineTop = 20;										// Начало блока текста расширенного режима

		// Графика
		private List<LogoDrawerLayer> layers = new List<LogoDrawerLayer> ();		// Базовые слои изображения
		private uint savingLayersCounter = 0,					// Счётчик сохранений
			borderSize = 5,										// Ширина границы субокна
			currentFrame = 0;									// Текущее субокно
		private bool answersMode = false,						// Замена окна вывода окном ответов
			showOutput = true;									// Флаг наличия поля вывода программы
		private double commentFramePart = 0.3;					// Процент поля, занимаемый окном комментариев
		private const double titlesFramePart = 0.07;			// Высота поля заголовков субокон

		private Graphics gr;									// Объекты-отрисовщики
		private List<List<SolidBrush>> brushes = new List<List<SolidBrush>> ();
		private List<Font> fonts = new List<Font> ();

		// Видео
		private VideoManager vm = new VideoManager ();			// Видеофайл (балластная инициализация)

		// Возможные фазы отрисовки
		private enum Phases
			{
			// Подготовка слоёв
			LayersPrecache = 1,

			// Первый фрагмент лого
			LogoFragment1 = 2,

			// Пауза после лого
			LogoIntermission = 3,

			// Затенение лого
			LogoFading1 = 4,

			LogoFading2 = 5,

			// Отрисовка основного текста
			MainTextDrawing = 6,

			// Сброс текста
			MainTextFading = 7,

			// Конечное затенение
			EndingFading1 = 8,

			// Завершение и конечная остановка
			Finished = 9,

			End = 10
			}

		/// <summary>
		/// Конструктор. Инициализирует экземпляр отрисовщика
		/// </summary>
		/// <param name="CommandLine">Параметры командной строки</param>
		public CSDrawer (string CommandLine)
			{
			// Инициализация
			commandLine = CommandLine;
			InitializeComponent ();
			}

		private void CSDrawer_Shown (object sender, EventArgs e)
			{
			// Если запрос границ экрана завершается ошибкой, отменяем отображение
			this.Left = this.Top = 0;
			try
				{
				this.Width = Screen.PrimaryScreen.Bounds.Width;
				this.Height = Screen.PrimaryScreen.Bounds.Height;
				}
			catch
				{
				this.Close ();
				return;
				}
			this.Text = ProgramDescription.AssemblyTitle;

			// Настройка окна
			gr = Graphics.FromHwnd (this.Handle);

			// Настройка диалогов
			OFConfig.Title = "Select configuration for showing";

			string cfgExtension = ".csc" + ProgramDescription.AssemblyVersion.Replace (".", "").Substring (0, 2);
			OFConfig.Filter = "CodeShow configurations (*" + cfgExtension + ")|*" + cfgExtension;
			OFConfig.InitialDirectory = Application.StartupPath;
			OFConfig.FileName = commandLine;

			SFVideo.Title = "Select placement of new video";
			SFVideo.Filter = "Audio-Video Interchange video format (*.avi)|(*.avi)";

			// Формирование шрифтов и кистей
			brushes.Add (new List<SolidBrush> ());		// Общие кисти
			brushes[0].Add (new SolidBrush (Color.FromArgb (0, 0, 0)));
			brushes[0].Add (new SolidBrush (Color.FromArgb (10, brushes[0][0].Color)));

			brushes.Add (new List<SolidBrush> ());		// Кисти комментариев
			brushes[1].Add (new SolidBrush (Color.FromArgb (96, 96, 96)));		// Текст
			brushes[1].Add (new SolidBrush (Color.FromArgb (255, 255, 255)));	// Фон
			brushes[1].Add (new SolidBrush (Color.FromArgb (128, 128, 128)));	// Рамка
			brushes[1].Add (new SolidBrush (Color.FromArgb (255, 255, 255)));	// Текст заголовка

			brushes.Add (new List<SolidBrush> ());		// Кисти кода
			brushes[2].Add (new SolidBrush (Color.FromArgb (0, 128, 255)));
			brushes[2].Add (new SolidBrush (Color.FromArgb (255, 255, 255)));
			brushes[2].Add (new SolidBrush (Color.FromArgb (0, 128, 255)));
			brushes[2].Add (new SolidBrush (Color.FromArgb (255, 255, 255)));

			brushes.Add (new List<SolidBrush> ());		// Кисти консоли
			brushes[3].Add (new SolidBrush (Color.FromArgb (192, 192, 192)));
			brushes[3].Add (new SolidBrush (Color.FromArgb (0, 0, 0)));
			brushes[3].Add (new SolidBrush (Color.FromArgb (0, 128, 0)));
			brushes[3].Add (new SolidBrush (Color.FromArgb (255, 255, 255)));

			brushes.Add (new List<SolidBrush> ());		// Кисти предупреждений
			brushes[4].Add (new SolidBrush (Color.FromArgb (255, 255, 255)));
			brushes[4].Add (new SolidBrush (Color.FromArgb (255, 128, 0)));
			brushes[4].Add (new SolidBrush (Color.FromArgb (0, 0, 0)));
			brushes[4].Add (new SolidBrush (Color.FromArgb (0, 0, 0)));

			brushes.Add (new List<SolidBrush> ());		// Неиспользуемый сет; рассчитан на поле исходного кода
			brushes[5].Add (new SolidBrush (Color.FromArgb (0, 0, 0)));
			brushes[5].Add (new SolidBrush (Color.FromArgb (0, 0, 0)));
			brushes[5].Add (new SolidBrush (Color.FromArgb (0, 0, 0)));
			brushes[5].Add (new SolidBrush (Color.FromArgb (0, 0, 0)));

			brushes.Add (new List<SolidBrush> ());		// Кисти ответов (замещает поле вывода)
			brushes[6].Add (new SolidBrush (Color.FromArgb (96, 96, 96)));
			brushes[6].Add (new SolidBrush (Color.FromArgb (255, 255, 255)));
			brushes[6].Add (new SolidBrush (Color.FromArgb (255, 255, 0)));
			brushes[6].Add (new SolidBrush (Color.FromArgb (0, 128, 0)));

			// Шрифты (перенесено в LoadConfig)

			// Загрузка параметров
			int err;
			if ((OFConfig.FileName == "") && (OFConfig.ShowDialog () != DialogResult.OK) || ((err = LoadConfig ()) < 0))
				{
				MessageBox.Show ("Failed to load configuration", ProgramDescription.AssemblyTitle,
					 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				this.Close ();
				return;
				}
			debugMode = (err > 0);
			SFVideo.FileName = Path.GetFileNameWithoutExtension (OFConfig.FileName) + ".avi";

			// Подготовка к записи в видеопоток
			layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)this.Height));	// Главный слой

			// Инициализация видеопотока (запрещена в режиме отладки конфигурации)
			if (!debugMode && (MessageBox.Show ("Write frames to AVI?", ProgramDescription.AssemblyTitle, MessageBoxButtons.YesNo,
				MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes) &&
				(SFVideo.ShowDialog () == DialogResult.OK))
				{
				vm = new VideoManager (SFVideo.FileName, 100.0 / generalStep, layers[0].Layer, true);

				if (!vm.IsInited)
					{
					MessageBox.Show ("Failed to initialize AVI stream", ProgramDescription.AssemblyTitle,
						 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					this.Close ();
					return;
					}
				}

			// Запуск
			ExtendedTimer.Enabled = true;
			this.Activate ();
			}

		// Таймер расширенного режима отображения
		private void ExtendedTimer_Tick (object sender, EventArgs e)
			{
			switch (currentPhase)
				{
				// Создание фрагментов лого
				case Phases.LayersPrecache:
					PrepareLayers ();
					break;

				// Отрисовка фрагментов лого
				case Phases.LogoFragment1:
					if (debugMode)
						currentPhase++;
					else
						DrawingLogoFragments ();
					break;

				// Пауза
				case Phases.LogoIntermission:
					if (debugMode || (steps++ > 150))
						{
						steps = 0;
						currentPhase++;
						}
					break;

				// Затенение
				case Phases.LogoFading1:
					if (debugMode)
						currentPhase++;
					else
						FadeScreen ();
					break;

				case Phases.LogoFading2:
					PrepareMainScene ();
					break;

				// Отображение объектов и текста
				case Phases.MainTextDrawing:
				case Phases.MainTextFading:
					// Отрисовка текста
					if ((mainStringsSet.Count > 0) && (mainStringsSet[0].Count > 0))
						currentFrame = mainStringsSet[0][0].StringType;

					if (currentPhase == Phases.MainTextDrawing)
						{
						DrawText (layers[(int)(currentFrame - 1) % 3 + 1].Descriptor, mainStringsSet);
						}

					// Сброс текста
					else
						{
						steps = 0;
						FlushText (currentFrame);
						}
					break;

				// Конечное затенение
				case Phases.EndingFading1:
					// Удаление слоёв основной сцены
					layers.RemoveAt (1);
					layers.RemoveAt (1);
					layers.RemoveAt (1);
					layers.RemoveAt (1);

					currentPhase++;
					break;

				// Завершение
				case Phases.Finished:
					// Остановка
					ExtendedTimer.Enabled = false;
					currentPhase++;
					this.Close ();
					break;
				}

			// Отрисовка слоёв
			if (currentPhase < Phases.Finished)
				DrawLayers ();
			}

		// Затемнение лого
		private void FadeScreen ()
			{
			// Перекрытие фона
			layers[1].Descriptor.FillRectangle (brushes[0][1], 0, 0, this.Width, this.Height);

			// Триггер
			if (steps++ >= 50)
				{
				steps = 0;
				currentPhase++;
				}
			}

		// Сброс текста
		private void FlushText (uint LayerNumber)
			{
			if ((LayerNumber > 0) && (LayerNumber <= frameTypesCount))
				{
				layers[((int)LayerNumber - 1) % 3 + 1].Descriptor.FillRectangle (brushes[(int)LayerNumber][1], borderSize, borderSize,
					layers[((int)LayerNumber - 1) % 3 + 1].Layer.Width - borderSize * 2,
					layers[((int)LayerNumber - 1) % 3 + 1].Layer.Height - borderSize * 2);
				}
			else
				{
				layers[1].Descriptor.FillRectangle (brushes[1][1], borderSize, borderSize,
					layers[1].Layer.Width - borderSize * 2, layers[1].Layer.Height - borderSize * 2);
				}

			// Переход
			drawPoint.X = lineLeft;
			drawPoint.Y = lineTop;
			currentPhase--;
			}

		// Отрисовка фрагментов лого
		private void DrawingLogoFragments ()
			{
			Bitmap b;
			steps += 6;

			b = ESHQSetupStub.Properties.Resources.FUPL.Clone (new Rectangle (0, 0,
				(int)((double)ESHQSetupStub.Properties.Resources.FUPL.Width *
				(0.5 + LogoDrawerSupport.Cosinus (steps - 180.0) / 2.0)) + 1, ESHQSetupStub.Properties.Resources.FUPL.Height),
				ESHQSetupStub.Properties.Resources.FUPL.PixelFormat);
			layers[1].Descriptor.DrawImage (b, (this.Width - ESHQSetupStub.Properties.Resources.FUPL.Width) / 2,
				(this.Height - ESHQSetupStub.Properties.Resources.FUPL.Height) / 2);
			b.Dispose ();

			if (steps >= 173)
				{
				// Версия

				// Расчёт размера надписи
				string ver = ProgramDescription.AssemblyVersion.Replace ("0", "");
				while (ver.Substring (ver.Length - 1, 1) == ".")
					ver = ver.Substring (0, ver.Length - 1);
				SizeF sz = gr.MeasureString (ProgramDescription.AssemblyMainName + " v " + ver, fonts[0]);

				// Надпись
				layers[1].Descriptor.DrawString (ProgramDescription.AssemblyMainName + " v " + ver, fonts[0], brushes[1][0],
					layers[1].Layer.Width - sz.Width - sz.Height, layers[1].Layer.Height - 2 * sz.Height);

				// Переход далее
				steps = 0;
				currentPhase++;
				}
			}

		// Создание и подготовка слоёв и лого
		private void PrepareLayers ()
			{
			///////////////////
			// Подготовка слоёв
			layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)this.Height));	// Слой лого

			// Начало записи
			this.BackColor = brushes[0][0].Color;	// Заливка фона
			layers[0].Descriptor.FillRectangle (brushes[0][0], 0, 0, this.Width, this.Height);
			layers[1].Descriptor.FillRectangle (brushes[0][0], 0, 0, this.Width, this.Height);

			// Первичная отрисовка
			DrawLayers ();

			// Переход к следующему обработчику
			currentPhase++;
			}

		private void PrepareMainScene ()
			{
			steps++;

			// Окно комментариев
			if (steps == 30)
				{
				// Удаление слоя лого
				layers.RemoveAt (1);

				// Подготовка слоёв
				layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)(this.Height * commentFramePart) + 1));	// Слой комментария
				for (uint i = borderSize; i > 0; i--)
					{
					double coeff = 0.5 * (double)i / (double)borderSize + 0.5;
					SolidBrush br = new SolidBrush (Color.FromArgb ((int)((double)brushes[1][2].Color.R * coeff),
						(int)((double)brushes[1][2].Color.G * coeff), (int)((double)brushes[1][2].Color.B * coeff)));
					layers[1].Descriptor.FillRectangle (br, borderSize - i, borderSize - i,
						layers[1].Layer.Width - 2 * (borderSize - i), layers[1].Layer.Height - 2 * (borderSize - i));
					br.Dispose ();
					}

				FlushText (1);
				currentPhase++;
				}

			// Окно кода
			if (steps == 60)
				{
				layers.Add (new LogoDrawerLayer (0, (uint)(this.Height * (commentFramePart + titlesFramePart)),
					(uint)this.Width / (showOutput ? 2u : 1u),
					(uint)(this.Height * (1.0 - commentFramePart - titlesFramePart)) + 1));	// Слой кода
				for (uint i = borderSize; i > 0; i--)
					{
					double coeff = 0.5 * (double)i / (double)borderSize + 0.5;
					SolidBrush br = new SolidBrush (Color.FromArgb ((int)((double)brushes[2][2].Color.R * coeff),
						(int)((double)brushes[2][2].Color.G * coeff), (int)((double)brushes[2][2].Color.B * coeff)));
					layers[2].Descriptor.FillRectangle (br, borderSize - i, borderSize - i,
						layers[2].Layer.Width - 2 * (borderSize - i), layers[2].Layer.Height - 2 * (borderSize - i));
					br.Dispose ();
					}

				FlushText (2);
				currentPhase++;
				}

			// Окно вывода или ответов
			if (steps == 75)
				{
				layers.Add (new LogoDrawerLayer ((uint)this.Width / 2, (uint)(this.Height * (commentFramePart + titlesFramePart)),
					(uint)this.Width / 2, (uint)(this.Height * (1.0 - commentFramePart - titlesFramePart)) + 1));	// Слой вывода

				if (showOutput)
					{
					for (uint i = borderSize; i > 0; i--)
						{
						double coeff = 0.5 * (double)i / (double)borderSize + 0.5;
						SolidBrush br = new SolidBrush (Color.FromArgb ((int)((double)brushes[answersMode ? 6 : 3][2].Color.R * coeff),
							(int)((double)brushes[answersMode ? 6 : 3][2].Color.G * coeff),
							(int)((double)brushes[answersMode ? 6 : 3][2].Color.B * coeff)));
						layers[3].Descriptor.FillRectangle (br, borderSize - i, borderSize - i,
							layers[3].Layer.Width - 2 * (borderSize - i), layers[3].Layer.Height - 2 * (borderSize - i));
						br.Dispose ();
						}

					FlushText (answersMode ? 6u : 3u);
					currentPhase++;
					}
				}

			if (steps == 85)
				{
				layers.Add (new LogoDrawerLayer (0, (uint)(this.Height * commentFramePart),
					(uint)this.Width, (uint)(this.Height * titlesFramePart) + 1));	// Слой панелей
				layers[4].Descriptor.FillRectangle (brushes[2][2], 0, 0,
					layers[4].Layer.Width / (showOutput ? 2 : 1), layers[4].Layer.Height);
				layers[4].Descriptor.DrawString ("Source code", fonts[3], brushes[2][3], lineLeft, lineTop / 2);

				if (showOutput)
					{
					layers[4].Descriptor.FillRectangle (brushes[answersMode ? 6 : 3][2], layers[4].Layer.Width / 2, 0,
						layers[4].Layer.Width / 2, layers[4].Layer.Height);
					layers[4].Descriptor.DrawString (answersMode ? "Answer" : "Output", fonts[3], brushes[answersMode ? 6 : 3][3],
						layers[4].Layer.Width / 2 + lineLeft, lineTop / 2);
					}
				}

			// Смена фазы
			if (steps > 100)
				{
				steps = 0;
				currentPhase++;
				}
			}

		// Отрисовка слоёв
		private void DrawLayers ()
			{
			// Сведение слоёв
			for (int i = 1; i < layers.Count; i++)
				{
				layers[0].Descriptor.DrawImage (layers[i].Layer, layers[i].Left, layers[i].Top);
				}

			// Отрисовка
			if (vm.IsInited)
				{
				Bitmap b = (Bitmap)layers[0].Layer.Clone ();
				vm.AddFrame (b);
				b.Dispose ();
				savingLayersCounter++;

				gr.FillRectangle (brushes[0][0], 0, 0, this.Width, this.Height);
				string s = "- Rendering -\nPhase: " + currentPhase.ToString () + "\nFrames: " + savingLayersCounter.ToString () +
					"\nPackages left: " + mainStringsSet.Count.ToString ();
				if (mainStringsSet.Count > 0)
					s += ("\nLines in current package left: " + mainStringsSet[0].Count.ToString ());

				gr.DrawString (s, fonts[1], brushes[2][0], 0, 0);
				}
			else
				{
				gr.DrawImage (layers[0].Layer, layers[0].Left, layers[0].Top);
				}

			// Контроль завершения
			if (currentPhase > Phases.Finished)
				this.Close ();
			}

		// Закрытие окна
		private void LogoDrawer_FormClosing (object sender, FormClosingEventArgs e)
			{
			// Остановка всех отрисовок
			ExtendedTimer.Enabled = false;

			// Сброс ресурсов
			for (int i = 0; i < brushes.Count; i++)
				{
				for (int j = 0; j < brushes[i].Count; j++)
					{
					brushes[i][j].Dispose ();
					}
				brushes[i].Clear ();
				}
			brushes.Clear ();

			for (int i = 0; i < fonts.Count; i++)
				{
				fonts[i].Dispose ();
				}
			fonts.Clear ();

			if (gr != null)
				gr.Dispose ();

			for (int i = 0; i < layers.Count; i++)
				layers[i].Dispose ();
			layers.Clear ();

			vm.Dispose ();
			}

		// Принудительный выход (по любой клавише)
		private void LogoDrawer_KeyDown (object sender, KeyEventArgs e)
			{
			if (e.KeyCode == Keys.Escape)
				this.Close ();
			}

		// Метод отрисовывает текст
		private void DrawText (Graphics Field, List<List<LogoDrawerString>> StringsSet)
			{
			// Последняя строка кончилась
			if (StringsSet[0].Count == 0)
				{
				StringsSet.RemoveAt (0);
				if (StringsSet.Count > 0)
					currentPhase++;
				else
					currentPhase += 2;
				return;
				}

			// Движение по строке
			if (steps < StringsSet[0][0].StringLength)
				{
				// Одна буква
				string letter = StringsSet[0][0].StringText.Substring ((int)steps++, 1);
				if ((StringsSet[0][0].StringType > 0) && (StringsSet[0][0].StringType <= frameTypesCount))
					{
					Field.DrawString (letter, StringsSet[0][0].StringFont, brushes[(int)StringsSet[0][0].StringType][0], drawPoint);
					}
				else
					{
					Field.DrawString (letter, StringsSet[0][0].StringFont, brushes[1][0], drawPoint);
					}

				// Смещение "каретки"
				SizeF sz = gr.MeasureString (letter, StringsSet[0][0].StringFont);
				drawPoint.X += (int)(sz.Width * 0.65f) * ((letter == " ") ? 2 : 1);

				// Конец строки, перевод "каретки"
				if ((drawPoint.X > Field.VisibleClipBounds.Width - lineRight) || (letter == "\n"))
					{
					drawPoint.X = lineLeft;
					drawPoint.Y += (int)(StringsSet[0][0].StringFont.Size * 1.65f);
					}
				}

			// Кончился текст строки и задержка отображения
			else if (steps > StringsSet[0][0].StringLength + StringsSet[0][0].Pause)
				{
				// Переход к следующей текстовой строке
				StringsSet[0].RemoveAt (0);
				steps = 0;
				if (StringsSet[0].Count > 0)
					{
					drawPoint.X = lineLeft;
					drawPoint.Y += (int)(StringsSet[0][0].StringFont.Size * 1.65f);
					}
				}

			// Кончился только текст строки, пауза
			else
				{
				steps++;
				}

			// Обработка смены экрана
			if (drawPoint.Y > (int)(Field.VisibleClipBounds.Height - 2 * lineTop))
				{
				drawPoint.Y = lineTop;
				currentPhase++;
				}
			}

		// Метод загружает текст для отображения
		private int LoadConfig ()
			{
			char[] splitters = new char[] { ' ', '\t' };

			// Открытие файла
			FileStream FS = null;
			try
				{
				FS = new FileStream (OFConfig.FileName, FileMode.Open);
				}
			catch
				{
				return -1;
				}
			StreamReader SR = new StreamReader (FS, Encoding.GetEncoding (1251));

			// Получение параметров
			uint commentPartPercentage, outputFrame = 1;
			string s = SR.ReadLine ();
			string[] values = s.Split (splitters, StringSplitOptions.RemoveEmptyEntries);

			try
				{
				commentPartPercentage = uint.Parse (values[0]);
				if ((commentPartPercentage > 70) || (commentPartPercentage < 30))
					commentFramePart = 0.3;
				else
					commentFramePart = (double)commentPartPercentage / 100.0;

				outputFrame = uint.Parse (values[1]);
				showOutput = (outputFrame != 0);
				}
			catch
				{
				return -2;
				}

			uint commentFontSize, codeFontSize, consoleFontSize;
			s = SR.ReadLine ();
			values = s.Split (splitters, StringSplitOptions.RemoveEmptyEntries);

			try
				{
				commentFontSize = uint.Parse (values[0]);
				if ((commentFontSize > 24) || (commentFontSize < 10))
					commentFontSize = 24;

				codeFontSize = uint.Parse (values[1]);
				if ((codeFontSize > 24) || (codeFontSize < 10))
					codeFontSize = 24;

				consoleFontSize = uint.Parse (values[2]);
				if ((consoleFontSize > 24) || (consoleFontSize < 10))
					consoleFontSize = 24;
				}
			catch
				{
				return -2;
				}

			fonts.Add (new Font ("Calibri", commentFontSize, FontStyle.Regular));
			fonts.Add (new Font ("Consolas", codeFontSize, FontStyle.Regular));
			fonts.Add (new Font ("Consolas", consoleFontSize, FontStyle.Regular));
			fonts.Add (new Font ("Calibri", 22, FontStyle.Bold));

			// Загрузка текста
			int debug = 0;

			// Чтение текста
			uint type = 0, oldType = 0, pause = 0;

			while (!SR.EndOfStream)
				{
				s = SR.ReadLine ().Replace ('`', '\n');

				if (s.Length < 9)
					{
					continue;
					}
				else if (s[0] == '!')	// Отладочная функция для файлов конфигурации
					{
					do
						{
						s = SR.ReadLine () + " ";
						} while (!SR.EndOfStream && (s[0] != '!'));
					debug = 1;
					}
				else
					{
					if (!uint.TryParse (s.Substring (0, 1), out type) || (type > frameTypesCount))	// Тип 0 используется для сброса текущего типа
						type = 1;
					answersMode = ((type == 6) || answersMode);

					if (!uint.TryParse (s.Substring (2, 5), out pause) || (pause > 60000))	// Пауза в миллисекундах
						pause = 0;	// Без ограничений
					pause = (pause * 100) / (generalStep * 1000);	// Пауза во фреймах

					if (!showOutput && (type == 3))
						continue;

					if (type != oldType)
						{
						oldType = type;
						mainStringsSet.Add (new List<LogoDrawerString> ());
						}

					if ((type > 0) && (type <= frameTypesCount))
						{
						mainStringsSet[mainStringsSet.Count - 1].Add (new LogoDrawerString (s.Substring (8),
							fonts[((int)type - 1) % 3], pause, 6, type));
						}
					else
						{
						mainStringsSet[mainStringsSet.Count - 1].Add (new LogoDrawerString (s.Substring (8),
							fonts[0], pause, 6, 1));
						}
					}
				}

			// Завершение
			if ((mainStringsSet.Count > 1) && (mainStringsSet[mainStringsSet.Count - 1].Count == 0))
				mainStringsSet.RemoveAt (mainStringsSet.Count - 1);

			SR.Close ();
			FS.Close ();
			return debug;
			}
		}
	}
