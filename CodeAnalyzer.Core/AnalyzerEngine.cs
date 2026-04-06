using System.Text;
using System.Text.RegularExpressions;

namespace CodeAnalyzer.Core;

/// <summary>
/// Основной движок анализа исходного кода.
/// Отвечает за сбор файлов, фильтрацию, чтение, формирование отчёта и его разбиение на части.
/// </summary>
public class AnalyzerEngine
{
    /// <summary>
    /// Статистика последнего выполненного анализа.
    /// </summary>
    public AnalysisStats LastRunStats { get; private set; } = new();



    /// <summary>
    /// Запускает асинхронный анализ кода согласно переданной конфигурации.
    /// </summary>
    /// <param name="config">Конфигурация анализа.</param>
    /// <param name="progress">Объект для отправки сообщений о прогрессе.</param>
    /// <returns>Содержимое индексного файла в виде строки.</returns>
    public async Task<string> AnalyzeAsync(AnalyzerConfig config, IProgress<string> progress)
    {
        LastRunStats = new AnalysisStats();
        progress.Report($"Начало анализа по конфигурации: {config.Name}");

        if (!Directory.Exists(config.OutputFolder))
        {
            Directory.CreateDirectory(config.OutputFolder);
            progress.Report($"Создана выходная папка: {config.OutputFolder}");
        }

        var encoding = GetEncoding(config.OutputEncoding);
        var maxSizeBytes = config.MaxFileSizeMB * 1024L * 1024L;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Собираем все файлы глубоко
        var allFiles = new List<FileInfo>();
        int excludedByFolder = 0;

        foreach (var folder in config.SourceFolders)
        {
            if (!Directory.Exists(folder))
            {
                progress.Report($"⚠ Папка не найдена: {folder}");
                continue;
            }

            var masks = GetFileMasks(config);
            var files = EnumerateFilesDeep(folder, masks, config.ExcludeFolders, out var excludedLocal);
            excludedByFolder += excludedLocal;
            allFiles.AddRange(files);
        }

        LastRunStats.SkippedExcludedFolders = excludedByFolder;

        // Удаляем дубликаты и сортируем по полному пути (стабильно, соответствует обходу)
        allFiles = allFiles
            .GroupBy(f => f.FullName.ToLowerInvariant())
            .Select(g => g.First())
            .OrderBy(f => f.FullName)
            .ToList();

        progress.Report($"Найдено файлов: {allFiles.Count} (пропущено по исключениям папок: {excludedByFolder})");

        // Фильтрация бинарных файлов
        var textFiles = new List<FileInfo>();
        foreach (var file in allFiles)
        {
            if (config.ExcludeBinaryFiles && IsBinaryFile(file.FullName))
            {
                LastRunStats.SkippedBinaryFiles++;
                continue;
            }
            textFiles.Add(file);
        }

        LastRunStats.FileCount = textFiles.Count;
        progress.Report($"Будет обработано файлов: {textFiles.Count} (пропущено бинарных: {LastRunStats.SkippedBinaryFiles})");

        // Структура каталогов (текстом)
        string directoryStructure = "";
        if (config.IncludeDirectoryStructure)
        {
            progress.Report("Формирование структуры каталогов...");
            directoryStructure = BuildDirectoryStructure(config.SourceFolders, config.ExcludeFolders, progress);
        }

        // Разбиение на части
        var parts = new List<List<string>>();
        var currentPart = new List<string>();
        long currentSize = 0;
        int partNumber = 1;

        var header = CreateHeader(config, timestamp, partNumber, textFiles.Count, directoryStructure);
        currentPart.AddRange(header);
        currentSize += GetSizeInBytes(header, encoding);

        int processed = 0;
        foreach (var file in textFiles)
        {
            processed++;
            if (processed % 10 == 0)
            {
                progress.Report($"Обработано файлов: {processed}/{textFiles.Count}");
            }

            var fileContent = await ProcessFileAsync(file, config, encoding, progress);
            var fileSection = CreateFileSection(file, fileContent, config);
            var fileSectionSize = GetSizeInBytes(fileSection, encoding);

            if (currentSize + fileSectionSize > maxSizeBytes)
            {
                parts.Add(new List<string>(currentPart));
                partNumber++;
                currentPart.Clear();
                currentSize = 0;

                var newHeader = CreateHeader(config, timestamp, partNumber, textFiles.Count, directoryStructure, true);
                currentPart.AddRange(newHeader);
                currentSize += GetSizeInBytes(newHeader, encoding);
            }

            currentPart.AddRange(fileSection);
            currentSize += fileSectionSize;
        }

        if (currentPart.Count > 0)
        {
            parts.Add(currentPart);
        }

        LastRunStats.PartCount = parts.Count;
        progress.Report($"Сохранение {parts.Count} частей отчета...");

        var createdFiles = new List<string>();
        long totalSize = 0;

        for (int i = 0; i < parts.Count; i++)
        {
            var partContent = string.Join(Environment.NewLine, parts[i]);
            var fileName = $"code_analysis_{timestamp}_part_{i + 1:000}.txt";
            var filePath = Path.Combine(config.OutputFolder, fileName);

            await File.WriteAllTextAsync(filePath, partContent, encoding);

            var fileInfo = new FileInfo(filePath);
            totalSize += fileInfo.Length;
            createdFiles.Add(filePath);

            progress.Report($"Сохранен файл: {fileName} ({FormatSize(fileInfo.Length)})");
        }

        LastRunStats.TotalSizeBytes = totalSize;

        // Индексный файл (как было)
        var indexContent = CreateIndexFile(config, timestamp, createdFiles, totalSize);
        var indexPath = Path.Combine(config.OutputFolder, $"code_analysis_{timestamp}_INDEX.md");
        await File.WriteAllTextAsync(indexPath, indexContent, encoding);

        progress.Report("Анализ завершен!");
        progress.Report($"Всего частей: {parts.Count}");
        progress.Report($"Общий размер: {FormatSize(totalSize)}");
        progress.Report($"Индексный файл: {Path.GetFileName(indexPath)}");

        return indexContent;
    }

