using System;
using System.Windows.Forms;

namespace ESHQSetupStub
	{
	/// <summary>
	/// Форма обеспечивает получение параметров работы программы от пользователя
	/// </summary>
	public partial class ParametersPicker:Form
		{
		/// <summary>
		/// Конструктор. Запускает форму для указания параметров
		/// </summary>
		public ParametersPicker ()
			{
			// Инициализация
			InitializeComponent ();
			this.Text = "Specify visualization parameters";

			// Запуск
			this.ShowDialog ();
			}

		// Закрытие формы
		private void BClose_Click (object sender, EventArgs e)
			{
			writeFramesToAVI = WriteToAVI.Checked;
			playAttachedAmbience = PlayAmbience.Checked;
			adjustShowParameters = AdjustParameters.Checked;
			this.Close ();
			}

		/// <summary>
		/// Возвращает флаг необходимости записи фреймов в видеофайл
		/// </summary>
		public bool WriteFramesToAVI
			{
			get
				{
				return writeFramesToAVI;
				}
			}
		private bool writeFramesToAVI = false;

		/// <summary>
		/// Возвращает флаг необходимости запуска эмбиента
		/// </summary>
		public bool PlayAttachedAmbience
			{
			get
				{
				return playAttachedAmbience;
				}
			}
		private bool playAttachedAmbience = false;

		/// <summary>
		/// Возвращает флаг настройки параметров демонстрации
		/// </summary>
		public bool AdjustShowParameters
			{
			get
				{
				return adjustShowParameters;
				}
			}
		private bool adjustShowParameters = false;

		// Установка флага настройки параметров
		private void AdjustParameters_CheckedChanged (object sender, EventArgs e)
			{
			if (AdjustParameters.Checked)
				{
				WriteToAVI.Checked = WriteToAVI.Enabled =
					PlayAmbience.Checked = PlayAmbience.Enabled = false;
				}
			else
				{
				WriteToAVI.Enabled = PlayAmbience.Enabled = true;
				}
			}
		}
	}
