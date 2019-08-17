using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ESHQSetupStub
	{
	/// <summary>
	/// Класс предоставляет методы воспроизведения MIDI-команд
	/// </summary>
	public class MIDIManager:IDisposable
		{
		// Константы
		private const uint masterLimit = 0x7F;	// Стандартное ограничение для параметров
		private const uint beatsChannel = 9;	// Канал ударных инструментов

		// Импорты для поддержки функционала
		[DllImport ("winmm.dll")]
		private static extern int midiOutOpen (out IntPtr handle, uint deviceID, IntPtr proc, IntPtr instance, uint flags);

		[DllImport ("winmm.dll")]
		private static extern int midiOutShortMsg (IntPtr handle, uint message);

		[DllImport ("winmm.dll")]
		private static extern int midiOutClose (IntPtr handle);

		// Параметры

		// Метод собирает ноту и октаву в команду
		private uint NoteAndOctaveToCommand (MIDINotes Note, MIDIOctaves Octave)
			{
			return AlignNote ((uint)Octave * 12 + (uint)Note);
			}

		// Методы контролируут вхождение параметров в допустимые диапазоны
		private uint AlignNote (uint NoteAsCommand)
			{
			if (NoteAsCommand > masterLimit)
				return masterLimit;
			return NoteAsCommand;
			}

		private uint AlignVolume (uint Volume)
			{
			if (Volume > masterLimit)
				return masterLimit;
			return Volume;
			}

		private uint AlignDuration (uint Duration)
			{
			if (Duration > 3600000)
				return 3600000;
			return Duration;
			}

		// Метод собирает команду в бинарный вид
		private uint AssembleCommand (MIDICommands Command, uint Channel)
			{
			return (uint)Command | (Channel & 0xFu);
			}

		/// <summary>
		/// Конструктор. Инициализирует менеджер
		/// </summary>
		public MIDIManager ()
			{
			// Пробуем инициализировать экземпляр
			if (midiOutOpen (out midiHandle, 0, IntPtr.Zero, IntPtr.Zero, 0) != 0)
				return;

			// Успешно
			isInited = true;
			}

		/////////////////////////////////////
		// Основные переменные, поля и методы

		private IntPtr midiHandle = IntPtr.Zero;		// Указатель на интерпретатор MIDI-команд

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
		/// Метод освобождает занятые экземпляром ресурсы
		/// </summary>
		public void Dispose ()
			{
			if (isInited)
				{
				midiOutClose (midiHandle);
				isInited = false;
				}
			}

		/// <summary>
		/// Метод воспроизводит указанную ноту
		/// </summary>
		/// <param name="Note">Нота</param>
		/// <param name="Octave">Октава</param>
		/// <param name="Volume">Громкость (0 – 127)</param>
		/// <param name="Duration">Длительность в миллисекундах</param>
		/// <param name="Channel">Канал воспроизведения (0 – 15)</param>
		/// <param name="WaitForNextNote">Время ожидания до начала следующей ноты в миллисекундах</param>
		/// <returns>Возвращает true в случае успеха</returns>
		public bool PlayNote (uint Channel, MIDINotes Note, MIDIOctaves Octave, uint Volume, uint Duration, uint WaitForNextNote)
			{
			// Контроль
			if (!isInited || ((Channel & 0xFu) == beatsChannel))
				return false;

			if (Volume * Duration == 0)
				return true;

			// Запуск
			HardWorkExecutor hwe = new HardWorkExecutor (PlayNoteStub, new uint[] {NoteAndOctaveToCommand (Note, Octave),
				Volume, Duration, Channel});

			if (WaitForNextNote > 0)
				Thread.Sleep ((int)WaitForNextNote);

			return true;
			}

		/// <summary>
		/// Метод воспроизводит указанный бит
		/// </summary>
		/// <param name="BeatInstrument">Инструмент бита</param>
		/// <param name="Volume">Громкость (0 – 127)</param>
		/// <param name="Duration">Длительность в миллисекундах</param>
		/// <param name="WaitForNextNote">Время ожидания до начала следующей ноты в миллисекундах</param>
		/// <returns>Возвращает true в случае успеха</returns>
		public bool PlayBeat (MIDIBeatsInstruments BeatInstrument, uint Volume, uint Duration, uint WaitForNextNote)
			{
			// Контроль
			if (!isInited)
				return false;

			if (Volume * Duration == 0)
				return true;

			// Запуск
			HardWorkExecutor hwe = new HardWorkExecutor (PlayNoteStub, new uint[] { (uint)BeatInstrument,
				Volume, Duration, beatsChannel });

			if (WaitForNextNote > 0)
				Thread.Sleep ((int)WaitForNextNote);

			return true;
			}

		private void PlayNoteStub (object sender, DoWorkEventArgs e)
			{
			// Получение параметров
			uint[] parameters;
			uint currentNote, currentVolume, currentDuration, currentChannel;

			try
				{
				parameters = (uint[])e.Argument;
				currentNote = AlignNote (parameters[0]);
				currentVolume = AlignVolume (parameters[1]);
				currentDuration = AlignDuration (parameters[2]);
				currentChannel = parameters[3];
				}
			catch
				{
				return;
				}

			// Запуск ноты
			midiOutShortMsg (midiHandle, AssembleCommand (MIDICommands.ВключитьНоту, currentChannel) |
				(currentNote << 8) | (currentVolume << 16));

			// Пауза
			Thread.Sleep ((int)currentDuration);

			// Остановка ноты
			midiOutShortMsg (midiHandle, AssembleCommand (MIDICommands.ВыключитьНоту, currentChannel) | (currentNote << 8));

			// Завершено
			e.Result = null;
			}

		/// <summary>
		/// Метод определяет ноту MIDI по указанной звуковой частоте
		/// </summary>
		/// <param name="Frequency">Частота звука</param>
		/// <returns>Нота в виде команды для воспроизведения</returns>
		public uint NoteAsCommandFromFrequency (double Frequency)
			{
			return AlignNote ((uint)Math.Round (69.0 + 12.0 * (Math.Log (Frequency / 440.0) / logBase)));
			}
		private double logBase = Math.Log (2.0);

		/// <summary>
		/// Метод устанавливает инструмент для указанного канала
		/// </summary>
		/// <param name="Channel">Канал</param>
		/// <param name="Instrument">Инструмент</param>
		/// <returns>Возвращает true в случае успеха</returns>
		public bool SetInstrument (uint Channel, MIDIInstruments Instrument)
			{
			// Контроль
			if (!isInited || ((Channel & 0xFu) == beatsChannel))
				return false;

			// Назначение
			midiOutShortMsg (midiHandle, AssembleCommand (MIDICommands.ИзменениеЗвуковойПрограммы, Channel) | ((uint)Instrument << 8));
			return true;
			}

		/// <summary>
		/// Метод преобразует текстовую строку в проигрываемую мелодию и запускает воспроизведение
		/// </summary>
		/// <param name="Text">Преобразуемая строка</param>
		/// <param name="Volume">Громкость (0 –127)</param>
		/// <param name="NoteDuration">Длительность каждой ноты в миллисекундах</param>
		/// <param name="Channel">Канал воспроизведения</param>
		/// <returns>Возвращает true в случае успеха</returns>
		public bool PlayText (string Text, uint Channel, uint Volume, uint NoteDuration)
			{
			// Контроль
			if (!isInited || ((Channel & 0xFu) == beatsChannel))
				return false;

			if ((Volume * NoteDuration == 0) || (Text == null) || (Text == ""))
				return true;

			// Сборка нот для проигрывания
			byte[] values = Encoding.Default.GetBytes (Text.ToCharArray ());
			List<uint> message = new List<uint> ();
			message.Add (Channel);
			message.Add (Volume);

			for (int i = 0; i < values.Length; i++)
				{
				if (values[i] > 128)
					{
					// Нужен нормальный алгоритм преобразования
					/*message.Add ((uint)values[i] % 12 + NoteAndOctaveToCommand (MIDINotes.До, MIDIOctaves.Первая));*/
					message.Add (NoteDuration);
					}
				else
					{
					message[message.Count - 1] += NoteDuration;
					}
				}
			message[message.Count - 1] += (2 * NoteDuration);

			// Запуск
			HardWorkExecutor hwe = new HardWorkExecutor (PlayTextStub, message);
			return true;
			}

		private void PlayTextStub (object sender, DoWorkEventArgs e)
			{
			// Получение параметров
			List<uint> parameters;
			uint currentVolume, currentChannel;

			try
				{
				parameters = (List<uint>)e.Argument;
				currentChannel = parameters[0];
				currentVolume = AlignVolume (parameters[1]);
				}
			catch
				{
				return;
				}

			// Проигрывание
			for (int i = 2; i < parameters.Count; i += 2)
				{
				HardWorkExecutor hwe2 = new HardWorkExecutor (PlayNoteStub, new uint[] { parameters [i],
					currentVolume, parameters [i + 1] , currentChannel });

				Thread.Sleep ((int)parameters[i + 1]);
				}

			// Завершено
			e.Result = null;
			}
		}
	}