    // Объединение масок и расширений
    private List<string> GetFileMasks(AnalyzerConfig config)
    {
        var masks = new List<string>();

        if (config.FileNameMasks != null && config.FileNameMasks.Count > 0)
            masks.AddRange(config.FileNameMasks);

        if (masks.Count == 0)
            masks.AddRange(GetExtensionsToSearch(config));

        if (masks.Count == 0)
            masks.Add("*.*");

        return masks;
    }

    // Новое: обход директорий сверху вниз (Top-Down) с фильтром по маскам и исключаемым папкам
    // Заменяем проблемный метод ShouldExcludePath
    // Удаляем старый метод ShouldExcludePath и используем другую логику

    // Вместо использования ShouldExcludePath внутри лямбды, изменим EnumerateFilesTopDown:
    private IEnumerable<FileInfo> EnumerateFilesTopDown(
        string root,
        IEnumerable<string> masks,
        List<string> excludeFolders,
        ref int excludedByFolder)  // оставляем ref
    {
        var results = new List<FileInfo>();
        int localExcludedCount = 0;

        Traverse(root);

        void Traverse(string currentDir)
        {
            try
            {
                // Директории
                var dirs = Directory.GetDirectories(currentDir).OrderBy(d => d);
                foreach (var dir in dirs)
                {
                    var dirName = Path.GetFileName(dir);
                    if (ShouldExcludeFolderName(dirName, excludeFolders))
                    {
                        localExcludedCount++;
                        continue;
                    }
                    Traverse(dir);
                }

                // Файлы
                var files = Directory.GetFiles(currentDir).OrderBy(f => f);
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (MatchesAnyMask(name, masks))
                    {
                        results.Add(new FileInfo(file));
                    }
                }

            }
            catch
            {
                // игнорируем недоступные директории
            }
        }

        // Переносим локальный счётчик в исходный ref-параметр после завершения обхода
        excludedByFolder += localExcludedCount;

