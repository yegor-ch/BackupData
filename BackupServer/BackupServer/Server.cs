using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using FileInformation;

namespace BackupServer
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
    /// Определяет работу серверной части приложения
    /// резервного копирования данных.
    /// </summary>
    class Server
    {
        #region Fields
        IPEndPoint ipEndPoint;                          // Конечная сетевая точка.
        TcpListener tcpServer;                          // Серверный сокет.
        TcpClient tcpClient;                            // Клинтский сокет.

        NetworkStream netStream;                        // Поток для передачи данных по сети.
        FileStream fileStream;                          // Поток для создания/чтения/записи/ данных.
        #endregion

        #region Constants
        const int BUF_SIZE = 2048;                      // Максимальный размер буфера для передачи данных.
        const int nSymbols = 80;                        // Размер символов выводимых на экран (вспомагательная константа).

        const string SERVER_LOG = "server.xml";         // Имя лог-файла сервера.
        const string CLIENT_LOG = "client.xml";         // Имя лог-файла клиента.
        #endregion

        #region Properties
        public string UserName { get; set; }            // Имя пользователя.
        public string ServerLogFileName { get; set; }   // Полное имя лог-файла сервера с указанным путем. 
        public string ClientLogFileName { get; set; }   // Полное имя лог-файла клиента с указанным путем. 
        #endregion

        #region Constructor
        /// <summary>
        /// Конструктор с параметрами инициализирующий серверный сокет.
        /// </summary>
        /// <param name="port">Порт</param>
        public Server(int port)
        {
            // Инициализируем локальную конечную точку.
            ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            // Инициализируем сокет.
            tcpServer = new TcpListener(ipEndPoint);
        }
        #endregion

        #region Destructor
        /// <summary>
        /// Деструктор.
        /// </summary>
        ~Server()
        {
            // Закрываем активный сетевой поток.
            netStream.Close();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Устанавлевает серверный сокет в режим прослушивания и извлекает 
        /// из очереди запрос на подключения.
        /// </summary>
        public void ListenConnection()
        {
            try
            {
                // Ожидаем входящие запросы на подключение.
                tcpServer.Start();

                PrintLines();
                Console.WriteLine("Server: {0}", ipEndPoint);
                PrintLines();

                // Извлекаем ожидающий запрос на подключение.
                tcpClient = tcpServer.AcceptTcpClient();

                Console.WriteLine("Client [{0}] - connected!", tcpClient.Client.RemoteEndPoint);
                PrintLines();

                // Получаем поток для чтения/записи данных по сети. 
                netStream = tcpClient.GetStream();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        /// <summary>
        /// Осуществляет авторизацию пользователя.
        /// </summary>
        public void UserLogin()
        {
            // Получаем имя пользователя.
            UserName = ReciveServiceCommand();

            // Задаем имя лог-файлов.
            ServerLogFileName = UserName + Path.DirectorySeparatorChar + SERVER_LOG;
            ClientLogFileName = UserName + Path.DirectorySeparatorChar + CLIENT_LOG;

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
                    Console.WriteLine("Can't to creat directory:\n{0}", ex.Message);
                }
            }
            else
            {
                Console.WriteLine("Current user: {0}", UserName);
            }

            PrintLines();
        }

        /// <summary>
        /// Отправляет имя файла клиенту.
        /// </summary>
        /// <param name="fileName">Имя файла.</param>
        public void SendFileName(string fileName)
        {
            // Кодируем имя пользователя в массив байт.
            byte[] sendBuf = Encoding.Default.GetBytes(fileName.ToCharArray());
            
            // Отправляем данные клиенту.
            netStream.Write(sendBuf, 0, sendBuf.Length);
        }

        /// <summary>
        /// Отправляет команду управления клиенту.
        /// </summary>
        /// <param name="command">Команда управления.</param>
        private void SendServiceCommand(int command)
        {
            // Кодируем команду в массив байт.
            byte[] sendBuff = Encoding.Default.GetBytes(command.ToString());
            
            // Отправляем данные клиенту.
            netStream.Write(sendBuff, 0, sendBuff.Length);
        }

        /// <summary>
        /// Получает сообщение от клиента.
        /// </summary>
        /// <returns>Возваращает полученное сообщение в виде строки.</returns>
        public string ReciveServiceCommand()
        {
            // Массив байт для хранения полученных данных от клиента.
            byte[] recvBuf = new byte[BUF_SIZE];
            // Считываем данные из потока NetStream (получаем данные отправленные клиентом).
            int recvBytes = netStream.Read(recvBuf, 0, recvBuf.Length);
            // Возвращаем данные предварительно декодируя их с байтового представления в строку урезая все '\0' символы.
            return Encoding.ASCII.GetString(recvBuf).TrimEnd('\0');
        }

        /// <summary>
        /// Получает файл от клиента.
        /// </summary>
        public void ReciveFile()
        {
            // Инстанцируем класс FileStruct описывающий структуру файла для отправки (Имя/Размер).
            var fileStruct = new FileStruct();

            // Инстанцируем класс BinaryFormatter, который будем использовать для десериализации объекта.
            var binaryFormatter = new BinaryFormatter();

            // Десериализируем объект.
            fileStruct = binaryFormatter.Deserialize(netStream) as FileStruct;

            // Извлекаем имя файла.
            var fileName = fileStruct.FileName;
            // Извлекаем размер файла.
            var fileSize = fileStruct.FileSize;

            // Создаем директории для файла исходя из его пути.
            CreateClientDirectories(fileName);

            // Получаем поток на созданный файл..
            fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);

            // Буфер хранимый принятые данные.
            byte[] tempRecvBuf = new byte[BUF_SIZE];
           
            // Кол-во принятых байт.
            int recvBytes = 0;

            // Цикл получения файла.
            while (fileSize != 0)
            {
                // Считываем данные с потока NetStream и записываем их в буфер.
                recvBytes = netStream.Read(tempRecvBuf, 0, tempRecvBuf.Length);
                
                // Пишем данные в поток созданного файла.
                fileStream.Write(tempRecvBuf, 0, recvBytes);

                // Декрементируем размер файла, тем самым опеделяя конец файла.
                fileSize -= recvBytes;
            }

            // Закрываем поток записи файла.
            fileStream.Close();
        }

        /// <summary>
        /// Получает команду от пользователя.
        /// </summary>
        public void GetClientCommand()
        {         
            // Цикл получения команды от пользователя.
            // Будет работать до тех пор пока клиент не отправит команду о завершении сеанса.
            while (true)
            {
                // Получаем команду отправленную клиентом.
                SERVICE_COMMAND command = (SERVICE_COMMAND)int.Parse(ReciveServiceCommand());

                Console.WriteLine("User command: {0}", command);

                if (command == SERVICE_COMMAND.SCAN)
                {
                    // Сканируем директорию пользователя.  
                    Scan();
                }
                else if (command == SERVICE_COMMAND.SYNC)
                {
                    // Синхронизируем данные клиента и сервера.
                    Sync();
                }
                else if (command == SERVICE_COMMAND.EXIT)
                {
                    PrintLines();
                    Console.WriteLine("Client closed the connection");
                    Console.Read();

                    // Завершаем работу сервера.
                    return;
                }
                PrintLines();
            }
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
        /// Сканирует директорию пользователя.
        /// </summary>
        public void Scan()
        {
            Console.WriteLine("Scanning...");

            // Инстанцируем класс описывающий структуру лог-файла.
            var serverLogFile = new LogFile();

            // Инстанцируем класс реализации хеш-алгоритма MD5.
            MD5 md5Hash = MD5.Create();

            // Цикл прохода по всем файлам содержащихся в каталоге активного пользователя (включая подкаталоги).
            foreach (var file in new DirectoryInfo(UserName).GetFiles("*.*", SearchOption.AllDirectories))
            {
                // Получаем поток на чтения данных из файла.
                fileStream = File.OpenRead(file.FullName);

                // Заполняем поля лог-файла.
                serverLogFile.fileList.Add(new BackupFile(GetFilePath(file.FullName), BitConverter.ToString(md5Hash.ComputeHash(fileStream)).Replace("-", "")));

                // Закрываем активный поток чтения данных из файла.
                fileStream.Close();
            }

            // Удаляем из списка файлов имена лог-файлов клиента и сервера.
            // Данное действие необходимо для того, чтобы при последующих запусках программы данный файл не был включен
            // в список проверяемых файлов и при синхронизации не удалялся, так как возможна ситуация, когда пользователь
            // вподряд отправит команду 'sync', которая удалит эти файлы, что приведет к ошибке ибо не будет файла, который
            // нужно будет десериализировать.
            serverLogFile.fileList.Remove(serverLogFile.fileList.Find(file => file.FilePath == ServerLogFileName));
            serverLogFile.fileList.Remove(serverLogFile.fileList.Find(file => file.FilePath == ClientLogFileName));

            // Инстанцируем класс xml сериализации, передавая в конструктор ссылку на тип данных, который будет сериализирован.
            var xml = new XmlSerializer(typeof(LogFile));

            // Получаем поток на созданный лог-файл.
            fileStream = new FileStream(ServerLogFileName, FileMode.Create);

            // Сериализируем экземпляр класса LogFile в открытый ранее поток, тем самым заполняя файл данными.
            xml.Serialize(fileStream, serverLogFile);

            // Закрываем активный поток записи данных в файл.
            fileStream.Close();

            // Получаем от клиента созданный клиентский лог-файл.
            ReciveFile();

            Console.WriteLine("Scanning compleate");
        }

        /// <summary>
        /// Синхронизирует данные директории активного пользователя с клиентом.
        /// </summary>
        private void Sync()
        {
            Console.WriteLine("Sync...");

            // Получаем массив имен всех файлов, которые необходимо скопировать от клиента.
            string[] newFiles = GetBackupFilesName();

            // Цикл запроса запроса и получения файлов от клиента.
            foreach(var newFile in newFiles)
            {
                // Отправляем имя файла.
                SendFileName(newFile);
                // Получаем файл.
                ReciveFile();
            }

            // Отправляем клиенту команду о завершении синхронизации данных. 
            SendServiceCommand((int)SERVICE_COMMAND.END_SYNC);

            Console.WriteLine("Sync was compleate");
        }

        /// <summary>
        /// Осуществляет анализ лог-файлов определяя файлы для копирования/удаления.
        /// </summary>
        /// <returns>Массив имен файлов подлежащих копированию.</returns>
        public string[] GetBackupFilesName()
        {
            #region Deserialization objects
            
            // Инстанцируем класс xml сериализации, передавая в конструктор ссылку на тип данных, который будет десериализирован.
            var xml = new XmlSerializer(typeof(LogFile));

            // Получаем поток на открытый файл для чтения.
            fileStream = File.OpenRead(ClientLogFileName);

            // Десериализируем объект.
            var clientLogFile = xml.Deserialize(fileStream) as LogFile;

            // Закрваем поток чтения данных из файла.
            fileStream.Close();

            // Получаем поток на открытый файл для чтения.
            fileStream = File.Open(ServerLogFileName, FileMode.Open, FileAccess.Read);

            // Десериализируем объект.
            var serverLogFile = xml.Deserialize(fileStream) as LogFile;

            // Закрваем поток чтения данных из файла.
            fileStream.Close();
            #endregion

            // Список имен элементов, которые необходимо скопировать.
            List<string> newFiles = new List<string>();

            // Цикл поиска элементов, которые подлежат копированию/удалению.
            for (int i = 0; i < clientLogFile.fileList.Count; i++)
            {
                // Ищем клиентский файл на сервере. 
                int fileIndex = serverLogFile.fileList.FindIndex(x => x.FilePath == clientLogFile[i].FilePath);
                
                // Если файл на сервере найден.
                if ( fileIndex != -1)
                {
                    // Если hashcod'ы разные, копируем файл.
                    if(serverLogFile[fileIndex].FileHash != clientLogFile[i].FileHash)
                    {
                        // Добавляем в список новых файл имя файла подлежащего копированию.
                        newFiles.Add(clientLogFile[i].FilePath);
                    }
                    // Иначе - файлы идентичны.
                    else
                    {
                        // Удаляем одинаковые файлы со списка сервера.
                        serverLogFile.fileList.RemoveAt(fileIndex);
                    }
                }
                // Иначе - файл в лог-файле сервера не найден.
                else
                {
                    // Добавляем в список новых файл имя файла подлежащего копированию.
                    newFiles.Add(clientLogFile[i].FilePath);
                }
            }

            // Если есть файлы для удаления.
            if (serverLogFile.fileList.Count != 0)
            {
                // Удаляем файлы, которые необходимо удалить.
                DeleteFiles(serverLogFile.fileList);
            }

            // Возвращаем список имен файлов подлежащих копированию.
            return newFiles.ToArray();
        }

        //TODO: Запустить метод удаления файлов в отдельном потоке.
        /// <summary>
        /// Удаляет файлы и директории.
        /// </summary>
        /// <param name="deleteFiles">Список файлов подлежащих удалению.</param>
        private void DeleteFiles(List<BackupFile> deleteFiles)
        {
            // Цикл удаления файлов и директорий.
            foreach(var file in deleteFiles)
            {
                try
                {
                    File.Delete(file.FilePath);

                    string dirName = Path.GetDirectoryName(file.FilePath);

                    if (new DirectoryInfo(dirName).GetFiles().Length == 0 &&
                        new DirectoryInfo(dirName).GetDirectories().Length == 0 &&
                        dirName != UserName)
                    {
                        // Удаляем текущею директорию.
                        Directory.Delete(new DirectoryInfo(dirName).FullName);
                        // Удаляем корневую директорию для текущей папки.
                        // Возможно возникновение исключения - не влияет на корректность работы.
                        Directory.Delete(new DirectoryInfo(dirName).Parent.FullName);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                }
            }
        }
        
        /// <summary>
        /// Создает директории по указанному пути к файлу.
        /// </summary>
        /// <param name="path">Путь к файлу.</param>
        private void CreateClientDirectories(string path)
        {
            // Путь к директории.
            string dirPath = null;

            // Цикл прохода по именам директориям.
            foreach (var dir in Path.GetDirectoryName(path).Split(Path.DirectorySeparatorChar))
            {
                // Получаем путь к новой директории.
                dirPath += dir + Path.DirectorySeparatorChar;

                // Если директория не существует.
                if(!Directory.Exists(dirPath))
                {
                    try
                    {
                        // Создаем директорию.
                        Directory.CreateDirectory(dirPath);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Can't to creat directory:\n{0}", ex.Message);
                    }
                }
            }
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