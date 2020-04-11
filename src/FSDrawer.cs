using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

// Классы
namespace RD_AAOW
	{
	/// <summary>
	/// Класс обеспечивает отображение визуализации текста
	/// </summary>
	public partial class FSDrawer:Form
		{
		// Переменные и константы
		// Главные
		private Phases currentPhase = Phases.LayersPrecache;	// Текущая фаза отрисовки
		private Random rnd = new Random ();						// ГПСЧ
		private uint steps = 0;									// Счётчик шагов отрисовки
		private float scale = 1.0f;								// Главный масштаб
		private const uint generalStep = 3;						// Длительность главного шага отображения
		private string commandLine;								// Параметры командной строки
		private ParametersPicker pp;							// Форма запроса параметров работы отрисовщика

		// Текст
		private List<List<LogoDrawerString>> mainStringsSet = new List<List<LogoDrawerString>> ();		// Основной текст
		private List<List<LogoDrawerString>> signatureStringsSet = new List<List<LogoDrawerString>> ();	// Подпись

		private char[] splitters = new char[] { ' ', '\t' };	// Спиттеры для текста
		private char signatureMarker = '^',						// Маркеры фрагментов текста
			textMarker = '#',
			colorMarker = '&';
		private Point drawPoint;								// Текущая позиция отрисовки текста
		private int lineFeed,									// Высота строки текста расширенного режима
			lineLeft,											// Начало и конец строки текста расширенного режима
			lineRight,
			lineTop;											// Начало блока текста расширенного режима

		// Графика
		private List<LogoDrawerLayer> layers = new List<LogoDrawerLayer> ();		// Базовые слои изображения
		private uint savingLayersCounter = 0;

		private Graphics gr;									// Объекты-отрисовщики
		private SolidBrush logoForeBrush, logoBackBrush,
			plotGradient1Brush, textBrush;
		private List<SolidBrush> plotBackBrushes = new List<SolidBrush> ();
		private Font logoFont, versionFont, textFont, signatureFont;
		private uint textFontSize, signatureFontSize;
		private bool centerizeText;
		private Pen logoBackPen;
		private Color currentColor;

		private ColorMatrix fadeMatrix;							// Матрица затенения текста
		private ImageAttributes fadeAttributes;					// Дескриптор параметров затенения

		private List<Bitmap> logo = new List<Bitmap> ();		// Фрагменты основного лого

		private List<ILogoDrawerObject> objects = new List<ILogoDrawerObject> ();	// Визуальные объекты
		private LogoDrawerObjectMetrics objectsMetrics;			// Метрики генерируемых объектов

		// Звук
		private AudioManager am = new AudioManager ();			// Эмбиент
		private uint soundStartFrame, soundEndFrame;			// Расчётные фреймы начала и остановки звука

		// Видео
		private VideoManager vm = new VideoManager ();			// Видеофайл (балластная инициализация)

		// Возможные фазы отрисовки
		private enum Phases
			{
			// Подготовка слоёв
			LayersPrecache = 1,

			// Первый фрагмент лого
			LogoFragment1 = 2,

			// Штриховка первого фрагмента
			LogoFragment1Overlay = 3,

			// Второй фрагмент
			LogoFragment2 = 4,

			// Пауза после лого
			LogoIntermission = 5,

			// Затенение лого
			LogoFading = 6,

			// Основной градиент для поля текста
			MainGradient = 7,

			// Отрисовка основного текста
			MainTextDrawing = 8,

			// Сброс текста
			MainTextFading = 9,

			// Подготовка подписи
			SignaturePrecache = 10,

			// Отрисовка подписи
			SignatureDrawing = 11,

			// Конечное затенение
			EndingFading1 = 13,

			EndingFading2 = 14,

			// Завершение и конечная остановка
			Finished = 15,

			End = 16
			}

		/// <summary>
		/// Конструктор. Инициализирует экземпляр отрисовщика
		/// </summary>
		/// <param name="CommandLine">Параметры командной строки</param>
		public FSDrawer (string CommandLine)
			{
			// Инициализация
			commandLine = CommandLine;
			InitializeComponent ();
			}

		private void FSDrawer_Shown (object sender, EventArgs e)
			{
			// Если запрос границ экрана завершается ошибкой, отменяем отображение
			this.Left = this.Top = 0;
			try
				{
				this.Width = (int)(Screen.PrimaryScreen.Bounds.Width * scale);
				this.Height = (int)(Screen.PrimaryScreen.Bounds.Height * scale);
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
			SFConfig.Title = "Specify configuration name";

			string cfgExtension = ".fss" + ProgramDescription.AssemblyVersion.Replace (".", "").Substring (0, 2);
			OFConfig.Filter = SFConfig.Filter = "FullSilence shows (*" + cfgExtension + ")|*" + cfgExtension;
			OFConfig.InitialDirectory = SFConfig.InitialDirectory = Application.StartupPath;
			OFConfig.FileName = SFConfig.FileName = commandLine;

			SFVideo.Title = "Select placement of new video";
			SFVideo.Filter = "Audio-Video Interchange video format (*.avi)|(*.avi)";

			// Загрузка параметров
			int err = -2;
			if ((OFConfig.FileName == "") && (OFConfig.ShowDialog () != DialogResult.OK) || ((err = LoadConfig ()) != 0))
				{
				MessageBox.Show ("Failed to load configuration: error " + err.ToString (), ProgramDescription.AssemblyTitle,
					 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				this.Close ();
				return;
				}
			SFVideo.FileName = Path.GetFileNameWithoutExtension (OFConfig.FileName) + ".avi";

			pp = new ParametersPicker (true);	// Запрос параметров отрисовки
			pp.ShowDialog ();

			// Подготовка к записи в видеопоток
			layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)this.Height));	// Главный слой

			if (pp.WriteFramesToAVI && (SFVideo.ShowDialog () == DialogResult.OK))
				{
				vm = new VideoManager (SFVideo.FileName, (int)(100 / generalStep), layers[0].Layer, true);

				if (!vm.IsCreated)
					{
					MessageBox.Show ("Failed to initialize AVI stream", ProgramDescription.AssemblyTitle,
						 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					this.Close ();
					return;
					}
				}

			// Донастройка окна
			if (!vm.IsCreated)
				{
				scale = 0.7f;
				this.Width = (int)(this.Width * scale);
				this.Height = (int)(this.Height * scale);
				}

			logoFont = new Font ("a_AvanteInt", 100 * scale, FontStyle.Bold);
			versionFont = new Font ("a_AvanteInt", 24 * scale);
			textFont = new Font ("Monotype Corsiva", textFontSize * scale, FontStyle.Italic);
			signatureFont = new Font ("Annabelle", signatureFontSize * scale, FontStyle.Italic);

			// Подготовка к воспроизведению эмбиента
			if (pp.PlayAttachedAmbience)
				{
				am = new AudioManager (Path.GetDirectoryName (OFConfig.FileName) + "\\" +
					Path.GetFileNameWithoutExtension (OFConfig.FileName) + ".wav", false);

				if (!am.IsInited)
					{
					MessageBox.Show ("Failed to load ambience", ProgramDescription.AssemblyTitle,
						 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					this.Close ();
					return;
					}
				}

			// Загрузка текста
			if (!LoadText ())
				{
				MessageBox.Show ("Failed to load text", ProgramDescription.AssemblyTitle,
					 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				this.Close ();
				return;
				}

			// Настройка параметров
			logoBackPen = new Pen (logoBackBrush.Color);
			lineLeft = lineRight = (int)(50 * scale);
			lineFeed = (int)(50 * scale);
			lineTop = this.Height / 24;

			float[][] fadeMatrixItems = { 
				new float[] {1, 0, 0, 0, 0},
				new float[] {0, 1, 0, 0, 0},
				new float[] {0, 0, 1, 0, 0},
				new float[] {0, 0, 0, 0.8f, 0}, 
				new float[] {0, 0, 0, 0, 1}};
			fadeMatrix = new ColorMatrix (fadeMatrixItems);
			fadeAttributes = new ImageAttributes ();
			fadeAttributes.SetColorMatrix (fadeMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

			// Настройка параметров демонстрации
			if (pp.AdjustShowParameters)
				{
				InitializeSettingsFrame ();
				SettingsGroup.Enabled = SettingsGroup.Visible = true;
				}

			// Запуск
			else
				{
				ExtendedTimer.Enabled = true;
				}
			this.Activate ();
			}

		// Инициализация поля настроек демонстрации
		private void InitializeSettingsFrame ()
			{
			// Цвета
			LogoForeground.BackColor = logoForeBrush.Color;
			LogoForeground.ForeColor = GetNearbyColor (LogoForeground.ForeColor);
			LogoBackground.BackColor = logoBackBrush.Color;
			LogoBackground.ForeColor = GetNearbyColor (LogoBackground.BackColor);
			MainGradient.BackColor = Color.FromArgb (255, plotGradient1Brush.Color);
			MainGradient.ForeColor = GetNearbyColor (MainGradient.BackColor);
			MainText.BackColor = Color.FromArgb (255, textBrush.Color);
			MainText.ForeColor = GetNearbyColor (MainText.BackColor);
			FirstMainBack.BackColor = Color.FromArgb (255, plotBackBrushes[0].Color);
			FirstMainBack.ForeColor = GetNearbyColor (FirstMainBack.BackColor);

			// Параметры
			TextFontSizeField.Minimum = SignatureFontSizeField.Minimum = LogoDrawerSupport.MinFontSize;
			TextFontSizeField.Maximum = SignatureFontSizeField.Maximum = LogoDrawerSupport.MaxFontSize;
			TextFontSizeField.Value = textFontSize;
			SignatureFontSizeField.Value = signatureFontSize;

			CenterTextAlign.Checked = centerizeText;

			// Метрики
			for (int i = 0; i < LogoDrawerSupport.ObjectTypesCount; i++)
				ObjectsTypeCombo.Items.Add (((LogoDrawerObjectTypes)i).ToString ());
			if ((uint)objectsMetrics.ObjectsType >= ObjectsTypeCombo.Items.Count)
				ObjectsTypeCombo.SelectedIndex = 0;
			else
				ObjectsTypeCombo.SelectedIndex = (int)objectsMetrics.ObjectsType;

			ObjectsCountField.Maximum = LogoDrawerSupport.MaxObjectsCount;
			ObjectsCountField.Value = objectsMetrics.ObjectsCount;

			SidesCountField.Minimum = LogoDrawerSupport.MinPolygonsSidesCount;
			SidesCountField.Maximum = LogoDrawerSupport.MaxPolygonsSidesCount;
			SidesCountField.Value = objectsMetrics.PolygonsSidesCount;

			for (int i = 0; i < LogoDrawerSupport.ObjectStartupPositionsCount; i++)
				StartupSideCombo.Items.Add (((LogoDrawerObjectStartupPositions)i).ToString ());
			if ((uint)objectsMetrics.StartupPosition >= StartupSideCombo.Items.Count)
				StartupSideCombo.SelectedIndex = 0;
			else
				StartupSideCombo.SelectedIndex = (int)objectsMetrics.StartupPosition;

			KeepTracks.Checked = objectsMetrics.KeepTracks;
			Acceleration.Checked = objectsMetrics.Acceleration;

			EnlargingCoeff.Minimum = -LogoDrawerSupport.MaxEnlarge;
			EnlargingCoeff.Maximum = LogoDrawerSupport.MaxEnlarge;
			EnlargingCoeff.Value = objectsMetrics.Enlarging;

			MinSpeedField.Minimum = MaxSpeedField.Minimum = LogoDrawerSupport.MinObjectSpeed;
			MinSpeedField.Maximum = MaxSpeedField.Maximum = SpeedFluctuationField.Maximum = LogoDrawerSupport.MaxObjectSpeed;
			MinSpeedField.Value = objectsMetrics.MinSpeed;
			MaxSpeedField.Value = objectsMetrics.MaxSpeed;
			SpeedFluctuationField.Value = objectsMetrics.MaxSpeedFluctuation;

			MinSizeField.Minimum = MaxSizeField.Minimum = LogoDrawerSupport.MinObjectSize;
			MinSizeField.Maximum = MaxSizeField.Maximum = LogoDrawerSupport.MaxObjectSize;
			MinSizeField.Value = objectsMetrics.MinSize;
			MaxSizeField.Value = objectsMetrics.MaxSize;

			MinColor.BackColor = Color.FromArgb (objectsMetrics.MinRed, objectsMetrics.MinGreen, objectsMetrics.MinBlue);
			MinColor.ForeColor = GetNearbyColor (MinColor.BackColor);
			MaxColor.BackColor = Color.FromArgb (objectsMetrics.MaxRed, objectsMetrics.MaxGreen, objectsMetrics.MaxBlue);
			MaxColor.ForeColor = GetNearbyColor (MaxColor.BackColor);

			// Текст
			SignatureText.Text = signatureStringsSet[0][0].StringText.Replace ('\n', '|');

			for (int i = 0; i < mainStringsSet.Count; i++)
				{
				// Сменяемый цвет
				if (plotBackBrushes[i + 1].Color.ToArgb () != plotBackBrushes[i].Color.ToArgb ())
					{
					MainTextField.Text += (colorMarker.ToString () + plotBackBrushes[i + 1].Color.R.ToString () +
						" " + plotBackBrushes[i + 1].Color.G.ToString () + " " + plotBackBrushes[i + 1].Color.B.ToString () + "\r\n");
					}

				// Строки текста
				for (int j = 0; j < mainStringsSet[i].Count; j++)
					{
					if (j == mainStringsSet[i].Count - 1)
						MainTextField.Text += textMarker.ToString ();
					MainTextField.Text += (mainStringsSet[i][j].StringText + "\r\n");
					}

				MainTextField.Text += "\r\n";
				}

			// Завершено
			}

		// Метод формирует цвет, достоверно отличающийся от указанного
		private Color GetNearbyColor (Color OldColor)
			{
			/*int r = (OldColor.R > 127) ? (OldColor.R - 128) : (OldColor.R + 128),
				g = (OldColor.G > 127) ? (OldColor.G - 128) : (OldColor.G + 128),
				b = (OldColor.B > 127) ? (OldColor.B - 128) : (OldColor.B + 128);*/
			return (OldColor.R + OldColor.G + OldColor.B > 128 * 3) ? Color.FromArgb (0, 0, 0) : Color.FromArgb (255, 255, 255);
			}

		// Таймер расширенного режима отображения
		private void ExtendedTimer_Tick (object sender, EventArgs e)
			{
			switch (currentPhase)
				{
				// Создание фрагментов лого
				case Phases.LayersPrecache:
					// Запуск звуковой дорожки (используется для запуска звука)
					if (!vm.IsCreated)
						PlayAmbience ();

					PrepareLayers ();
					break;

				// Отрисовка фрагментов лого
				case Phases.LogoFragment1:
				case Phases.LogoFragment2:
					DrawingLogoFragments ();
					break;

				// Штриховка
				case Phases.LogoFragment1Overlay:
					MakeStripes ();
					break;

				// Пауза
				case Phases.LogoIntermission:
					if (steps++ > 150)
						{
						// Запуск звуковой дорожки (используется для фиксации момента запуска)
						if (vm.IsCreated)
							PlayAmbience ();

						PrepareMainScene ();
						}
					break;

				// Затенение
				case Phases.LogoFading:
					FadeScreen ();
					DrawObjects ();
					break;

				// Градиент
				case Phases.MainGradient:
					MakeGradient ();
					DrawObjects ();
					break;

				// Отображение объектов и текста
				case Phases.MainTextDrawing:
				case Phases.MainTextFading:
				case Phases.SignaturePrecache:
				case Phases.SignatureDrawing:
					// Подготовка подписи
					if (currentPhase == Phases.SignaturePrecache)
						{
						SizeF sz = gr.MeasureString (signatureStringsSet[0][0].StringText, signatureStringsSet[0][0].StringFont);

						lineLeft = drawPoint.X = this.Width - lineLeft - (int)sz.Width;
						drawPoint.Y = (int)(LogoDrawerSupport.TextFieldPart * this.Height) -
							(signatureStringsSet[0][0].StringText.Contains ("\n") ? 3 : 2) * lineFeed;

						currentPhase++;
						}

					// Отрисовка подписи
					else if (currentPhase == Phases.SignatureDrawing)
						{
						DrawText (layers[4].Descriptor, signatureStringsSet);
						}

					// Отрисовка текста
					else if (currentPhase == Phases.MainTextDrawing)
						{
						DrawText (layers[4].Descriptor, mainStringsSet);
						}

					// Сброс текста
					else
						{
						FlushText ();
						}

					DrawObjects ();
					break;

				// Конечное затенение
				case Phases.EndingFading1:
					// Удаление слоёв основной сцены
					layers.RemoveAt (1);
					layers.RemoveAt (1);
					layers.RemoveAt (1);
					layers.RemoveAt (1);

					// Добавление слоя затенения
					layers.Add (new LogoDrawerLayer (0, 0, (uint)(this.Width), (uint)(this.Height)));
					currentPhase++;
					break;

				case Phases.EndingFading2:
					FadeScreen ();
					break;

				// Завершение
				case Phases.Finished:
					// Остановка
					ExtendedTimer.Enabled = false;

					currentPhase++;
					break;
				}

			// Отрисовка слоёв
			if (currentPhase != Phases.End)
				DrawLayers ();
			}

		// Сброс текста
		private void FlushText ()
			{
			// Сброс текста
			/*Bitmap b = layers[4].Layer.Clone (new Rectangle (0, (int)steps, layers[4].Layer.Width, layers[4].Layer.Height - (int)steps),
				layers[4].Layer.PixelFormat);/**/
			/*b = layers[4].Layer.Clone (new Rectangle ((int)steps, 0, layers[4].Layer.Width - (int)steps, layers[4].Layer.Height),
				layers[4].Layer.PixelFormat);/**/

			Bitmap b = (Bitmap)layers[4].Layer.Clone ();
			layers[4].Dispose ();
			layers[4] = new LogoDrawerLayer (0, (uint)((1.0 - LogoDrawerSupport.TextFieldPart) * this.Height *
				(centerizeText ? 0.5 : 1.0)),
				(uint)this.Width, (uint)(LogoDrawerSupport.TextFieldPart * this.Height));

			layers[4].Descriptor.DrawImage (b, new Rectangle (0, 0, this.Width, (int)(LogoDrawerSupport.TextFieldPart * this.Height)),
				0, 0, b.Width, b.Height, GraphicsUnit.Pixel, fadeAttributes);
			b.Dispose ();

			// Обновление фона
			if (plotBackBrushes[1].Color != currentColor)
				layers[1].Descriptor.FillRectangle (plotBackBrushes[1], 0, 0, this.Width, this.Height);

			if (steps++ >= 50)
				{
				drawPoint.X = lineLeft;
				drawPoint.Y = lineTop;

				currentColor = plotBackBrushes[1].Color;
				plotBackBrushes.RemoveAt (1);
				steps = 0;
				currentPhase--;
				}
			}

		// Отрисовка основного градиента
		private void MakeGradient ()
			{
			layers[3].Descriptor.FillRectangle (plotGradient1Brush, 0, steps,
				this.Width, (uint)(LogoDrawerSupport.TextFieldPart * this.Height) - steps * (centerizeText ? 2 : 1));

			if (steps++ >= 80)
				{
				drawPoint.X = lineLeft;		// Установка начальной позиции текста
				drawPoint.Y = lineTop;

				layers.Add (new LogoDrawerLayer (0, (uint)((1 - LogoDrawerSupport.TextFieldPart) * this.Height *
					(centerizeText ? 0.5 : 1.0)),
					(uint)this.Width, (uint)(LogoDrawerSupport.TextFieldPart * this.Height)));		// Слой текста

				steps = 0;
				currentPhase++;
				}
			}

		// Затемнение лого
		private void FadeScreen ()
			{
			// Перекрытие фона
			if (currentPhase == Phases.LogoFading)
				layers[1].Descriptor.FillRectangle (plotBackBrushes[0], 0, 0, this.Width, this.Height);
			else
				layers[1].Descriptor.FillRectangle (plotBackBrushes[plotBackBrushes.Count - 1], 0, 0, this.Width, this.Height);

			// Триггер
			if (steps++ >= 50)
				{
				currentColor = plotBackBrushes[0].Color;

				if (currentPhase == Phases.LogoFading)
					{
					layers.Add (new LogoDrawerLayer (0, (uint)((1 - LogoDrawerSupport.TextFieldPart) * this.Height *
						(centerizeText ? 0.5 : 1.0)),
						(uint)this.Width, (uint)(LogoDrawerSupport.TextFieldPart * this.Height)));			// Слой градиента
					}
				if (currentPhase == Phases.EndingFading2)
					{
					soundEndFrame = savingLayersCounter;
					}

				steps = 0;
				currentPhase++;
				}
			}

		// Подготовка главной сцены
		private void PrepareMainScene ()
			{
			// Установка нового мастер-интервала
			ExtendedTimer.Interval = (int)generalStep;

			// Настройка слоёв
			layers.RemoveAt (1);	// Слой лого больше не нужен
			layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)this.Height));		// Слой фона
			layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)this.Height));		// Слой сфер

			// Генерация визуальных объектов
			for (int i = 0; i < objectsMetrics.ObjectsCount; i++)
				{
				switch (objectsMetrics.ObjectsType)
					{
					default:
					case LogoDrawerObjectTypes.Spheres:
						objects.Add (new LogoDrawerSphere ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics));
						break;

					case LogoDrawerObjectTypes.RotatingPolygons:
						objectsMetrics.AsStars = false;
						objectsMetrics.Rotation = true;
						objects.Add (new LogoDrawerSquare ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics));
						break;

					case LogoDrawerObjectTypes.RotatingStars:
						objectsMetrics.AsStars = true;
						objectsMetrics.Rotation = true;
						objects.Add (new LogoDrawerSquare ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics));
						break;

					case LogoDrawerObjectTypes.RotatingLetters:
						objectsMetrics.Rotation = true;
						objects.Add (new LogoDrawerLetter ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics));
						break;

					case LogoDrawerObjectTypes.Letters:
						objectsMetrics.Rotation = false;
						objects.Add (new LogoDrawerLetter ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics));
						break;

					case LogoDrawerObjectTypes.Polygons:
						objectsMetrics.AsStars = false;
						objectsMetrics.Rotation = false;
						objects.Add (new LogoDrawerSquare ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics));
						break;

					case LogoDrawerObjectTypes.Stars:
						objectsMetrics.AsStars = true;
						objectsMetrics.Rotation = false;
						objects.Add (new LogoDrawerSquare ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics));
						break;

					case LogoDrawerObjectTypes.RotatingPictures:
						objectsMetrics.Rotation = true;
						objects.Add (new LogoDrawerPicture ((uint)this.Width, (uint)this.Height,
							rnd, objectsMetrics, Path.GetDirectoryName (OFConfig.FileName) + "\\" +
							Path.GetFileNameWithoutExtension (OFConfig.FileName)));
						break;

					case LogoDrawerObjectTypes.Pictures:
						objectsMetrics.Rotation = false;
						objects.Add (new LogoDrawerPicture ((uint)this.Width, (uint)this.Height,
							rnd, objectsMetrics, Path.GetDirectoryName (OFConfig.FileName) + "\\" +
							Path.GetFileNameWithoutExtension (OFConfig.FileName)));
						break;
					}
				}

			// Переход к следующему шагу
			steps = 0;
			currentPhase++;
			}

		// Штриховка на лого
		private void MakeStripes ()
			{
			for (int i = 0; i < logo[0].Height / 5; i++)
				{
				layers[1].Descriptor.DrawLine (logoBackPen, (this.Width - logo[0].Width - logo[1].Width - 30) / 2,
					(this.Height - logo[0].Height) / 2 + 10 * i + steps,
					(this.Width - logo[0].Width - logo[1].Width - 30) / 2 + 10 * i + steps,
					(this.Height - logo[0].Height) / 2);
				}

			if (steps++ >= 4)
				{
				steps = 0;
				currentPhase++;
				}
			}

		// Отрисовка фрагментов лого
		private void DrawingLogoFragments ()
			{
			Bitmap b;

			if (currentPhase == Phases.LogoFragment1)
				{
				steps += 5;

				b = logo[0].Clone (new Rectangle (0, 0, logo[0].Width,
					(int)((double)logo[0].Height * (0.5 + LogoDrawerSupport.Cosinus (steps - 180.0) / 2.0)) + 1), logo[0].PixelFormat);
				layers[1].Descriptor.DrawImage (b, (this.Width - logo[0].Width - logo[1].Width - 30) / 2,
					(this.Height - logo[0].Height) / 2);
				b.Dispose ();
				}
			else
				{
				steps += 7;

				b = logo[1].Clone (new Rectangle (0, 0,
					(int)((double)logo[1].Width * (0.5 + LogoDrawerSupport.Cosinus (steps - 180.0) / 2.0)) + 1, logo[1].Height),
					logo[1].PixelFormat);
				layers[1].Descriptor.DrawImage (b, (this.Width + logo[0].Width - logo[1].Width + 30) / 2,
					(this.Height - logo[1].Height) / 2 + (int)(15 * scale));
				b.Dispose ();

				b = logo[2].Clone (new Rectangle (0, 0,
					(int)((double)logo[2].Width * (0.5 + LogoDrawerSupport.Cosinus (steps - 180.0) / 2.0)) + 1, logo[2].Height),
					logo[2].PixelFormat);
				layers[1].Descriptor.DrawImage (b, (this.Width - logo[0].Width - logo[1].Width - 30) / 2 - 15,
					(this.Height - logo[2].Height) / 2 + (int)(15 * scale));
				b.Dispose ();

				b = logo[3].Clone (new Rectangle (0, 0,
				(int)((double)logo[3].Width * (0.5 + LogoDrawerSupport.Cosinus (steps - 180.0) / 2.0)) + 1, logo[3].Height),
				logo[3].PixelFormat);
				layers[1].Descriptor.DrawImage (b, this.Width - logo[3].Width - 15, this.Height - logo[3].Height - 15);
				b.Dispose ();
				}

			if (steps >= 174)
				{
				steps = 0;
				currentPhase++;
				}
			}

		// Создание и подготовка слоёв и лого
		private void PrepareLayers ()
			{
			// Формирование лого
			// Часть 1

			// Создание полотна
			Bitmap b = new Bitmap ((int)(230 * scale), (int)(400 * scale));
			Graphics g = Graphics.FromImage (b);
			g.FillRectangle (logoBackBrush, 0, 0, b.Width, b.Height);

			// Создание фреймов
			Point[] frame1 = new Point[] {
				new Point (0, 0),
				new Point ((int)(150 * scale), (int)(130 * scale)),
				new Point ((int)(150 * scale), (int)(270 * scale)),
				new Point (0, (int)(400 * scale))
				};
			g.FillPolygon (logoForeBrush, frame1);

			Point[] frame2 = new Point[] {
				new Point ((int)(180 * scale), (int)(130 * scale)),
				new Point ((int)(230 * scale), (int)(130 * scale)),
				new Point ((int)(230 * scale), (int)(270 * scale)),
				new Point ((int)(180 * scale), (int)(270 * scale))
				};
			g.FillPolygon (logoForeBrush, frame2);

			// Объём
			for (int i = 0; i < b.Height; i++)
				{
				Pen p = new Pen (Color.FromArgb (200 - (int)(200.0 * LogoDrawerSupport.Sinus (180.0 * (double)i / (double)b.Height)),
					logoBackBrush.Color));
				g.DrawLine (p, 0, i, b.Width, i);
				p.Dispose ();
				}

			// Добавление
			logo.Add ((Bitmap)b.Clone ());
			g.Dispose ();
			b.Dispose ();

			//////////
			// Часть 2

			// Расчёт размера надписи
			SizeF sz = gr.MeasureString (ProgramDescription.AssemblyMainName, logoFont);

			// Создание полотна
			b = new Bitmap ((int)sz.Width + 1, (int)sz.Height + 1);
			g = Graphics.FromImage (b);
			g.FillRectangle (logoBackBrush, 0, 0, b.Width, b.Height);

			// Надпись
			g.DrawString (ProgramDescription.AssemblyMainName, logoFont, logoForeBrush, 0, 0);

			// Объём
			for (int i = 0; i < b.Height; i++)
				{
				Pen p = new Pen (Color.FromArgb (200 - (int)(200.0 * LogoDrawerSupport.Sinus (180.0 * (double)i / (double)b.Height)),
					logoBackBrush.Color));
				g.DrawLine (p, 0, i, b.Width, i);
				p.Dispose ();
				}

			// Добавление
			logo.Add ((Bitmap)b.Clone ());
			g.Dispose ();
			b.Dispose ();

			//////////
			// Часть 3

			// Расчёт размера надписи
			sz = gr.MeasureString ("FS", logoFont);

			// Создание полотна
			b = new Bitmap ((int)sz.Width + 1, (int)sz.Height + 1);
			g = Graphics.FromImage (b);

			// Надпись
			g.DrawString ("FS", logoFont, logoBackBrush, 0, 0);

			// Добавление
			logo.Add ((Bitmap)b.Clone ());
			g.Dispose ();
			b.Dispose ();

			//////////
			// Версия

			// Расчёт размера надписи
			string ver = ProgramDescription.AssemblyVersion.Replace ("0", "");
			while (ver.Substring (ver.Length - 1, 1) == ".")
				ver = ver.Substring (0, ver.Length - 1);
			sz = gr.MeasureString ("v " + ver, versionFont);

			// Создание полотна
			b = new Bitmap ((int)sz.Width + 1, (int)sz.Height + 1);
			g = Graphics.FromImage (b);

			// Надпись
			g.DrawString ("v " + ver, versionFont, logoForeBrush, 0, 0);

			// Добавление
			logo.Add ((Bitmap)b.Clone ());
			g.Dispose ();
			b.Dispose ();

			///////////////////
			// Подготовка слоёв
			layers.Add (new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)this.Height));	// Слой лого

			// Начало записи
			this.BackColor = logoBackBrush.Color;	// Заливка фона
			layers[0].Descriptor.FillRectangle (logoBackBrush, 0, 0, this.Width, this.Height);

			// Первичная отрисовка
			DrawLayers ();

			// Переход к следующему обработчику
			currentPhase++;
			}

		// Отрисовка визуальных объектов
		private void DrawObjects ()
			{
			// Затенение предыдущих элементов
			if (!objectsMetrics.KeepTracks)
				{
				layers[2].Dispose ();
				layers[2] = new LogoDrawerLayer (0, 0, (uint)this.Width, (uint)this.Height);
				}

			// Отрисовка объектов со смещением
			for (int i = 0; i < objects.Count; i++)
				{
				objects[i].Move (objectsMetrics.Acceleration && (rnd.Next (3) == 0), objectsMetrics.Enlarging);
				if (!objects[i].IsInited)
					{
					objects[i].Dispose ();
					switch (objectsMetrics.ObjectsType)
						{
						default:
						case LogoDrawerObjectTypes.Spheres:
							objects[i] = new LogoDrawerSphere ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics);
							break;

						case LogoDrawerObjectTypes.Polygons:
						case LogoDrawerObjectTypes.Stars:
						case LogoDrawerObjectTypes.RotatingPolygons:
						case LogoDrawerObjectTypes.RotatingStars:
							objects[i] = new LogoDrawerSquare ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics);
							break;

						case LogoDrawerObjectTypes.Letters:
						case LogoDrawerObjectTypes.RotatingLetters:
							objects[i] = new LogoDrawerLetter ((uint)this.Width, (uint)this.Height, rnd, objectsMetrics);
							break;

						case LogoDrawerObjectTypes.Pictures:
						case LogoDrawerObjectTypes.RotatingPictures:
							objects[i] = new LogoDrawerPicture ((uint)this.Width, (uint)this.Height,
								rnd, objectsMetrics, Path.GetDirectoryName (OFConfig.FileName) + "\\" +
								Path.GetFileNameWithoutExtension (OFConfig.FileName));
							break;
						}
					}

				layers[2].Descriptor.DrawImage (objects[i].Image, objects[i].X - objects[i].Image.Width / 2,
					objects[i].Y - objects[i].Image.Height / 2);
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
			if (vm.IsCreated)
				{
				Bitmap b = (Bitmap)layers[0].Layer.Clone ();
				vm.AddFrame (b);
				b.Dispose ();
				savingLayersCounter++;

				double soundAt = soundStartFrame / vm.FPS;
				double soundLength = ((double)soundEndFrame - (double)soundStartFrame) / vm.FPS;

				gr.FillRectangle (logoBackBrush, 0, 0, this.Width, this.Height);
				string s = "- Rendering -\nPhase: " + currentPhase.ToString () + "\nFrames: " + savingLayersCounter.ToString () +
					"\nVerses left: " + mainStringsSet.Count.ToString () +
					"\nSound at: " + ((soundAt > 0.0) ? (soundAt.ToString ("F3") + " s") : "<testing...>") +
					"\nSound length: " + ((soundLength > 0.0) ? (soundLength.ToString ("F3") + " s") : "<testing...>");

				if (!am.IsInited && (currentPhase == Phases.Finished))
					MessageBox.Show (s, ProgramDescription.AssemblyTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

				gr.DrawString (s, textFont, logoForeBrush, 0, 0);
				}
			else
				{
				gr.DrawImage (layers[0].Layer, layers[0].Left, layers[0].Top);
				}

			// Контроль завершения
			if (currentPhase >= Phases.Finished)
				this.Close ();
			}

		// Закрытие окна
		private void LogoDrawer_FormClosing (object sender, FormClosingEventArgs e)
			{
			// Остановка всех отрисовок
			ExtendedTimer.Enabled = false;

			// Сброс ресурсов
			if (logoBackBrush != null)
				logoBackBrush.Dispose ();
			if (logoForeBrush != null)
				logoForeBrush.Dispose ();

			for (int i = 0; i < plotBackBrushes.Count; i++)
				plotBackBrushes[i].Dispose ();
			plotBackBrushes.Clear ();

			if (plotGradient1Brush != null)
				plotGradient1Brush.Dispose ();
			if (textBrush != null)
				textBrush.Dispose ();
			if (logoBackPen != null)
				logoBackPen.Dispose ();
			if (gr != null)
				gr.Dispose ();

			if (logoFont != null)
				logoFont.Dispose ();
			if (versionFont != null)
				versionFont.Dispose ();
			if (signatureFont != null)
				signatureFont.Dispose ();
			if (textFont != null)
				textFont.Dispose ();

			for (int i = 0; i < logo.Count; i++)
				logo[i].Dispose ();
			logo.Clear ();

			for (int i = 0; i < objects.Count; i++)
				objects[i].Dispose ();
			objects.Clear ();

			for (int i = 0; i < layers.Count; i++)
				layers[i].Dispose ();
			layers.Clear ();

			mainStringsSet.Clear ();
			signatureStringsSet.Clear ();

			vm.Dispose ();
			am.Dispose ();
			//mm.Dispose ();
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
				if (letter == " ")
					{
					steps++;
					}
				else
					{
					Field.DrawString (letter, StringsSet[0][0].StringFont, textBrush, drawPoint);
					}

				// Смещение "каретки"
				SizeF sz = gr.MeasureString (letter, StringsSet[0][0].StringFont);
				drawPoint.X += (int)(sz.Width * 0.65f) * ((letter == " ") ? 2 : 1);

				// Конец строки, перевод "каретки"
				if ((drawPoint.X > this.Width - lineRight) || (letter == "\n"))
					{
					drawPoint.X = lineLeft;
					drawPoint.Y += lineFeed;

					// Обработка смены экрана
					if (drawPoint.Y > this.Height - lineFeed)
						{
						drawPoint.Y = lineTop;
						currentPhase++;
						}
					}
				}

			// Кончился текст строки и задержка отображения
			else if (steps > StringsSet[0][0].StringLength + StringsSet[0][0].Pause)
				{
				// Переход к следующей текстовой строке
				StringsSet[0].RemoveAt (0);
				steps = 0;
				drawPoint.X = lineLeft;
				drawPoint.Y += lineFeed;

				// Обработка смены экрана
				if (drawPoint.Y > this.Height - lineFeed)
					{
					drawPoint.Y = lineTop;
					currentPhase++;
					}
				}

			// Кончился только текст строки, пауза
			else
				{
				steps++;
				}
			}

		// Метод загружает конфигурацию для отображения
		private int LoadConfig ()
			{
			int err = -1;

			// Открытие файла
			FileStream FS = null;
			try
				{
				FS = new FileStream (OFConfig.FileName, FileMode.Open);
				}
			catch
				{
				return err;
				}
			StreamReader SR = new StreamReader (FS, Encoding.GetEncoding (1251));

			// Чтение настроек
			string[] backColor = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);	// Цвет фона лого
			string[] foreColor = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);	// Цвет текста лого
			string[] plotColor1 = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);	// Цвет изображения 1
			string[] plotColor2 = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);	// Цвет изображения 2
			string[] textColor = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);	// Цвет текста
			string[] fontSizes = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);	// Размеры шрифтов

			err = -100;
			try
				{
				logoBackBrush = new SolidBrush (Color.FromArgb (255, byte.Parse (backColor[0]),
					byte.Parse (backColor[1]), byte.Parse (backColor[2])));
				err--;
				logoForeBrush = new SolidBrush (Color.FromArgb (255, byte.Parse (foreColor[0]),
					byte.Parse (foreColor[1]), byte.Parse (foreColor[2])));
				err--;	// -102
				plotBackBrushes.Add (new SolidBrush (Color.FromArgb (20, byte.Parse (plotColor1[0]),
					byte.Parse (plotColor1[1]), byte.Parse (plotColor1[2]))));
				err--;
				plotGradient1Brush = new SolidBrush (Color.FromArgb (20, byte.Parse (plotColor2[0]),
					byte.Parse (plotColor2[1]), byte.Parse (plotColor2[2])));
				err--;	// -104
				textBrush = new SolidBrush (Color.FromArgb (255, byte.Parse (textColor[0]),
					byte.Parse (textColor[1]), byte.Parse (textColor[2])));
				err--;

				textFontSize = uint.Parse (fontSizes[0]);
				if (textFontSize < LogoDrawerSupport.MinFontSize)
					textFontSize = LogoDrawerSupport.MinFontSize;
				if (textFontSize > LogoDrawerSupport.MaxFontSize)
					textFontSize = LogoDrawerSupport.MaxFontSize;
				err--;	// -106

				signatureFontSize = uint.Parse (fontSizes[1]);
				if (signatureFontSize < LogoDrawerSupport.MinFontSize)
					signatureFontSize = LogoDrawerSupport.MinFontSize;
				if (signatureFontSize > LogoDrawerSupport.MaxFontSize)
					signatureFontSize = LogoDrawerSupport.MaxFontSize;
				err--;

				centerizeText = (uint.Parse (fontSizes[2]) != 0);
				err--;	// -108
				}
			catch
				{
				return err;
				}

			//////
			string[] metrics1 = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);
			string[] metrics2 = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);
			string[] metrics3 = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);
			string[] metrics4 = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);
			string[] metrics5 = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);
			string[] metrics6 = SR.ReadLine ().Split (splitters, StringSplitOptions.RemoveEmptyEntries);

			try
				{
				objectsMetrics.ObjectsType = (LogoDrawerObjectTypes)byte.Parse (metrics1[0]);
				err--;
				objectsMetrics.ObjectsCount = byte.Parse (metrics1[1]);
				err--;	// -110
				objectsMetrics.PolygonsSidesCount = byte.Parse (metrics1[2]);
				err--;
				objectsMetrics.StartupPosition = (LogoDrawerObjectStartupPositions)uint.Parse (metrics1[3]);
				err--;	// -112
				objectsMetrics.KeepTracks = (uint.Parse (metrics1[4]) != 0);
				err--;
				objectsMetrics.Acceleration = (uint.Parse (metrics1[5]) != 0);
				err--;	// -114
				objectsMetrics.Enlarging = int.Parse (metrics1[6]);
				err--;
				objectsMetrics.MinSpeed = uint.Parse (metrics2[0]);
				err--;	// -116
				objectsMetrics.MaxSpeed = uint.Parse (metrics2[1]);
				err--;
				objectsMetrics.MaxSpeedFluctuation = uint.Parse (metrics2[2]);
				err--;	// -118
				objectsMetrics.MinSize = uint.Parse (metrics3[0]);
				err--;
				objectsMetrics.MaxSize = uint.Parse (metrics3[1]);
				err--;	// -120
				objectsMetrics.MinRed = byte.Parse (metrics4[0]);
				err--;
				objectsMetrics.MaxRed = byte.Parse (metrics4[1]);
				err--;	// -122
				objectsMetrics.MinGreen = byte.Parse (metrics5[0]);
				err--;
				objectsMetrics.MaxGreen = byte.Parse (metrics5[1]);
				err--;	// -124
				objectsMetrics.MinBlue = byte.Parse (metrics6[0]);
				err--;
				objectsMetrics.MaxBlue = byte.Parse (metrics6[1]);
				err--;	// -126
				}
			catch
				{
				return err;
				}

			// Выравнивание метрик
			objectsMetrics = LogoDrawerSupport.AlingMetrics (objectsMetrics);

			// Завершение
			SR.Close ();
			FS.Close ();
			return 0;
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

			// Пропуск настроек
			string s;
			while (((s = SR.ReadLine ()).IndexOf (signatureMarker) != 0) && !SR.EndOfStream)
				;

			// Получение подписи
			signatureStringsSet.Add (new List<LogoDrawerString> ());
			s = s.Replace (" ", "  ").Replace ("|", "\n");
			signatureStringsSet[signatureStringsSet.Count - 1].Add (new LogoDrawerString (s.Substring (1),
				signatureFont, 300, 5));

			// Чтение текста
			mainStringsSet.Add (new List<LogoDrawerString> ());
			currentColor = plotBackBrushes[0].Color;

			while (!SR.EndOfStream)
				{
				s = SR.ReadLine ().Replace (" ", "  ");

				if (s == "")
					{
					continue;
					}
				else if (s[0] == colorMarker)
					{
					string[] newColor = s.Substring (1).Split (splitters, StringSplitOptions.RemoveEmptyEntries);
					try
						{
						currentColor = Color.FromArgb (20, int.Parse (newColor[0]), int.Parse (newColor[1]),
							int.Parse (newColor[2]));
						}
					catch
						{
						}
					}
				else if (s[0] == textMarker)
					{
					uint length = 0;
					for (int i = 0; i < mainStringsSet[mainStringsSet.Count - 1].Count; i++)
						{
						length += mainStringsSet[mainStringsSet.Count - 1][i].StringLength;
						}
					length = (uint)((double)(length + (uint)s.Length - 1) * 2.2);
					if (length < 150)
						length = 150;

					mainStringsSet[mainStringsSet.Count - 1].Add (new LogoDrawerString (s.Substring (1),
						textFont, length, 5));
					mainStringsSet.Add (new List<LogoDrawerString> ());

					plotBackBrushes.Add (new SolidBrush (currentColor));
					}
				else
					{
					mainStringsSet[mainStringsSet.Count - 1].Add (new LogoDrawerString (s, textFont, 0, 5));
					}
				}

			// Завершение
			if ((mainStringsSet.Count > 1) && (mainStringsSet[mainStringsSet.Count - 1].Count == 0))
				mainStringsSet.RemoveAt (mainStringsSet.Count - 1);

			SR.Close ();
			FS.Close ();
			return true;
			}

		// Метод запускает звук эмбиента
		private void PlayAmbience ()
			{
			// Фискация момента вызова
			soundStartFrame = savingLayersCounter;

			// Контроль
			if (!am.IsInited)
				return;

			// 'Живой' звук только в случае отображения на экране
			if (!vm.IsCreated)
				am.PlayAudio ();
			else
				vm.AddAudio (am);
			}

		// Выбор цветов
		private void LogoForeground_Click (object sender, EventArgs e)
			{
			Button btn = (Button)sender;

			ColorPicker.Color = btn.BackColor;
			ColorPicker.ShowDialog ();
			btn.BackColor = ColorPicker.Color;
			btn.ForeColor = GetNearbyColor (btn.BackColor);
			}

		// Отмена сохранения
		private void CancelShow_Click (object sender, EventArgs e)
			{
			if (MessageBox.Show ("Unsaved settings will be lost. Exit?", ProgramDescription.AssemblyTitle,
				MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				this.Close ();
			}

		// Сохранение
		private void SaveShow_Click (object sender, EventArgs e)
			{
			SFConfig.ShowDialog ();
			}

		private void SFConfig_FileOk (object sender, System.ComponentModel.CancelEventArgs e)
			{
			// Создание файла
			FileStream FS = null;
			try
				{
				FS = new FileStream (SFConfig.FileName, FileMode.Create);
				}
			catch
				{
				MessageBox.Show ("Failed to create file '" + SFConfig.FileName + "'", ProgramDescription.AssemblyTitle,
					MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
				}
			StreamWriter SW = new StreamWriter (FS, Encoding.GetEncoding (1251));

			// Запись
			SW.Write (LogoBackground.BackColor.R.ToString () + " " +
				LogoBackground.BackColor.G.ToString () + " " + LogoBackground.BackColor.B.ToString () + "\n");
			SW.Write (LogoForeground.BackColor.R.ToString () + " " +
				LogoForeground.BackColor.G.ToString () + " " + LogoForeground.BackColor.B.ToString () + "\n");
			SW.Write (FirstMainBack.BackColor.R.ToString () + " " +
				FirstMainBack.BackColor.G.ToString () + " " + FirstMainBack.BackColor.B.ToString () + "\n");
			SW.Write (MainGradient.BackColor.R.ToString () + " " +
				MainGradient.BackColor.G.ToString () + " " + MainGradient.BackColor.B.ToString () + "\n");
			SW.Write (MainText.BackColor.R.ToString () + " " +
				MainText.BackColor.G.ToString () + " " + MainText.BackColor.B.ToString () + "\n");

			SW.Write (TextFontSizeField.Value.ToString () + " " + SignatureFontSizeField.Value.ToString () + " " +
				(CenterTextAlign.Checked ? "1" : "0") + "\n");

			SW.Write (ObjectsTypeCombo.SelectedIndex.ToString () + " " + ObjectsCountField.Value.ToString () + " " +
				SidesCountField.Value.ToString () + " " + StartupSideCombo.SelectedIndex.ToString () + " " +
				(KeepTracks.Checked ? "1" : "0") + " " + (Acceleration.Checked ? "1" : "0") + " " +
				EnlargingCoeff.Value.ToString () + "\n");

			SW.Write (MinSpeedField.Value.ToString () + " " + MaxSpeedField.Value.ToString () + " " +
				SpeedFluctuationField.Value.ToString () + "\n");
			SW.Write (MinSizeField.Value.ToString () + " " + MaxSizeField.Value.ToString () + "\n");

			SW.Write (MinColor.BackColor.R.ToString () + " " + MaxColor.BackColor.R.ToString () + "\n");
			SW.Write (MinColor.BackColor.G.ToString () + " " + MaxColor.BackColor.G.ToString () + "\n");
			SW.Write (MinColor.BackColor.B.ToString () + " " + MaxColor.BackColor.B.ToString () + "\n\n\n\n");

			SW.Write (signatureMarker.ToString () + SignatureText.Text + "\n\n");
			SW.Write (MainTextField.Text.Replace ("\r", "").Replace ("  ", " "));

			// Завершено
			SW.Close ();
			FS.Close ();

			if (AndExit.Checked)
				this.Close ();
			}
		}
	}