        return results;
    }

    // Глубокий обход всего дерева каталогов (DFS без рекурсии)
    // - уважает excludeFolders по имени на любом уровне
    // - пропускает reparse points (символические ссылки), чтобы избежать циклов
    private IEnumerable<FileInfo> EnumerateFilesDeep(
        string root,
        IEnumerable<string> masks,
        List<string> excludeFolders,
        out int excludedByFolder)
    {
        var results = new List<FileInfo>();
        int excluded = 0;

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            try
            {
                // Подкаталоги
                foreach (var subdir in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(subdir);
                    if (ShouldExcludeFolderName(name, excludeFolders))
                    {
                        excluded++;
                        continue;
                    }

                    // Пропускаем reparse points (symlink/junction)
                    try
                    {
                        var attr = File.GetAttributes(subdir);
                        if ((attr & FileAttributes.ReparsePoint) != 0)
                            continue;
                    }
                    catch
                    {
                        // недоступные/нечитаемые — пропускаем
                        continue;
                    }

                    stack.Push(subdir);
                }

                // Файлы
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    var fileName = Path.GetFileName(file);
                    if (MatchesAnyMask(fileName, masks))
                    {
                        results.Add(new FileInfo(file));
                    }
                }
            }
            catch
            {
                // Недоступные директории пропускаем
            }
        }

        excludedByFolder = excluded;
        return results;
    }

    // Масочное сравнение: поддержка * и ?
    private static bool MatchesAnyMask(string fileName, IEnumerable<string> masks)
    {
        foreach (var mask in masks)
        {
            if (string.IsNullOrWhiteSpace(mask)) continue;
            var regex = "^" + Regex.Escape(mask)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            if (Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase))
                return true;
        }
        return false;
    }


    private List<string> CreateHeader(AnalyzerConfig config, string timestamp, int partNumber, int totalFiles, string directoryStructure, bool isContinuation = false)
    {
        var header = new List<string>();

        if (config.SeparatorStyle == "ai-plain")
        {
            // Минимальный заголовок: в первой части — дерево проекта текстом, затем пустая строка.
            if (!isContinuation && !string.IsNullOrEmpty(directoryStructure) && config.IncludeDirectoryStructure)
            {
                header.Add(directoryStructure);
                header.Add(""); // пустая строка после дерева
            }
            // Иначе ничего не добавляем: фокус на формат "полный путь" -> "содержимое" -> пустая строка
            return header;
        }

        if (config.SeparatorStyle == "markdown")
        {
            header.Add("# АНАЛИЗ КОДА - ОТЧЕТ");
            header.Add($"## Конфигурация: {config.Name}");
            header.Add($"## Дата: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            header.Add($"## Часть: {partNumber}" + (isContinuation ? " (Продолжение)" : ""));
            header.Add("");
            header.Add("---");
            header.Add("");

            if (!isContinuation && !string.IsNullOrEmpty(directoryStructure))
            {
                header.Add("## СТРУКТУРА КАТАЛОГОВ");
                header.Add("```");
                header.Add(directoryStructure);
                header.Add("```");
                header.Add("");
                header.Add("---");
                header.Add("");
            }

            header.Add("## СОДЕРЖИМОЕ ФАЙЛОВ");
            header.Add($"Всего файлов: {totalFiles}");
            header.Add("");
        }
        else
        {
            header.Add(new string('=', 80));
            header.Add("ОТЧЕТ О СОДЕРЖИМОМ ФАЙЛОВ");
            header.Add(new string('=', 80));
            header.Add($"Конфигурация: {config.Name}");
            header.Add($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            header.Add($"Часть: {partNumber}" + (isContinuation ? " (Продолжение)" : ""));
            header.Add("");

            if (!isContinuation && !string.IsNullOrEmpty(directoryStructure))
            {
                header.Add("СТРУКТУРА КАТАЛОГОВ:");
                header.Add(new string('-', 80));
                header.Add(directoryStructure);
                header.Add("");
            }

            header.Add("СОДЕРЖИМОЕ ФАЙЛОВ:");
            header.Add(new string('-', 80));
            header.Add("");
        }

        return header;
    }

    private List<string> CreateFileSection(FileInfo file, string content, AnalyzerConfig config)
    {
        var section = new List<string>();

        if (config.SeparatorStyle == "ai-plain")
        {
            // Формат для нейросетей:
            // Полный путь (или относительный, если UseAbsolutePathsInOutput=false)
            // Содержимое как есть
            // Пустая строка-разделитель
            var pathLine = config.UseAbsolutePathsInOutput ? file.FullName : GetRelativePath(file.FullName, config.SourceFolders);
            section.Add(pathLine);
            section.Add(content);
            section.Add(""); // пустая строка как разделитель
            return section;
        }

        var relativePath = GetRelativePath(file.FullName, config.SourceFolders);

        if (config.SeparatorStyle == "markdown")
        {
            section.Add("");
            section.Add($"### ФАЙЛ: `{file.Name}`");

            if (config.IncludeFileMetadata)
            {
                section.Add($"**Путь:** `{relativePath}`");
                section.Add($"**Размер:** {FormatSize(file.Length)}");
                section.Add($"**Изменен:** {file.LastWriteTime:dd.MM.yyyy HH:mm:ss}");
            }

            section.Add("");
            section.Add("```" + GetLanguageFromExtension(file.Extension));

            if (config.IncludeLineNumbers)
            {
                var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    section.Add($"{i + 1,4}: {lines[i]}");
                }
            }
            else
            {
                section.Add(content);
            }

            section.Add("```");
            section.Add("");
            section.Add("---");
        }
        else
        {
            section.Add(new string('=', 80));
            section.Add($"ФАЙЛ: {file.Name}");

            if (config.IncludeFileMetadata)
            {
                section.Add($"ПУТЬ: {relativePath}");
                section.Add($"ПОЛНЫЙ ПУТЬ: {file.FullName}");
                section.Add($"РАЗМЕР: {FormatSize(file.Length)}");
                section.Add($"ИЗМЕНЕН: {file.LastWriteTime:dd.MM.yyyy HH:mm:ss}");
            }

            section.Add(new string('=', 80));
            section.Add("");

            if (config.IncludeLineNumbers)
            {
                var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    section.Add($"{i + 1:0000}: {lines[i]}");
                }
            }
            else
            {
                section.Add(content);
            }

            section.Add("");
        }

        return section;
    }


    #region Private Helpers

    private async Task<string> ProcessFileAsync(FileInfo file, AnalyzerConfig config, Encoding encoding, IProgress<string> progress)
    {
        try
        {
            var fileEncoding = DetectFileEncoding(file.FullName);
            var content = await File.ReadAllTextAsync(file.FullName, fileEncoding);

            if (fileEncoding != encoding)
            {
                var bytes = fileEncoding.GetBytes(content);
                content = encoding.GetString(bytes);
            }

            return content;
        }
        catch (Exception ex)
        {
            progress.Report($"Ошибка чтения файла {file.Name}: {ex.Message}");
            return $"[ОШИБКА ЧТЕНИЯ ФАЙЛА: {ex.Message}]";
        }
    }

    private string BuildDirectoryStructure(List<string> sourceFolders, List<string> excludeFolders, IProgress<string> progress)
    {
        var sb = new StringBuilder();

        foreach (var root in sourceFolders)
        {
            if (!Directory.Exists(root)) continue;

            sb.AppendLine($"ПАПКА: {root}");
            foreach (var line in EnumerateDirectoryTreeLines(root, excludeFolders))
                sb.AppendLine(line);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private IEnumerable<string> EnumerateDirectoryTreeLines(string root, List<string> excludeFolders)
    {
        var stack = new Stack<(string dir, int depth)>();
        stack.Push((root, 0));

        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();
            string indent = new string(' ', depth * 4);

            IEnumerable<string> subdirs = Array.Empty<string>();
            IEnumerable<string> files = Array.Empty<string>();
            bool hadError = false;

            try
            {
                subdirs = Directory.EnumerateDirectories(dir).OrderBy(d => d);
                files = Directory.EnumerateFiles(dir).OrderBy(f => f);
            }
            catch
            {
                hadError = true;
            }

            if (hadError)
            {
                yield return $"{indent}❌ [Ошибка доступа]";
                continue;
            }

            // Чтобы вывести в алфавитном порядке при использовании стека, добавляем в обратном порядке
            var subdirList = subdirs.ToList();
            subdirList.Reverse();

            foreach (var sub in subdirList)
            {
                var name = Path.GetFileName(sub);
                if (ShouldExcludeFolderName(name, excludeFolders))
                {
                    yield return $"{indent}🚫 {name}/ [ИСКЛЮЧЕНО]";
                    continue;
                }

                yield return $"{indent}📁 {name}/";

                // Пропускаем reparse points (symlink/junction), чтобы не зациклиться
                try
                {
                    var attr = File.GetAttributes(sub);
                    if ((attr & FileAttributes.ReparsePoint) != 0)
                        continue;
                }
                catch
                {
                    continue;
                }

                stack.Push((sub, depth + 1));
            }

            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                string size = "";
                try { size = $" ({FormatSize(new FileInfo(f).Length)})"; } catch { }
                yield return $"{indent}📄 {name}{size}";
            }
        }
    }

    private List<string> GetDirectoryStructureRecursive(string path, string indent, List<string> excludeFolders)
    {
        var result = new List<string>();

        try
        {
            var dirs = Directory.GetDirectories(path).OrderBy(d => d);
            foreach (var dir in dirs)
            {
                var dirName = Path.GetFileName(dir);
                if (ShouldExcludeFolderName(dirName, excludeFolders))
                {
                    result.Add($"{indent}🚫 {dirName}/ [ИСКЛЮЧЕНО]");
                    continue;
                }
                result.Add($"{indent}📁 {dirName}/");
                result.AddRange(GetDirectoryStructureRecursive(dir, indent + "    ", excludeFolders));
            }

            var files = Directory.GetFiles(path).OrderBy(f => f);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var size = FormatSize(new FileInfo(file).Length);
                result.Add($"{indent}📄 {fileName} ({size})");
            }
        }
        catch
        {
            result.Add($"{indent}❌ [Ошибка доступа]");
        }

        return result;
    }

    private string CreateIndexFile(AnalyzerConfig config, string timestamp, List<string> createdFiles, long totalSize)
    {
        var index = new List<string>
        {
            "# ИНДЕКСНЫЙ ФАЙЛ - АНАЛИЗ КОДА",
            "",
            $"## Конфигурация: {config.Name}",
            $"## Дата анализа: {DateTime.Now:dd.MM.yyyy HH:mm:ss}",
            $"## Время выполнения: {timestamp}",
            "",
            "## 📊 Статистика",
            $"- Обработано файлов: {LastRunStats.FileCount}",
            $"- Пропущено бинарных файлов: {LastRunStats.SkippedBinaryFiles}",
            $"- Пропущено по исключениям папок: {LastRunStats.SkippedExcludedFolders}",
            $"- Создано частей: {LastRunStats.PartCount}",
            $"- Общий размер: {FormatSize(totalSize)}",
            "",
            "## 📁 Исходные папки"
        };

        foreach (var folder in config.SourceFolders)
        {
            index.Add($"- `{folder}`");
        }

        if (config.ExcludeFolders.Count > 0)
        {
            index.Add("");
            index.Add("## ⛔ Исключаемые папки");
            foreach (var folder in config.ExcludeFolders)
            {
                index.Add($"- `{folder}`");
            }
        }

        index.Add("");
        index.Add("## 🔧 Настройки анализа");
        index.Add($"- Режим расширений: {GetExtensionModeDescription(config.ExtensionMode)}");
        if (config.ExtensionMode == ExtensionMode.Preset)
        {
            index.Add($"- Предустановка: {GetPresetDescription(config.SelectedPreset)}");
        }
        else if (config.ExtensionMode == ExtensionMode.Custom && config.CustomExtensions.Count > 0)
        {
            index.Add($"- Расширения: {string.Join(", ", config.CustomExtensions)}");
        }
        index.Add($"- Исключать бинарные файлы: {(config.ExcludeBinaryFiles ? "Да" : "Нет")}");
        index.Add($"- Макс. размер части: {config.MaxFileSizeMB} МБ");

        index.Add("");
        index.Add("## 🗂️ Созданные файлы");
        index.Add("");

        for (int i = 0; i < createdFiles.Count; i++)
        {
            var file = createdFiles[i];
            var size = FormatSize(new FileInfo(file).Length);
            index.Add($"{i + 1}. **{Path.GetFileName(file)}** - {size}");
        }

        index.Add("");
        index.Add("---");
        index.Add("*Этот файл создан автоматически анализатором кода*");

        return string.Join(Environment.NewLine, index);
    }

    private List<string> GetExtensionsToSearch(AnalyzerConfig config)
    {
        if (config.ExtensionMode == ExtensionMode.AllFiles)
        {
            return ["*.*"];
        }
        else if (config.ExtensionMode == ExtensionMode.Custom)
        {
            return config.CustomExtensions.Count > 0 ? config.CustomExtensions : ["*.*"];
        }
        else // Preset
        {
            return config.SelectedPreset switch
            {
                ExtensionPreset.WebProject => [
                    "*.html", "*.css", "*.js", "*.ts", "*.jsx", "*.tsx",
                    "*.json", "*.xml", "*.md", "*.txt", "*.config"
                ],
                ExtensionPreset.CSharpProject => [
                    "*.cs", "*.csproj", "*.sln", "*.config", "*.json", "*.xml",
                    "*.txt", "*.md"
                ],
                ExtensionPreset.PythonProject => [
                    "*.py", "*.pyw", "*.pyx", "*.txt", "*.md", "*.json",
                    "*.yml", "*.yaml", "*.ini", "*.cfg", "*.toml"
                ],
                ExtensionPreset.CppProject => [
                    "*.cpp", "*.h", "*.hpp", "*.c", "*.cc", "*.cxx",
                    "*.hxx", "*.inl", "*.txt", "*.md", "*.json", "*.cmake", "*.make"
                ],
                _ => [ // AllSupported
                    "*.cs", "*.js", "*.html", "*.css", "*.xml", "*.json",
                    "*.config", "*.csproj", "*.sln", "*.sql", "*.txt",
                    "*.md", "*.py", "*.java", "*.php", "*.rb", "*.cpp", "*.h",
                    "*.ts", "*.jsx", "*.tsx", "*.yml", "*.yaml", "*.ini",
                    "*.cshtml", "*.aspx", "*.vb", "*.fs"
                ]
            };
        }
    }

    private bool ShouldExcludePath(string filePath, List<string> excludePatterns)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (directory == null) return false;

        var pathParts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in pathParts)
        {
            if (ShouldExcludeFolderName(part, excludePatterns))
            {
                return true;
            }
        }
        return false;
    }


    private bool ShouldExcludeFolderName(string folderName, List<string> excludePatterns)
    {
        foreach (var pattern in excludePatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            if (folderName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            if (pattern.Contains('*'))
            {
                try
                {
                    var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                    if (Regex.IsMatch(folderName, regexPattern, RegexOptions.IgnoreCase))
                        return true;
                }
                catch
                {
                    if (pattern.EndsWith('*') && folderName.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    private string GetExtensionModeDescription(ExtensionMode mode) => mode switch
    {
        ExtensionMode.Preset => "Предустановки",
        ExtensionMode.Custom => "Свой список",
        ExtensionMode.AllFiles => "Все файлы",
        _ => "Неизвестно"
    };

    private string GetPresetDescription(ExtensionPreset preset) => preset switch
    {
        ExtensionPreset.AllSupported => "Все поддерживаемые",
        ExtensionPreset.WebProject => "Веб-проект",
        ExtensionPreset.CSharpProject => "C# проект",
        ExtensionPreset.PythonProject => "Python проект",
        ExtensionPreset.CppProject => "C++ проект",
        _ => "Неизвестно"
    };

    private Encoding GetEncoding(string encodingName) => encodingName.ToUpperInvariant() switch
    {
        "UTF-8" => Encoding.UTF8,
        "UTF-32" => Encoding.UTF32,
        "UNICODE" => Encoding.Unicode,
        "ASCII" => Encoding.ASCII,
        "WINDOWS-1251" => Encoding.GetEncoding(1251),
        _ => Encoding.UTF8
    };

    private static bool IsBinaryFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var buffer = new byte[1024];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0) return true;
                }
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static Encoding DetectFileEncoding(string filePath)
    {
        try
        {
            var bom = new byte[4];
            using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var bytesRead = file.Read(bom, 0, 4);

            if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return Encoding.UTF8;
            if (bytesRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
            if (bytesRead >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
            if (bytesRead >= 4 && bom[0] == 0 && bom[1] == 0 && bom[2] == 0xFE && bom[3] == 0xFF) return Encoding.UTF32;

            var encodings = new[] { Encoding.UTF8, Encoding.GetEncoding(1251), Encoding.Default };
            foreach (var encoding in encodings)
            {
                try
                {
                    var content = File.ReadAllText(filePath, encoding);
                    if (content.Length > 0 && content.Count(c => c == '\uFFFD') < content.Length * 0.1)
                        return encoding;
                }
                catch { }
            }

            return Encoding.UTF8;
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static long GetSizeInBytes(IEnumerable<string> lines, Encoding encoding)
    {
        var text = string.Join(Environment.NewLine, lines);
        return encoding.GetByteCount(text);
    }

    private static string GetRelativePath(string fullPath, List<string> sourceFolders)
    {
        foreach (var folder in sourceFolders)
        {
            if (fullPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = fullPath.Substring(folder.Length);
                return relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
        return fullPath;
    }

    private static string GetLanguageFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" => "csharp",
        ".js" => "javascript",
        ".html" => "html",
        ".css" => "css",
        ".xml" => "xml",
        ".json" => "json",
        ".py" => "python",
        ".java" => "java",
        ".php" => "php",
        ".rb" => "ruby",
        ".cpp" or ".h" => "cpp",
        ".sql" => "sql",
        ".md" => "markdown",
        ".yml" or ".yaml" => "yaml",
        ".txt" => "text",
        ".config" or ".csproj" => "xml",
        ".sln" => "text",
        ".ts" => "typescript",
        ".jsx" => "javascript",
        ".tsx" => "typescript",
        ".vb" => "vb",
        ".fs" => "fsharp",
        _ => "text"
    };

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    #endregion
}