using System;
using System.Media;

namespace ESHQSetupStub
	{
#if AUDIO || VIDEO
	/// <summary>
	/// Класс предоставляет методы воспроизведения аудиофайлов
	/// </summary>
	public class AudioManager:IDisposable
		{
		// Переменные
		private SoundPlayer ambient = new SoundPlayer ();	// Эмбиент
		private bool loop;									// Флаг петли

		/// <summary>
		/// Состояние инициализации экземпляра менеджера
		/// </summary>
		public bool IsInited
			{
			get
				{
				return isInited;
				}
			}
		private bool isInited = false;

		/// <summary>
		/// Конструктор. Инициализирует аудиофайл
		/// </summary>
		/// <param name="FilePath">Путь к файлу</param>
		/// <param name="Loop">Флаг циклического воспроизведения</param>
		public AudioManager (string FilePath, bool Loop)
			{
			// Сохранение параметров
			loop = Loop;

			// Попытка инициализации
			ambient.SoundLocation = FilePath;
			try
				{
				ambient.Load ();
				}
			catch
				{
				return;
				}

			// Успешно
			isInited = true;
			}

		/// <summary>
		/// Конструктор. Инициализирует пустой менеджер для заполнения полей
		/// </summary>
		public AudioManager ()
			{
			// Doing nothing
			}

		/// <summary>
		/// Освобождает занятые экземпляром ресурсы
		/// </summary>
		public void Dispose ()
			{
			if (isInited)
				{
				ambient.Stop ();
				ambient.Dispose ();
				isInited = false;
				}
			}

		/// <summary>
		/// Возвращает путь к аудиофайлу
		/// </summary>
		public string AudioFilePath
			{
			get
				{
				if (!isInited)
					return "";

				return ambient.SoundLocation;
				}
			}

		/// <summary>
		/// Метод запускает воспроизведение аудиофайла
		/// </summary>
		public void PlayAudio ()
			{
			if (!isInited)
				return;

			if (loop)
				ambient.PlayLooping ();
			else
				ambient.Play ();
			}

		/// <summary>
		/// Метод останавливает вопроизведение аудиофайла
		/// </summary>
		public void StopAudio ()
			{
			if (isInited)
				ambient.Stop ();
			}
		}
#endif
	}
