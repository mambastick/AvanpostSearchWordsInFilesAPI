using System.Text.Json;

namespace SearchWordsInFilesAPI
{
    public class Startup(IConfiguration configuration)
    {
        private IConfiguration Configuration { get; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting(); // Добавление сервиса маршрутизации
        }
        
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting(); // Промежуточное ПО для маршрутизации запросов
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/files/search", async context => // Обработчик GET-запроса на путь /files/search
                {
                    await GetRequestFilesSearch(context);
                });
            });
        }

        private async Task GetRequestFilesSearch(HttpContext context)
        {
            string keyword = context.Request.Query["keyword"]; // Получение ключевого слова из параметров запроса
            if (string.IsNullOrWhiteSpace(keyword))
            {
                context.Response.StatusCode = 400; // Установка кода ответа в случае отсутствия ключевого слова
                await context.Response.WriteAsync("Ключевое слово не указано."); // Отправка сообщения об ошибке
                return;
            }

            var directoryPath = Configuration["FileSearchOptions:ExamplesDirectoryPath"]; // Путь к каталогу с файлами
            if (!Directory.Exists(directoryPath))
            {
                context.Response.StatusCode = 500; // Установка кода ответа в случае отсутствия каталога
                await context.Response.WriteAsync("Указанный каталог не существует."); // Отправка сообщения об ошибке
                return;
            }

            var tasks = Directory.GetFiles(directoryPath)
                .Select(filePath => SearchFileAsync(filePath, keyword))
                .ToList(); // Создание списка задач для асинхронного поиска в файлах

            await Task.WhenAll(tasks); // Ожидание завершения всех задач

            var filesContainingKeyword = tasks
                .SelectMany(t => t.Result)
                .Select(fileName => new { fileName });

            var jsonResponse = JsonSerializer.Serialize(new { fileNames = filesContainingKeyword });
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(jsonResponse);
        }
        
        private static async Task<List<string>> SearchFileAsync(string filePath, string keyword)
        {
            var result = new List<string>(); // Создание списка для хранения имен файлов с ключевым словом
            if ((await File.ReadAllTextAsync(filePath)).Contains(keyword, StringComparison.OrdinalIgnoreCase)) // Поиск ключевого слова в файле
            {
                result.Add(Path.GetFileName(filePath)); // Добавление имени файла в список результатов
            }
            return result; // Возврат списка результатов
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .ConfigureAppConfiguration((context, builder) =>
                {
                    // Добавляем переменные среды
                    var env = context.HostingEnvironment;
 
                    // Добавялем конфигурационные файлы
                    builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                })
                .UseKestrel() // Настройка веб-сервера Kestrel
                .UseStartup<Startup>() // Указание класса Startup как точки входа для настройки приложения
                .Build(); // Создание и конфигурирование хоста приложения

            host.Run(); // Запуск веб-приложения
        }
    }
}
