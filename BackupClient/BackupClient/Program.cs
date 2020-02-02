using System;

namespace BackupClient
{
    /// <summary>
    /// Класс содержащий точку входа в программу.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Точка входа в программу.
        /// </summary>
        /// <param name="args">Массив строк передаваемых при запуске программы приложению.</param>
        static void Main(string[] args)
        {
            Console.Write("Input ip server: ");
            string ip = Console.ReadLine();

            Console.Write("Input port server: ");
            int port = int.Parse(Console.ReadLine());

            // Инстанцируем объект класса Client.
            Client client = new Client(ip, port);
            
            // Соединяемся с сервером.
            client.ConnectToServer();
            
            string choise = null;
            bool flagExit = false;
            bool flagScan = false;

            // Логинимся.
            client.UserLogin();

            while (!flagExit)
            {
                Console.Write("Type 'scan','sync' or 'exit': ");
                choise = Console.ReadLine();

                switch (choise)
                {
                    case "scan":
                        {
                            client.Scan();

                            flagScan = true;
                            break;
                        }
                    case "sync":
                        {
                            if(!flagScan)
                            {
                                Console.WriteLine("Press scan before sync!");
                                Console.Write(new string('-', 80));
                                break;
                            }

                            client.Sync();

                            flagScan = false;
                            break;
                        }
                    case "exit":
                        {
                            client.Disconnect();
                            flagExit = true;
                            break;
                        }
                    default: ShowInsturction();     break;
                }
                
            }
              
            Console.ReadLine();
        }

        /// <summary>
        /// Выводит инструкцию по работе с программою.
        /// </summary>
        static void ShowInsturction()
        {
            Console.WriteLine(new string('-', 80));

            Console.WriteLine("For scanning directory input: 'scan'");
            Console.WriteLine("For sync with server input: 'sync'");
            Console.WriteLine("For exit of program input: 'exit'");

            Console.WriteLine(new string('-', 80));

        }
    }
}
