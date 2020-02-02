using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using FileInformation;

namespace BackupClient
{
    /// <summary>
    /// Перечисление из констант, служащих командами управления 
    /// между программами клиента и сервера.
    /// </summary>
    enum SERVICE_COMMAND
    {
        SCAN = 1,                                       // Сканирование данных.
        SYNC,                                           // Синхронизация данных.
        END_SYNC,                                       // Конец сеанса синхронизации данных.
        EXIT                                            // Завершение сеанса работы программы.
    }

    /// <summary>
    /// Определяет работу клиентской части приложения резервного 
    /// копирования данных.
    /// </summary>
    class Client
    {
        #region Fields
        IPEndPoint ipEndPoint;                          // Конечная сетевая точка.
        TcpClient tcpClient;                            // Клинтский сокет.

        NetworkStream netStream;                        // Поток для передачи данных по сети.
        FileStream fileStream;                          // Поток для создания/чтения/записи/ данных.
        #endregion

        #region Constants
        const int BUF_SIZE = 2048;                      // Максимальный размер буфера для передачи данных.
        const int nSymbols = 80;                        // Размер символов выводимых на экран (вспомагательная константа).
        const string CLIENT_LOG = "client.xml";         // Имя лог-файла.
        #endregion

        #region Properties
        public string UserName { get; set; }            // Имя пользователя.
        public string ClientLogFileName { get; set; }   // Полное имя лог-файла с указанным путем. 
        #endregion

        #region Constructor
        /// <summary>
        /// Конструктор с параметрами инициализирующий клиентский сокет.
        /// </summary>
        /// <param name="ip">IP-адресс сервера</param>
        /// <param name="port">Порт сервера</param>
        public Client(string ip, int port)
        {
            // Инициализируем локальную конечною точку.
            ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            // Инициализируем сокет.
            tcpClient = new TcpClient();     
        }
        #endregion

        #region Methods
        /// <summary>
        /// Устанавливает соединение с сервером.
        /// </summary>
        public void ConnectToServer()
        {
            try
            {
                PrintLines();
                Console.WriteLine("Client: {0}", ipEndPoint);
                PrintLines();
                
                // Устанавливаем соединение с сервером.
                tcpClient.Connect(ipEndPoint);

                Console.WriteLine("Connect to {0} success", ipEndPoint);
                PrintLines();

                // Получаем поток для чтения/записи данных по сети.
                netStream = tcpClient.GetStream();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0} - {1}", ex.Message, ipEndPoint);
                return;
            }
        }

        /// <summary>
        /// Разрываем соединение с сервером.
        /// </summary>
        public void Disconnect()
        {
            // Отправляем на сервер команду о завершении работы.
            SendServiceCommand((int)SERVICE_COMMAND.EXIT);

            // Закрываем активные потоки.
            tcpClient.Close();
            netStream.Close();
        }

        /// <summary>
        /// Отправляет указанный файл на сервер.
        /// </summary>
        /// <param name="fileName">Имя отправляемого файла.</param>
        void SendFile(string fileName)
        {
            // Получаем информацию о файле.
            var fileInfo = new FileInfo(fileName);

            Console.WriteLine("Sending the file {0}", fileInfo.Name + " " + fileInfo.Length);

            // Инстанцируем класс FileStruct описывающий структуру файла для отправки (Имя/Размер).
            var fileStruct = new FileStruct(GetFilePath(fileInfo.FullName), fileInfo.Length);

            // Инстанцируем класс BinaryFormatter, который будем использовать для бинарной сериализации объекта.
            var binaryFormatter = new BinaryFormatter();

            // Сериализируем объект в поток NetStream.
            binaryFormatter.Serialize(netStream, fileStruct);

            // Получаем поток на чтение файла.
            fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);

            // Буфер хранимый данные для отправки.
            byte[] temp = new byte[BUF_SIZE];

            // Кол-во считанных байт.
            int readBytes = 0;

