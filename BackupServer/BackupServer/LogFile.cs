using System;
using System.Collections.Generic;

namespace BackupServer
{
    /// <summary>
    /// Описывает структуру лог-файла.
    /// Атрибут Serializable позволяет сериализировать объект данного класса.
    /// </summary>
    [Serializable()]
    public class LogFile
    {
        // Список закрытый типом BackupFile содержащий служебную информацию о файлах.
        public List<BackupFile> fileList;

        /// <summary>
        /// Конструктор по умолчанию. 
        /// </summary>
        public LogFile()
        {
            // Инициализируем переменную ссылкой на экземпляр класса List<T> закрывая его типом BackupFile.
            fileList = new List<BackupFile>();
        }

        /// <summary>
        /// Индексатор получения доступу к элементам списка.
        /// </summary>
        /// <param name="index">Индекс прохода по элементам списка.</param>
        /// <returns>Ссылка на экземпляр класса BackupFile.</returns>
        public BackupFile this[int index]
        {
            // Аксессор.
            get { return fileList[index]; }
        }
    }

    /// <summary>
    /// Описывает инфомацию о конкретном файле.
    /// </summary>
    public class BackupFile
    {
        // Путь к файлу.
        public string FilePath { get; set; }
        // Hash-code файла.
        public string FileHash { get; set; }

        /// <summary>
        /// Конструтор по-умолчанию.
        /// </summary>
        public BackupFile() { }

        /// <summary>
        /// Конструктор с параметрами.
        /// </summary>
        /// <param name="filePath">Путь к файлу.</param>
        /// <param name="fileHash">Hash-code файла.</param>
        public BackupFile(string filePath, string fileHash)
        {
            this.FilePath = filePath;
            this.FileHash = fileHash;
        }
    }
}
