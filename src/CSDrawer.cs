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

		// Текст
		private List<List<LogoDrawerString>> mainStringsSet = new List<List<LogoDrawerString>> ();	// Тексты для отображения
		private Point drawPoint;								// Текущая позиция отрисовки текста
		private int lineFeed,									// Высота строки текста расширенного режима
			lineLeft,											// Начало и конец строки текста расширенного режима
			lineRight,
			lineTop;											// Начало блока текста расширенного режима

		// Графика
		private List<LogoDrawerLayer> layers = new List<LogoDrawerLayer> ();		// Базовые слои изображения
		private uint savingLayersCounter = 0;					// Счётчик сохранений
		private uint borderSize = 5;							// Ширина границы субокна
		private uint currentFrame = 0;							// Текущее субокно

		private Graphics gr;									// Объекты-отрисовщики
		private SolidBrush backBrush, fadeBrush,
			commentTextBrush, codeTextBrush, consoleTextBrush,
			commentBackBrush, codeBackBrush, consoleBackBrush,
			commentBorderBrush, codeBorderBrush, consoleBorderBrush,
			warningTextBrush, warningBackBrush;
		private Font commentFont, codeFont, consoleFont;

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
			backBrush = new SolidBrush (Color.FromArgb (255, 255, 255));
			fadeBrush = new SolidBrush (Color.FromArgb (50, 255, 255, 255));

			commentTextBrush = new SolidBrush (Color.FromArgb (96, 96, 96));
			codeTextBrush = new SolidBrush (Color.FromArgb (0, 128, 255));
			consoleTextBrush = new SolidBrush (Color.FromArgb (192, 192, 192));

			commentBackBrush = new SolidBrush (Color.FromArgb (255, 255, 255));
			codeBackBrush = new SolidBrush (Color.FromArgb (255, 255, 255));
			consoleBackBrush = new SolidBrush (Color.FromArgb (0, 0, 0));

			commentBorderBrush = new SolidBrush (Color.FromArgb (128, 128, 128));
			codeBorderBrush = new SolidBrush (Color.FromArgb (0, 128, 255));
			consoleBorderBrush = new SolidBrush (Color.FromArgb (0, 128, 0));

			warningTextBrush = new SolidBrush (Color.FromArgb (255, 255, 255));
			warningBackBrush = new SolidBrush (Color.FromArgb (255, 128, 0));

			commentFont = new Font ("Calibri", 24, FontStyle.Regular);
			codeFont = new Font ("Consolas", 22, FontStyle.Regular);
			consoleFont = new Font ("Consolas", 22, FontStyle.Regular);

			// Загрузка параметров
			int err = -2;
			if ((OFConfig.ShowDialog () != DialogResult.OK) || !LoadText ())
				{
				MessageBox.Show ("Failed to load configuration: error " + err.ToString (), ProgramDescription.AssemblyTitle,
					 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				this.Close ();
				return;
				}
			SFVideo.FileName = Path.GetFileNameWithoutExtension (OFConfig.FileName) + ".avi";

			// Подготовка к записи в видеопоток
			layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)this.Height));	// Главный слой

			if ((MessageBox.Show ("Write frames to AVI?", ProgramDescription.AssemblyTitle, MessageBoxButtons.YesNo,
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

			// Настройка параметров
			lineLeft = lineRight = 20;
			lineFeed = 38;
			lineTop = 20;

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
					DrawingLogoFragments ();
					break;

				// Пауза
				case Phases.LogoIntermission:
					if (steps++ > 150)
						{
						steps = 0;
						currentPhase++;
						}
					break;

				// Затенение
				case Phases.LogoFading1:
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
						DrawText (layers[(currentFrame > 3) ? 1 : (int)currentFrame].Descriptor, mainStringsSet);
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
			layers[1].Descriptor.FillRectangle (fadeBrush, 0, 0, this.Width, this.Height);

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
			if (LayerNumber >= layers.Count)
				return;

			switch (LayerNumber)
				{
				// Сброс
				default:
				case 1:
					layers[1].Descriptor.FillRectangle (commentBackBrush, borderSize, borderSize,
						layers[1].Layer.Width - borderSize * 2, layers[1].Layer.Height - borderSize * 2);
					break;

				case 2:
					layers[2].Descriptor.FillRectangle (codeBackBrush, borderSize, borderSize,
						layers[2].Layer.Width - borderSize * 2, layers[2].Layer.Height - borderSize * 2);
					break;

				case 3:
					layers[3].Descriptor.FillRectangle (consoleBackBrush, borderSize, borderSize,
						layers[3].Layer.Width - borderSize * 2, layers[3].Layer.Height - borderSize * 2);
					break;

				case 4:
					layers[1].Descriptor.FillRectangle (warningBackBrush, borderSize, borderSize,
						layers[1].Layer.Width - borderSize * 2, layers[1].Layer.Height - borderSize * 2);
					break;
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
			this.BackColor = backBrush.Color;	// Заливка фона
			layers[0].Descriptor.FillRectangle (backBrush, 0, 0, this.Width, this.Height);
			layers[1].Descriptor.FillRectangle (backBrush, 0, 0, this.Width, this.Height);

			// Первичная отрисовка
			DrawLayers ();

			// Переход к следующему обработчику
			currentPhase++;
			}

		private void PrepareMainScene ()
			{
			steps++;

			// Удаление слоя лого
			if (steps == 30)
				{
				layers.RemoveAt (1);

				// Подготовка слоёв
				layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)(this.Height * 0.3) + 1));	// Слой комментария
				for (uint i = borderSize; i > 0; i--)
					{
					double coeff = 0.5 * (double)i / (double)borderSize + 0.5;
					SolidBrush br = new SolidBrush (Color.FromArgb ((int)((double)commentBorderBrush.Color.R * coeff),
						(int)((double)commentBorderBrush.Color.G * coeff), (int)((double)commentBorderBrush.Color.B * coeff)));
					layers[1].Descriptor.FillRectangle (br, borderSize - i, borderSize - i,
						layers[1].Layer.Width - 2 * (borderSize - i), layers[1].Layer.Height - 2 * (borderSize - i));
					br.Dispose ();
					}

				FlushText (1);
				currentPhase++;
				}

			if (steps == 60)
				{
				layers.Add (new LogoDrawerLayer (0, (uint)(this.Height * 0.4), (uint)this.Width / 2,
					(uint)(this.Height * 0.6) + 1));									// Слой кода
				for (uint i = borderSize; i > 0; i--)
					{
					double coeff = 0.5 * (double)i / (double)borderSize + 0.5;
					SolidBrush br = new SolidBrush (Color.FromArgb ((int)((double)codeBorderBrush.Color.R * coeff),
						(int)((double)codeBorderBrush.Color.G * coeff), (int)((double)codeBorderBrush.Color.B * coeff)));
					layers[2].Descriptor.FillRectangle (br, borderSize - i, borderSize - i,
						layers[2].Layer.Width - 2 * (borderSize - i), layers[2].Layer.Height - 2 * (borderSize - i));
					br.Dispose ();
					}

				FlushText (2);
				currentPhase++;
				}

			if (steps == 75)
				{
				layers.Add (new LogoDrawerLayer ((uint)this.Width / 2, (uint)(this.Height * 0.4),
					(uint)this.Width / 2, (uint)(this.Height * 0.6) + 1));				// Слой вывода
				for (uint i = borderSize; i > 0; i--)
					{
					double coeff = 0.5 * (double)i / (double)borderSize + 0.5;
					SolidBrush br = new SolidBrush (Color.FromArgb ((int)((double)consoleBorderBrush.Color.R * coeff),
						(int)((double)consoleBorderBrush.Color.G * coeff), (int)((double)consoleBorderBrush.Color.B * coeff)));
					layers[3].Descriptor.FillRectangle (br, borderSize - i, borderSize - i,
						layers[3].Layer.Width - 2 * (borderSize - i), layers[3].Layer.Height - 2 * (borderSize - i));
					br.Dispose ();
					}

				FlushText (3);
				currentPhase++;
				}

			if (steps == 85)
				{
				layers.Add (new LogoDrawerLayer (0, (uint)(this.Height * 0.3), (uint)this.Width, (uint)(this.Height * 0.1) + 1));	// Слой панелей
				layers[4].Descriptor.FillRectangle (codeBorderBrush, 0, 0, layers[4].Layer.Width / 2, layers[4].Layer.Height);
				layers[4].Descriptor.DrawString ("Source code", codeFont, codeBackBrush, lineLeft, lineTop);
				layers[4].Descriptor.FillRectangle (consoleBorderBrush, layers[4].Layer.Width / 2, 0,
					layers[4].Layer.Width / 2, layers[4].Layer.Height);
				layers[4].Descriptor.DrawString ("Output", consoleFont, consoleBackBrush, layers[4].Layer.Width / 2 + lineLeft, lineTop);
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

				gr.FillRectangle (backBrush, 0, 0, this.Width, this.Height);
				string s = "- Rendering -\nPhase: " + currentPhase.ToString () + "\nFrames: " + savingLayersCounter.ToString () +
					"\nPackages left: " + mainStringsSet.Count.ToString ();
				if (mainStringsSet.Count > 0)
					s += ("\nLines in current package left: " + mainStringsSet[0].Count.ToString ());

				gr.DrawString (s, codeFont, codeTextBrush, 0, 0);
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
			if (backBrush != null)
				backBrush.Dispose ();
			if (commentTextBrush != null)
				commentTextBrush.Dispose ();
			if (commentBackBrush != null)
				commentBackBrush.Dispose ();
			if (commentBorderBrush != null)
				commentBorderBrush.Dispose ();
			if (codeTextBrush != null)
				codeTextBrush.Dispose ();
			if (codeBackBrush != null)
				codeBackBrush.Dispose ();
			if (codeBorderBrush != null)
				codeBorderBrush.Dispose ();
			if (consoleTextBrush != null)
				consoleTextBrush.Dispose ();
			if (consoleBackBrush != null)
				consoleBackBrush.Dispose ();
			if (consoleBorderBrush != null)
				consoleBorderBrush.Dispose ();
			if (warningBackBrush != null)
				warningBackBrush.Dispose ();
			if (warningTextBrush != null)
				warningTextBrush.Dispose ();

			if (gr != null)
				gr.Dispose ();

			if (commentFont != null)
				commentFont.Dispose ();
			if (codeFont != null)
				codeFont.Dispose ();
			if (consoleFont != null)
				consoleFont.Dispose ();

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
				switch (StringsSet[0][0].StringType)
					{
					case 1:
						Field.DrawString (letter, StringsSet[0][0].StringFont, commentTextBrush, drawPoint);
						break;

					case 2:
						Field.DrawString (letter, StringsSet[0][0].StringFont, codeTextBrush, drawPoint);
						break;

					case 3:
						Field.DrawString (letter, StringsSet[0][0].StringFont, consoleTextBrush, drawPoint);
						break;

					case 4:
						Field.DrawString (letter, StringsSet[0][0].StringFont, warningTextBrush, drawPoint);
						break;
					}

				// Смещение "каретки"
				SizeF sz = gr.MeasureString (letter, StringsSet[0][0].StringFont);
				drawPoint.X += (int)(sz.Width * 0.65f) * ((letter == " ") ? 2 : 1);

				// Конец строки, перевод "каретки"
				if ((drawPoint.X > Field.VisibleClipBounds.Width - lineRight) || (letter == "\n"))
					{
					drawPoint.X = lineLeft;
					drawPoint.Y += lineFeed;
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
					drawPoint.Y += lineFeed;
					}
				}

			// Кончился только текст строки, пауза
			else
				{
				steps++;
				}

			// Обработка смены экрана
			if (drawPoint.Y > Field.VisibleClipBounds.Height - lineFeed)
				{
				drawPoint.Y = lineTop;
				currentPhase++;
				}
			}

		// Метод загружает текст для отображения
		private bool LoadText ()
			{
			// Открытие файла
			FileStream FS = null;
			try
				{
				FS = new FileStream (OFConfig.FileName, FileMode.Open);
				}
			catch
				{
				return false;
				}
			StreamReader SR = new StreamReader (FS, Encoding.GetEncoding (1251));

			// Загрузка текста
			string s;

			// Чтение текста
			uint type = 0, oldType = 0, pause = 0;

			while (!SR.EndOfStream)
				{
				s = SR.ReadLine ().Replace ('`', '\n');

				if (s.Length < 9)
					{
					continue;
					}
				else
					{
					if (!uint.TryParse (s.Substring (0, 1), out type) || (type > 4))	// Тип 0 используется для сброса текущего типа
						type = 1;
					if (!uint.TryParse (s.Substring (2, 5), out pause) || (pause > 60000))
						pause = 0;	// Без ограничений

					if (type != oldType)
						{
						oldType = type;
						mainStringsSet.Add (new List<LogoDrawerString> ());
						}

					switch (type)
						{
						default:
						case 1:
						case 4:
							mainStringsSet[mainStringsSet.Count - 1].Add (new LogoDrawerString (s.Substring (8), commentFont, pause, 5, type));
							break;

						case 2:
							mainStringsSet[mainStringsSet.Count - 1].Add (new LogoDrawerString (s.Substring (8), codeFont, pause, 6, type));
							break;

						case 3:
							mainStringsSet[mainStringsSet.Count - 1].Add (new LogoDrawerString (s.Substring (8), consoleFont, pause, 6, type));
							break;
						}
					}
				}

			// Завершение
			if ((mainStringsSet.Count > 1) && (mainStringsSet[mainStringsSet.Count - 1].Count == 0))
				mainStringsSet.RemoveAt (mainStringsSet.Count - 1);

			SR.Close ();
			FS.Close ();
			return true;
			}
		}
	}