            // Цикл отправки файла по сети.
            // Считываем данные c открытого файла в временный буфер и отправляем их до того момента
            // пока не считаем все данные.
            while ((readBytes = fileStream.Read(temp, 0, temp.Length)) > 0)
            {
                // Записываем данные в поток NetStream (передаем на сервер).
                netStream.Write(temp, 0, readBytes);
            }
            // Закрываем поток чтения файла.
            fileStream.Close();
        }

        /// <summary>
        /// Отправляет имя активного пользователя на сервер.
        /// </summary>
        void SendUserName()
        {
            // Кодируем имя пользователя в массив байт.
            byte[] sendBuff = Encoding.Default.GetBytes(UserName.ToCharArray());
            // Передаем данные на сервер.
            netStream.Write(sendBuff, 0, sendBuff.Length);
        }   

        /// <summary>
        /// Отправляет команду управления на сервер.
        /// </summary>
        /// <param name="command">Команда управления.</param>
        void SendServiceCommand(int command)
        {
            // Кодируем команду в массив байт.
            byte[] sendBuff = Encoding.Default.GetBytes(command.ToString());
            // Отправляем данные на сервер.
            netStream.Write(sendBuff, 0, sendBuff.Length);
        } 

        /// <summary>
        /// Получает сообщение с сервера.
        /// </summary>
        /// <returns>Возваращает полученное сообщение в виде строки.</returns>
        public string ReciveServiceString()
        {
            // Массив байт для хранения полученных данных с сервера.
            byte[] recvBuf = new byte[BUF_SIZE];
            // Считываем данные из потока NetStream (получаем данные отправленные сервером).
            int recvBytes = netStream.Read(recvBuf, 0, recvBuf.Length);
            // Возвращаем данные предварительно декодируя их с байтового представления в строку урезая все '\0' символы.
            return Encoding.Default.GetString(recvBuf).TrimEnd('\0');
        }

        /// <summary>
        /// Сканирует директорию пользователя.
        /// </summary>
        public void Scan()
        {
            Console.WriteLine("Scanning...");

            // Отправляем команду 'scan' серверу.
            SendServiceCommand((int)SERVICE_COMMAND.SCAN);
            
            // Инстанцируем класс описывающий структуру лог-файла.
            var logFile = new LogFile();

            // Инстанцируем класс реализации хеш-алгоритма MD5.
            var md5Hash = MD5.Create();

            // Цикл прохода по всем файлам содержащихся в каталоге активного пользователя (включая подкаталоги).
            foreach (var file in new DirectoryInfo(UserName).GetFiles("*", SearchOption.AllDirectories))
            {
                // Получаем поток на чтения данных из файла.
                fileStream = File.OpenRead(file.FullName);

                // Заполняем поля лог-файла.
                logFile.fileList.Add(new BackupFile(GetFilePath(file.FullName), BitConverter.ToString(md5Hash.ComputeHash(fileStream)).Replace("-","")));

                // Закрываем активный поток чтения данных из файла.
                fileStream.Close();     
            }

            // Удаляем из списка файлов имя лог-файла.
            // Данное действие необходимо для того, чтобы при последующих запусках программы данный файл не был включен
            // в список проверяемых сервером файлов и при синхронизации не отправлялся серверу.
            logFile.fileList.Remove(logFile.fileList.Find(file => file.FilePath == ClientLogFileName));

            // Инстанцируем класс xml сериализации, передавая в конструктор ссылку на тип данных, который будет сериализирован.
            var xml = new XmlSerializer(typeof(LogFile));

            // Получаем поток на созданный лог-файл.
            fileStream = new FileStream(ClientLogFileName, FileMode.Create);

            // Сериализируем экземпляр класса LogFile в открытый ранее поток, тем самым заполняя файл данными.
            xml.Serialize(fileStream, logFile);

            //TODO: Посмотреть целесообразно ли закрывать здесь поток, так как этот файл будет использоваться далее.
            // Закрываем активный поток записи данных в файл.
            fileStream.Close();
            
            // Отправляем серверу созданный клиентский лог-файл.
            SendFile(ClientLogFileName);

            Console.WriteLine("Scanning was compleate");
            PrintLines();
        }

        /// <summary>
        /// Синхронизирует данные директории активного пользователя с сервером.
        /// </summary>
        public void Sync()
        {
            Console.WriteLine("Sync...");
            // Отправляем серверу команду: 'sync'.
            SendServiceCommand((int)SERVICE_COMMAND.SYNC);

            string fileName = null;

            // Цикл получения имени файла запрашиваемого сервером и его отправка.
            // Отправка осуществляется до того момента, пока сервер не отправит команду завершения синхронизации.
            while ((fileName = ReciveServiceString()) != ((int)SERVICE_COMMAND.END_SYNC).ToString())
            {
                // Отправка файла на сервер.
                SendFile(fileName);
            }

            Console.WriteLine("Sync was compleate");
            PrintLines();
        }

        /// <summary>
        /// Осуществляет авторизацию пользователя.
        /// </summary>
        public void UserLogin()
        {
            Console.Write("Input user name: ");
          
            UserName = Console.ReadLine();
            
            // Задаем имя лог-файла.
            ClientLogFileName = UserName + Path.DirectorySeparatorChar + CLIENT_LOG;
            
            // Отправляем имя пользователя на сервер.
            SendUserName();

            // Проверяем существование директории пользователя.
            // Если директория не существует.
            if (!Directory.Exists(UserName))
            {
                try
                {
                    // Создаем директорию.
                    Directory.CreateDirectory(UserName);
                    Console.WriteLine("Directory {0} was created!", UserName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Can't to creat directory:\n", ex.Message);
                }
            }
            else
            {
                Console.WriteLine("Current user: {0}", UserName);
            }

            PrintLines();
        }

        /// <summary>
        /// Создает корректный путь к файлу обрезая существующий до имени пользователя.
        /// </summary>
        /// <param name="path">Старый путь к файлу.</param>
        /// <returns>Новый корректный путь к файлу.</returns>
        private string GetFilePath(string path)
        {   
            return path.Remove(0, path.IndexOf(UserName));
        }

        /// <summary>
        /// Вспомагательный метод.
        /// Выводит линию на косоль.
        /// </summary>
        private void PrintLines()
        {
            Console.Write(new string('-', nSymbols));
        }
        #endregion
    }
}