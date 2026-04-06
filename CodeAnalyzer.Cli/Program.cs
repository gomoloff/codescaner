// <copyright file="Program.cs" company="CodeAnalyzer">
// Copyright (c) CodeAnalyzer. All rights reserved.
// </copyright>

using CodeAnalyzer.Core;

/// <summary>
/// Точка входа консольного приложения анализатора исходного кода.
/// Поддерживает два режима работы:
/// <list type="bullet">
///   <item><description><b>CLI-режим</b>: запуск с аргументами командной строки.</description></item>
///   <item><description><b>Интерактивный режим</b>: пошаговый ввод параметров, если аргументы не заданы.</description></item>
/// </list>
/// </summary>
public static class Program
{
    /// <summary>
    /// Основная точка входа приложения.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код завершения: 0 — успех, 1 — ошибка.</returns>
    public static async Task<int> Main(string[] args)
    {
        // Если переданы аргументы — работаем в CLI-режиме
        if (args.Length > 0)
        {
            return await RunFromArgs(args);
        }

        // Иначе — интерактивный режим
        Console.WriteLine("=== Анализатор исходного кода (интерактивный режим) ===");
        Console.WriteLine();

        var config = new AnalyzerConfig
        {
            Name = "Интерактивная конфигурация"
        };

        // 1. Источники
        Console.WriteLine("Укажите папки для анализа (по одной на строку). Пустая строка — завершить ввод:");
        config.SourceFolders = new List<string>();
        while (true)
        {
            Console.Write("Папка: ");
            var folder = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(folder)) break;
            folder = folder.Trim();
            if (Directory.Exists(folder))
            {
                config.SourceFolders.Add(Path.GetFullPath(folder));
            }
            else
            {
                Console.WriteLine($"⚠ Папка не найдена: {folder}");
            }
        }

        if (config.SourceFolders.Count == 0)
        {
            Console.WriteLine("Ошибка: не указано ни одной существующей папки.");
            return 1;
        }

