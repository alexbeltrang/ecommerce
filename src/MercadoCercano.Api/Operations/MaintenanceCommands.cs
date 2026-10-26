using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MercadoCercano.Api.Operations;

public static class MaintenanceCommands
{
    public static async Task<int> RunAsync(string[] args, string contentRoot)
    {
        try
        {
            if (args.Length == 0) throw new InvalidOperationException("Usa --maintenance migrate|create-admin|backup|verify-backup|restore.");
            var configuration = new ConfigurationBuilder().SetBasePath(contentRoot)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Production.json", optional: false).AddEnvironmentVariables().Build();
            var storage = ProductionStorage.Load(configuration, contentRoot);
            var backups = new BackupManager(storage);
            switch (args[0])
            {
                case "migrate" when args.Length == 1:
                    using (storage.AcquireLease(TimeSpan.FromSeconds(60)))
                    {
                        await using var db = storage.OpenDatabase(create: true);
                        await db.Database.MigrateAsync();
                    }
                    Console.WriteLine("Migraciones aplicadas. No se crearon datos de demostracion ni usuarios.");
                    break;
                case "create-admin" when args.Length == 1:
                    if (Console.IsInputRedirected) throw new InvalidOperationException("create-admin requiere una consola interactiva privada.");
                    using (storage.AcquireLease(TimeSpan.FromSeconds(60)))
                    {
                        Console.Write("Nombre: "); var name = Console.ReadLine() ?? "";
                        Console.Write("Correo: "); var email = Console.ReadLine() ?? "";
                        Console.Write("Contraseña: "); var password = ReadPassword();
                        Console.Write("Confirmar contraseña: "); var confirmation = ReadPassword();
                        if (password != confirmation) throw new InvalidOperationException("Las contraseñas no coinciden.");
                        await using var db = storage.OpenDatabase();
                        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Aplica primero las migraciones.");
                        await ProductionAdministrator.CreateAsync(db, name, email, password, CancellationToken.None);
                    }
                    Console.WriteLine("Primer administrador creado. No se inicio sesion.");
                    break;
                case "backup" when args.Length == 1:
                    Console.WriteLine("Respaldo verificado: " + await backups.CreateAsync(CancellationToken.None));
                    break;
                case "verify-backup" when args.Length == 2:
                    await backups.VerifyAsync(args[1], CancellationToken.None);
                    Console.WriteLine("Respaldo verificado: SHA-256, SQLite, relaciones y archivos referenciados correctos.");
                    break;
                case "restore" when args.Length is 3 or 4 && args[2] == "--confirm-restore" &&
                    (args.Length == 3 || args[3] == "--new-machine"):
                    Console.WriteLine("Restauracion terminada. Sesiones revocadas. Datos anteriores conservados en: " +
                        await backups.RestoreAsync(args[1], args.Length == 4, CancellationToken.None));
                    break;
                default:
                    throw new InvalidOperationException("Comando o argumentos de mantenimiento invalidos. restore exige nombre de respaldo y --confirm-restore.");
            }
            return 0;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException or
            Microsoft.Data.Sqlite.SqliteException or DbUpdateException or System.Text.Json.JsonException or AggregateException)
        {
            Console.Error.WriteLine("ERROR de mantenimiento: " + error.Message);
            return 1;
        }
    }

    private static string ReadPassword()
    {
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Escape) throw new InvalidOperationException("Aprovisionamiento cancelado.");
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
            else if (!char.IsControl(key.KeyChar))
            {
                if (value.Length >= 256) throw new InvalidOperationException("La entrada supera el limite permitido.");
                value.Append(key.KeyChar);
            }
        }
    }
}
