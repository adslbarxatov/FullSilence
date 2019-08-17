namespace ESHQSetupStub
	{
	/// <summary>
	/// Список доступных команд
	/// </summary>
	public enum MIDICommands
		{
		/// <summary></summary>
		ВыключитьНоту = 0x80,
		/// <summary></summary>
		ВключитьНоту = 0x90,
		/// <summary></summary>
		ПолифоническаяРетушь = 0xA0,
		/// <summary></summary>
		ИзменениеКонтроллераИРежима = 0xB0,
		/// <summary></summary>
		ИзменениеЗвуковойПрограммы = 0xC0,
		/// <summary></summary>
		РетушьКанала = 0xD0,
		/// <summary></summary>
		ИзменениеВысотыТона = 0xE0

		// Остальные зарезервированы системой
		}
	}
