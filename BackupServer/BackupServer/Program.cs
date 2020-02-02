using System;

namespace BackupServer
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
            Console.Write("Input port: ");
            int port = int.Parse(Console.ReadLine());

            // Инстанцируем класс Server.
            Server server = new Server(port);

            // Ожидаем подключения клиента.
            server.ListenConnection();

            // Логинимся.
            server.UserLogin();

            // Получаем и выполняем команды пользователя.
            server.GetClientCommand();

            Console.ReadLine();
        }
    }
}