        // 2. Исключения
        Console.WriteLine("\nИсключаемые папки (например: bin, obj, node_modules, *test*). Пустая строка — пропустить:");
        config.ExcludeFolders = new List<string>();
        while (true)
        {
            Console.Write("Исключение: ");
            var excl = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(excl)) break;
            config.ExcludeFolders.Add(excl.Trim());
        }

        // 3. Выходная папка
        Console.Write("\nВыходная папка (Enter для ./report): ");
        var output = Console.ReadLine();
        config.OutputFolder = string.IsNullOrWhiteSpace(output) ? "./report" : output.Trim();

        // 4. Макс. размер
        Console.Write("Макс. размер части отчёта в МБ (Enter для 25): ");
        var maxSizeStr = Console.ReadLine();
        config.MaxFileSizeMB = int.TryParse(maxSizeStr, out var size) && size > 0 ? size : 25;

        // 5. Режим расширений
        Console.WriteLine("\nРежим расширений:");
        Console.WriteLine("  1. Предустановки (Web, C#, Python...)");
        Console.WriteLine("  2. Свой список");
        Console.WriteLine("  3. Все файлы");
        Console.Write("Выберите (1-3, Enter для 1): ");
        var modeChoice = Console.ReadLine();
        config.ExtensionMode = modeChoice switch
        {
            "2" => ExtensionMode.Custom,
            "3" => ExtensionMode.AllFiles,
            _ => ExtensionMode.Preset
        };

        if (config.ExtensionMode == ExtensionMode.Preset)
        {
            Console.WriteLine("\nВыберите предустановку:");
            Console.WriteLine("  1. Все поддерживаемые");
            Console.WriteLine("  2. Веб-проект");
            Console.WriteLine("  3. C# проект");
            Console.WriteLine("  4. Python проект");
            Console.WriteLine("  5. C++ проект");
            Console.Write("Выберите (1-5, Enter для 1): ");
            var presetChoice = Console.ReadLine();
            config.SelectedPreset = presetChoice switch
            {
                "2" => ExtensionPreset.WebProject,
                "3" => ExtensionPreset.CSharpProject,
                "4" => ExtensionPreset.PythonProject,
                "5" => ExtensionPreset.CppProject,
                _ => ExtensionPreset.AllSupported
            };
        }
        else if (config.ExtensionMode == ExtensionMode.Custom)
        {
            Console.Write("\nУкажите расширения через запятую (например: .cs,.js,.py): ");
            var extInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(extInput))
            {
                config.CustomExtensions = extInput
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.StartsWith(".") ? e : "." + e)
                    .ToList();
            }
        }

        // Остальные параметры — по умолчанию
        config.ExcludeBinaryFiles = true;
        config.IncludeDirectoryStructure = true;
        config.OutputEncoding = "UTF-8";
        config.SeparatorStyle = "markdown";
        config.IncludeLineNumbers = true;
        config.IncludeFileMetadata = true;

        try
        {
            var engine = new AnalyzerEngine();
            await engine.AnalyzeAsync(config, new Progress<string>(msg => Console.WriteLine($"[INFO] {msg}")));
            Console.WriteLine("\n✅ Анализ завершён!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ОШИБКА] {ex.Message}");
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Обрабатывает аргументы командной строки и запускает анализ.
    /// </summary>
    /// <param name="args">Массив аргументов.</param>
    /// <returns>Код завершения: 0 — успех, 1 — ошибка.</returns>
    private static async Task<int> RunFromArgs(string[] args)
    {
        var config = new AnalyzerConfig
        {
            Name = "CLI-конфигурация",
            SourceFolders = new List<string>(),
            ExcludeFolders = new List<string>(),
            OutputFolder = "./report",
            MaxFileSizeMB = 25,
            ExtensionMode = ExtensionMode.Preset,
            SelectedPreset = ExtensionPreset.AllSupported,
            CustomExtensions = new List<string>(),
            ExcludeBinaryFiles = true,
            IncludeDirectoryStructure = true,
            OutputEncoding = "UTF-8",
            SeparatorStyle = "markdown",
            IncludeLineNumbers = true,
            IncludeFileMetadata = true
        };

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--source" || arg == "-s")
            {
                if (i + 1 >= args.Length) { Console.WriteLine("Ошибка: --source требует значение"); return 1; }
                config.SourceFolders.Add(args[++i]);
            }
            else if (arg == "--exclude" || arg == "-x")
            {
                if (i + 1 >= args.Length) { Console.WriteLine("Ошибка: --exclude требует значение"); return 1; }
                config.ExcludeFolders.Add(args[++i]);
            }
            else if (arg == "--output" || arg == "-o")
            {
                if (i + 1 >= args.Length) { Console.WriteLine("Ошибка: --output требует значение"); return 1; }
                config.OutputFolder = args[++i];
            }
            else if (arg == "--max-size-mb")
            {
                if (i + 1 >= args.Length || !int.TryParse(args[++i], out var size)) { Console.WriteLine("Ошибка: --max-size-mb требует число"); return 1; }
                config.MaxFileSizeMB = size;
            }
            else if (arg == "--mode")
            {
                if (i + 1 >= args.Length) { Console.WriteLine("Ошибка: --mode требует значение"); return 1; }
                var modeStr = args[++i];
                if (!Enum.TryParse<ExtensionMode>(modeStr, true, out var mode)) { Console.WriteLine($"Неверный режим: {modeStr}"); return 1; }
                config.ExtensionMode = mode;
            }
            else if (arg == "--preset")
            {
                if (i + 1 >= args.Length) { Console.WriteLine("Ошибка: --preset требует значение"); return 1; }
                var presetStr = args[++i];
                if (!Enum.TryParse<ExtensionPreset>(presetStr, true, out var preset)) { Console.WriteLine($"Неверная предустановка: {presetStr}"); return 1; }
                config.SelectedPreset = preset;
            }
            else if (arg == "--extensions")
            {
                if (i + 1 >= args.Length) { Console.WriteLine("Ошибка: --extensions требует значение"); return 1; }
                config.CustomExtensions = args[++i]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.StartsWith(".") ? e : "." + e)
                    .ToList();
            }
            else if (arg == "--no-binary")
            {
                config.ExcludeBinaryFiles = true;
            }
            else if (arg == "--binary")
            {
                config.ExcludeBinaryFiles = false;
            }
            else if (arg == "--dir-structure")
            {
                config.IncludeDirectoryStructure = true;
            }
            else if (arg == "--no-dir-structure")
            {
                config.IncludeDirectoryStructure = false;
            }
            else if (arg == "--encoding")
            {
                if (i + 1 >= args.Length) { Console.WriteLine("Ошибка: --encoding требует значение"); return 1; }
                config.OutputEncoding = args[++i];
            }
            else if (arg == "--style")
            {
                if (i + 1 >= args.Length) { Console.WriteLine("Ошибка: --style требует значение"); return 1; }
                config.SeparatorStyle = args[++i];
            }
            else if (arg == "--line-numbers")
            {
                config.IncludeLineNumbers = true;
            }
            else if (arg == "--no-line-numbers")
            {
                config.IncludeLineNumbers = false;
            }
            else if (arg == "--metadata")
            {
                config.IncludeFileMetadata = true;
            }
            else if (arg == "--no-metadata")
            {
                config.IncludeFileMetadata = false;
            }
            else if (arg == "--help" || arg == "-h")
            {
                PrintHelp();
                return 0;
            }
            else
            {
                Console.WriteLine($"Неизвестный аргумент: {arg}");
                PrintHelp();
                return 1;
            }
        }

        if (config.SourceFolders.Count == 0)
        {
            Console.WriteLine("Ошибка: требуется хотя бы одна папка (--source)");
            PrintHelp();
            return 1;
        }

        try
        {
            var engine = new AnalyzerEngine();
            await engine.AnalyzeAsync(config, new Progress<string>(msg => Console.WriteLine($"[INFO] {msg}")));
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ОШИБКА] {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Выводит справку по использованию CLI.
    /// </summary>
    private static void PrintHelp()
    {
        Console.WriteLine(@"
Анализатор исходного кода (CLI)

Использование:
  dotnet CodeAnalyzer.Cli.dll [опции]

Обязательные опции:
  -s, --source <путь>       Папка для анализа (можно указать несколько раз)

Опции:
  -x, --exclude <имя>       Исключаемая папка (можно несколько раз)
  -o, --output <путь>       Выходная папка (по умолчанию: ./report)
      --max-size-mb <число> Макс. размер части отчёта в МБ (по умолчанию: 25)
      --mode <Preset|Custom|AllFiles> Режим расширений (по умолчанию: Preset)
      --preset <AllSupported|WebProject|CSharpProject|PythonProject|CppProject>
                            Предустановка (если mode=Preset)
      --extensions <список> Список расширений через запятую (если mode=Custom)
      --no-binary           Исключать бинарные файлы (по умолчанию: да)
      --binary              Не исключать бинарные файлы
      --dir-structure       Включать структуру каталогов (по умолчанию: да)
      --no-dir-structure    Не включать структуру каталогов
      --encoding <кодировка> Кодировка вывода (по умолчанию: UTF-8)
      --style <markdown|plain> Стиль разделителей
      --line-numbers        Нумерация строк (по умолчанию: да)
      --no-line-numbers     Без нумерации строк
      --metadata            Метаданные файлов (по умолчанию: да)
      --no-metadata         Без метаданных
  -h, --help                Показать эту справку

Если запустить без аргументов — будет запущен интерактивный режим.
");
    }
}